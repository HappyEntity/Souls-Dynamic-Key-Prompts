using System.Runtime.InteropServices;

namespace DynamicKeyPrompts;

/// Replaces the gamepad buttons in DS3 prompts.
///
/// Prompts show buttons as Scaleform images: L"<img src='img://%s' vspace='-8'>", where %s is an icon name
/// (KG_OK, KG_R1, ...) read from the message repository (category 203, ids 2001..2022). The icon name
/// lookup is hooked: when the markup builder asks for it, the button is resolved to the action behind it
/// and the key label is returned instead. Tutorial tags (<?kgAttackR?>) get their markup through a
/// separate replace function, hooked as well.
static unsafe class Prompts
{
    const int IconCategory = 203, FirstIcon = 2001, LastIcon = 2022;
    const int TabRightText = 100000, TabLeftText = 100001;
    /// The camera stick: shown as mouse movement.
    const int MouseMoveEntry = -1;

    static delegate* unmanaged<nint, int, int, int, nint> s_getMsg;
    static delegate* unmanaged<nint, nint, nint, nint, nint> s_replaceTag;

    public static bool Install()
    {
        s_getMsg = (delegate* unmanaged<nint, int, int, int, nint>)Loader.Hook("GetMsg", Loader.GameBase + Rva.GetMsg,
            (nint)(delegate* unmanaged<nint, int, int, int, nint>)&GetMsgHook);
        s_replaceTag = (delegate* unmanaged<nint, nint, nint, nint, nint>)Loader.Hook("ReplaceTag", Loader.GameBase + Rva.ReplaceTag,
            (nint)(delegate* unmanaged<nint, nint, nint, nint, nint>)&ReplaceTagHook);
        return s_getMsg != null;
    }

    // ------------------------------------------------------------------ buttons in prompts

    // Button (icon id) -> key config entries, for prompts in menus and in the world.
    static readonly Dictionary<int, int[]> MenuButtons = new()
    {
        [2001] = [KeyConfig.Confirm], [2002] = [KeyConfig.Cancel],
        [2003] = [KeyConfig.CursorUp], [2004] = [KeyConfig.CursorDown], [2005] = [KeyConfig.CursorLeft], [2006] = [KeyConfig.CursorRight],
        [2007] = [KeyConfig.CursorUp, KeyConfig.CursorDown], [2008] = [KeyConfig.CursorLeft, KeyConfig.CursorRight],
        [2009] = [KeyConfig.CursorUp, KeyConfig.CursorDown, KeyConfig.CursorLeft, KeyConfig.CursorRight],
        [2010] = [KeyConfig.Function2], [2011] = [KeyConfig.Function1], [2012] = [KeyConfig.TabLeft], [2013] = [KeyConfig.TabRight],
        [2014] = [KeyConfig.ScrollUp], [2015] = [KeyConfig.ScrollDown], [2016] = [KeyConfig.Sort], [2017] = [KeyConfig.Function3],
        [2020] = [KeyConfig.Gesture], [2022] = [KeyConfig.Menu],
    };

    static readonly Dictionary<int, int[]> WorldButtons = new()
    {
        [2001] = [KeyConfig.Interact], [2002] = [KeyConfig.Dash],
        [2003] = [KeyConfig.ChangeMagic], [2004] = [KeyConfig.ChangeItem], [2005] = [KeyConfig.ChangeLeft], [2006] = [KeyConfig.ChangeRight],
        [2007] = [KeyConfig.ChangeMagic, KeyConfig.ChangeItem], [2008] = [KeyConfig.ChangeLeft, KeyConfig.ChangeRight],
        [2010] = [KeyConfig.TwoHand], [2011] = [KeyConfig.UseItem], [2012] = [KeyConfig.AttackL], [2013] = [KeyConfig.AttackR],
        [2014] = [KeyConfig.StrongL], [2015] = [KeyConfig.StrongR], [2016] = [KeyConfig.Jump], [2017] = [KeyConfig.CameraReset],
        [2009] = [KeyConfig.ChangeMagic, KeyConfig.ChangeItem, KeyConfig.ChangeLeft, KeyConfig.ChangeRight],
        [2018] = [KeyConfig.RunForward, KeyConfig.RunLeft, KeyConfig.RunBack, KeyConfig.RunRight],
        [2019] = [MouseMoveEntry],
        [2020] = [KeyConfig.Gesture], [2022] = [KeyConfig.Menu],
    };

    static int s_languageChecks;

    [UnmanagedCallersOnly]
    static nint GetMsgHook(nint repo, int zero, int category, int id)
    {
        nint text = s_getMsg(repo, zero, category, id);
        if (text == 0) return text;
        if (s_languageChecks < 500) DetectLanguage(text);
        if (!Config.Enabled) return text;
        // Tab switch hints next to the menu tabs: plain text "LB" / "RB"
        if (id is TabRightText or TabLeftText && text != 0 && ((char*)text)[0] is 'L' or 'R' && ((char*)text)[1] == 'B' && ((char*)text)[2] == 0)
            try
            {
                if (KeyConfig.Parts(id == TabLeftText ? KeyConfig.TabLeft : KeyConfig.TabRight) is { } parts)
                    return Labels.TabText(parts);
            }
            catch (Exception e) { Loader.LogOnce("tabs: " + e.Message); }
        if (category != IconCategory || id is < FirstIcon or > LastIcon) return text;
        try
        {
            if (ButtonEntries(id) is { } entries && Prompt(entries) is { } prompt)
                return Labels.InImageSource(prompt);
        }
        catch (Exception e) { Loader.LogOnce("GetMsg: " + e.Message); }
        return text;
    }

    /// The key config entries behind the button asked for by the markup builder, or null when the
    /// icon is requested for something else (the Key Bindings screen shows the pad icons themselves).
    static int[]? ButtonEntries(int id)
    {
        Span<nint> frames = stackalloc nint[32];
        int n = Native.CallStack(frames);
        long game = Loader.GameBase;
        for (int i = 0; i + 2 < n; i++)
        {
            if (frames[i] - game != Rva.MarkupBuilderReturn) continue;
            // frames[i + 1] is in the button's function, frames[i + 2] in whoever asked for the button
            long caller = frames[i + 2] - game;
            // World prompts pass the key config entry right after the call: [nop] mov r9d, imm32
            byte* code = (byte*)(game + caller);
            int k = code[0] == 0x90 ? 1 : 0;
            if (code[k] == 0x41 && code[k + 1] == 0xB9 && *(int*)(code + k + 2) is var entry and >= 0 and < KeyConfig.EntryCount)
            {
                if (Config.Diagnostics) LogButton(id, frames, i, n, $"entry {entry} (passed by the caller)");
                return [entry];
            }
            bool menu = caller >= Rva.MenuTagsBegin && caller < Rva.MenuTagsEnd;
            if (menu)
                for (int j = i + 3; j < n; j++)
                {
                    long ret = frames[j] - game;
                    // The Key Bindings screen's Controller column keeps the gamepad icons
                    if (ret is Rva.KeyBindingsPadIcon1 or Rva.KeyBindingsPadIcon2 or Rva.KeyBindingsPadIcon3)
                    {
                        if (Config.Diagnostics) LogButton(id, frames, i, n, "key bindings controller column, unchanged");
                        return null;
                    }
                    // The action list at objects formats menu tags that mean game actions
                    if (ret == Rva.ActionListPrompt) { menu = false; break; }
                }
            int[]? entries = (menu ? MenuButtons : WorldButtons).GetValueOrDefault(id);
            if (Config.Diagnostics)
                LogButton(id, frames, i, n, $"{(menu ? "menu" : "world")} table -> {(entries == null ? "none" : string.Join(",", entries))}");
            return entries;
        }
        return null;
    }

    static bool s_keyConfigLogged;

    /// Diagnostics=1: which code asked for a button icon and what it was resolved to (once per call site),
    /// and the key config once, to compare with the Key Bindings screen.
    static void LogButton(int id, Span<nint> frames, int i, int n, string result)
    {
        if (!s_keyConfigLogged)
        {
            s_keyConfigLogged = true;
            Loader.Log(KeyConfig.Dump());
        }
        long game = Loader.GameBase;
        var callers = new System.Text.StringBuilder();
        for (int j = i + 1; j < n && j < i + 16; j++) callers.Append($" {frames[j] - game:X}");
        Loader.LogOnce($"button {id}: callers{callers} -> {result}");
    }

    // ------------------------------------------------------------------ tutorial tags

    static readonly Dictionary<string, int[]> TutorialTags = new()
    {
        ["<?kgRun?>"] = [KeyConfig.RunForward, KeyConfig.RunLeft, KeyConfig.RunBack, KeyConfig.RunRight],
        ["<?kgDash?>"] = [KeyConfig.Dash], ["<?kgJump?>"] = [KeyConfig.Jump], ["<?kgLock?>"] = [KeyConfig.CameraReset],
        ["<?kgAttackL?>"] = [KeyConfig.AttackL], ["<?kgAttackR?>"] = [KeyConfig.AttackR], ["<?kgArts?>"] = [KeyConfig.StrongL],
        ["<?kgStrongAttackR?>"] = [KeyConfig.StrongR], ["<?kgChangeEquStyle?>"] = [KeyConfig.TwoHand],
        ["<?kgChangeWeaponBoth?>"] = [KeyConfig.ChangeLeft, KeyConfig.ChangeRight], ["<?kgChangeWeaponL?>"] = [KeyConfig.ChangeLeft],
        ["<?kgChangeWeaponR?>"] = [KeyConfig.ChangeRight], ["<?kgChangeMagic?>"] = [KeyConfig.ChangeMagic],
        ["<?kgUseItem?>"] = [KeyConfig.UseItem], ["<?kgChangeItem?>"] = [KeyConfig.ChangeItem],
        ["<?kgStartAction?>"] = [KeyConfig.Interact], ["<?kgPrecisionFireMode?>"] = [KeyConfig.AttackL],
        ["<?kgZoomIn?>"] = [KeyConfig.ChangeMagic], ["<?kgZoomOut?>"] = [KeyConfig.ChangeItem],
        ["<?kgGesture?>"] = [KeyConfig.Gesture], ["<?kgMenu?>"] = [KeyConfig.Menu],
    };

    [UnmanagedCallersOnly]
    static nint ReplaceTagHook(nint source, nint output, nint tag, nint markup)
    {
        if (Config.Enabled)
            try
            {
                string t = new((char*)tag);
                Part[][]? prompt = t == "<?kgCamera?>" ? [[KeyConfig.MouseMove()]]
                    : TutorialTags.TryGetValue(t, out var entries) ? Prompt(entries) : null;
                if (prompt != null) markup = Labels.Markup(prompt);
                if (Config.Diagnostics) // which tags the game replaces, and with what
                {
                    string m = markup == 0 ? "(null)" : new string((char*)markup);
                    Loader.LogOnce($"tag {t} -> {(m.Length > 160 ? m[..160] + "..." : m)}");
                }
            }
            catch (Exception e) { Loader.LogOnce("ReplaceTag: " + e.Message); }
        return s_replaceTag(source, output, tag, markup);
    }

    // ------------------------------------------------------------------ labels

    /// The bound parts of several entries (a stick or the d-pad has 2-4); null when none is bound.
    static Part[][]? Prompt(int[] entries)
    {
        var parts = entries.Select(e => e == MouseMoveEntry ? [KeyConfig.MouseMove()] : KeyConfig.Parts(e)).OfType<Part[]>()
            .DistinctBy(p => string.Join(",", p.Select(x => x.Id))).ToArray();
        return parts.Length == 0 ? null : parts;
    }

    static void DetectLanguage(nint text)
    {
        s_languageChecks++;
        for (char* c = (char*)text; *c != 0; c++)
            if (*c is >= 'А' and <= 'я')
            {
                KeyConfig.Russian = true;
                s_languageChecks = int.MaxValue;
                return;
            }
    }
}
