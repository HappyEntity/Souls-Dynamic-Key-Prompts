using DynamicKeyPrompts.Icons;

namespace DynamicKeyPrompts;

/// One key or mouse button of a prompt: its text name, its keycap drawing and a stable id for the icon.
sealed record Part(string Text, KeycapSpec Spec, string Id);

/// The player's bindings, read live from CSPcKeyConfig. Entries are 0x14 bytes:
/// { 0, pad button, key, modifier (3 = Shift), mouse button }. Key codes are those of Dark Souls II
/// (DirectInput scan code + 0x45, extended keys packed after), so KeycapSet draws them as is.
static unsafe class KeyConfig
{
    const int TableOffset = 0x380, EntrySize = 0x14;
    public const int EntryCount = 45;

    // Entry indices (order of the Key Bindings screen, see docs)
    public const int Walk = 0, RunForward = 1, RunBack = 2, RunLeft = 3, RunRight = 4, Dash = 5, Jump = 6,
        CameraReset = 13, ChangeMagic = 14, ChangeItem = 15, ChangeRight = 16, ChangeLeft = 17,
        AttackR = 18, StrongR = 19, AttackL = 20, StrongL = 21, UseItem = 22, Interact = 23, TwoHand = 24,
        Menu = 26, Gesture = 27, CursorUp = 28, CursorDown = 29, CursorRight = 30, CursorLeft = 31,
        Confirm = 32, Cancel = 33, TabLeft = 34, TabRight = 35, Function2 = 36, Function1 = 37,
        Sort = 41, Function3 = 42, ScrollUp = 43, ScrollDown = 44;

    const int ShiftCode = 111;
    static readonly Dictionary<int, KeycapSpec> s_keySpecs = KeycapSet.All().ToDictionary(e => e.Code, e => e.Spec);

    /// Mouse names in Russian; set from the game's texts (a Cyrillic string seen by the message hook).
    public static bool Russian { get; set; }

    /// The parts to show for an entry, following Labels= in the ini; null when nothing is bound.
    public static Part[]? Parts(int entry)
    {
        if (entry is < 0 or >= EntryCount) return null;
        nint obj = *(nint*)(Loader.GameBase + Rva.KeyConfig);
        if (obj == 0) return null;
        int* e = (int*)(obj + TableOffset + entry * EntrySize);
        int code = e[2], mod = e[3], mouse = e[4];

        Part[]? key = code > 0 ? (mod == 3 ? [KeyPart(ShiftCode), KeyPart(code)] : [KeyPart(code)]) : null;
        Part[]? mice = mouse > 0 ? MouseParts(mouse) : null;
        return Config.Labels switch
        {
            LabelMode.Keyboard => key ?? mice,
            LabelMode.Mouse => mice ?? key,
            LabelMode.Both => key != null && mice != null ? [.. key, .. mice] : key ?? mice,
            _ => key ?? mice,
        };
    }

    /// Every mouse icon a prompt can use (the Shift combinations are drawn as Shift + button).
    public static IEnumerable<(string Id, KeycapSpec Spec)> MouseIcons() =>
        new[] { 1, 2, 8, 9, 0xA }.Select(c => MousePart(c)).Append(MouseMove()).Select(p => (p.Id, p.Spec));

    public static Part MouseMove() => new(KeyCodeNames.MouseMovement(Russian), new KeycapSpec.MouseMove(), "MOVE");

    static Part KeyPart(int code) =>
        new(KeyCodeNames.Key(code), s_keySpecs.GetValueOrDefault(code) ?? new KeycapSpec.Key(KeyCodeNames.Key(code)), $"K{code}");

    static Part[] MouseParts(int code) => code switch
    {
        0x13 or 0x14 or 0x1B or 0x1C => [KeyPart(ShiftCode), MousePart(code switch { 0x13 => 1, 0x14 => 2, 0x1B => 9, _ => 0xA })],
        _ => [MousePart(code)],
    };

    static Part MousePart(int code)
    {
        var spec = code switch
        {
            1 => new KeycapSpec.Mouse(MouseButton.Right),
            2 => new KeycapSpec.Mouse(MouseButton.Left),
            9 => new KeycapSpec.Mouse(MouseButton.Middle, WheelDirection: +1),
            0xA => new KeycapSpec.Mouse(MouseButton.Middle, WheelDirection: -1),
            _ => new KeycapSpec.Mouse(MouseButton.Middle),
        };
        return new(KeyCodeNames.Mouse(spec, Russian), spec, $"M{code}");
    }
}
