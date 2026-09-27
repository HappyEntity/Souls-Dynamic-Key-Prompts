using System.Runtime.InteropServices;
using DynamicKeyPrompts.Formats;
using DynamicKeyPrompts.Icons;

namespace DynamicKeyPrompts;

/// Key icon textures for Style=icons.
///
/// Scaleform finds img:// images among the textures of the menu TPFs the game has loaded. The icons are
/// therefore added to a copy of menu/05_Dummy.tpf.dcx (placeholder textures, loaded for every menu) kept in
/// DynamicKeyPrompts\cache\ds3, and the game is made to read that copy: like ModEngine2, the archive path
/// "data1:/menu/05_Dummy.tpf.dcx" produced by the virtual file system is turned into a local path
/// (".//////menu/...") and CreateFileW opens the cache file for it. Until the game has opened the copy,
/// prompts fall back to text. When another ModEngine2 mod replaces the file, the copy is made from that
/// mod's version, so its textures stay.
static unsafe class KeyIcons
{
    const int Scale = 2;          // textures are drawn at twice the size they are shown at
    const int ShownHeight = 32;   // like the game's KG_* button icons
    const int TopPadding = 4;
    // Height the images are shown at: with 29 px or more the text line grows and the prompt text (and
    // pop-up menus built from it) moves down, unlike with the game's own 32 px icons.
    const int LineHeight = 28;
    const string Carrier = "menu/05_dummy.tpf.dcx";
    const int StampVersion = 2;

    static string s_cacheFile = "";
    static readonly Dictionary<string, (string Name, int W, int H)> s_icons = new();
    static readonly ManualResetEventSlim s_built = new(false);
    static volatile bool s_ready, s_opened, s_failed;
    [ThreadStatic] static bool t_building;

    static delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint> s_replace;
    static delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint> s_createFile;

    public static void Install()
    {
        string dir = Path.Combine(Loader.ModDir, "cache", "ds3", "menu");
        Directory.CreateDirectory(dir);
        s_cacheFile = Path.Combine(dir, "05_dummy.tpf.dcx");
        KeycapRenderer.T = KeycapRenderer.ThemeByName(Config.IconTheme);

        // The game opens the carrier during start-up: build the copy right away, CreateFileW waits for it.
        new Thread(Build) { IsBackground = true, Name = "DKP icons" }.Start();

        s_replace = (delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint>)Loader.Hook("PathReplace", Loader.GameBase + Rva.PathReplace,
            (nint)(delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint>)&ReplaceHook);
        nint createFile = Native.Export("kernelbase.dll", "CreateFileW");
        s_createFile = (delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint>)Loader.Hook("CreateFileW", createFile,
            (nint)(delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint>)&CreateFileHook);
        PatchLooseFileCheck();
    }

    /// Texture name and shown size of a part's icon, or null (text is shown instead).
    public static (string Name, int W, int H)? Get(string id)
    {
        if (!s_ready || !s_opened) return null;
        lock (s_icons) return s_icons.TryGetValue(id, out var icon) ? icon : null;
    }

    // ------------------------------------------------------------------ the copy of the carrier TPF

    static IEnumerable<(string Id, KeycapSpec Spec)> AllIcons()
    {
        foreach (var e in KeycapSet.All())
            if (e.Spec is KeycapSpec.Key) yield return ($"K{e.Code}", e.Spec);
        foreach (var (id, spec) in KeyConfig.MouseIcons()) yield return (id, spec);
    }

    static void Build()
    {
        try
        {
            var icons = new List<(string Id, string Name, RgbaImage Image, int W, int H)>();
            foreach (var (id, spec) in AllIcons())
            {
                // The keycap is a little smaller than the line and sits lower, like the game's round
                // button icons inside their 32x32 textures: transparent rows above it.
                var img = KeycapRenderer.Render(spec, (ShownHeight - TopPadding) * Scale);
                int w = (img.Width + 3) & ~3, h = (img.Height + TopPadding * Scale + 3) & ~3;
                icons.Add((id, "DKP_" + id, Pad(img, w, h, TopPadding * Scale), w * LineHeight / h, LineHeight));
            }
            lock (s_icons) foreach (var i in icons) s_icons[i.Id] = (i.Name, i.W, i.H);

            // Another ModEngine2 mod may replace the carrier: the icons are then added to its version.
            string? loose = ModEngine.FindModFile(Carrier);
            string source = loose == null ? "data1" : $"{loose} {new FileInfo(loose).Length} {File.GetLastWriteTimeUtc(loose).Ticks}";
            string stamp = $"{StampVersion} {Config.IconTheme} {icons.Count} {source}";
            string stampFile = s_cacheFile + ".stamp";
            if (!File.Exists(s_cacheFile) || !File.Exists(stampFile) || File.ReadAllText(stampFile) != stamp)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                byte[] original;
                if (loose != null)
                {
                    t_building = true; // our own read of the mod's file must not be redirected to the cache
                    try { original = File.ReadAllBytes(loose); }
                    finally { t_building = false; }
                }
                else
                {
                    // The game folder, not the mod's (the loader may live elsewhere, e.g. with ModEngine2)
                    string game = Path.GetDirectoryName(Environment.ProcessPath)!;
                    var archive = new Bhd5(Path.Combine(game, "Data1.bhd"), ArchiveKeys.Data1, Path.Combine(game, "Data1.bdt"), ds3: true);
                    original = archive.Read("/" + Carrier) ?? throw new FileNotFoundException(Carrier + " not in Data1");
                }
                byte[] tpf = Tpf.AddTextures(Dcx.Decompress(original),
                    icons.Select(i => (i.Name, Dds.EncodeDxt5(i.Image.Width, i.Image.Height, i.Image.Rgba))).ToList());
                File.WriteAllBytes(s_cacheFile, Dcx.Compress(tpf));
                File.WriteAllText(stampFile, stamp);
                Loader.Log($"icons: built {s_cacheFile} from {loose ?? "Data1"} with {icons.Count} icons in {sw.ElapsedMilliseconds} ms");
            }
            else Loader.Log($"icons: using cached {s_cacheFile} (from {loose ?? "Data1"})");
            s_ready = true;
        }
        catch (Exception e) { s_failed = true; Loader.Log("icons: build failed, showing text: " + e); }
        finally { s_built.Set(); }
    }

    static RgbaImage Pad(RgbaImage img, int w, int h, int top)
    {
        var rgba = new byte[w * h * 4];
        for (int y = 0; y < img.Height; y++) Array.Copy(img.Rgba, y * img.Width * 4, rgba, (y + top) * w * 4, img.Width * 4);
        return new RgbaImage(w, h, rgba);
    }

    // ------------------------------------------------------------------ making the game read it

    // DLString replace (the virtual file system expands "menu:/..." to "data1:/menu/..." with it).
    // Returns the result string: {wchar_t* or inline buffer; ?; length; capacity}.
    [UnmanagedCallersOnly]
    static nint ReplaceHook(nint str, nint a2, nint a3, nint a4, nint a5, nint a6)
    {
        nint r = s_replace(str, a2, a3, a4, a5, a6);
        try
        {
            if (r != 0 && !s_failed)
            {
                long length = *(long*)(r + 0x10), capacity = *(long*)(r + 0x18);
                char* s = capacity >= 8 ? *(char**)r : (char*)r;
                const string archivePath = "data1:/" + Carrier;
                if (length == archivePath.Length && new string(s, 0, (int)length).Equals(archivePath, StringComparison.OrdinalIgnoreCase))
                {
                    // "data1:" -> "./////" : the game loads it from the game folder, CreateFileW redirects it
                    s[0] = '.';
                    for (int i = 1; i < 6; i++) s[i] = '/';
                    Loader.LogOnce("icons: redirecting " + archivePath);
                }
            }
        }
        catch { }
        return r;
    }

    [UnmanagedCallersOnly]
    static nint CreateFileHook(char* name, uint access, uint share, nint security, uint disposition, uint flags, nint template)
    {
        if (name != null && !t_building)
            try
            {
                var path = new ReadOnlySpan<char>(name, Native.WcsLen(name));
                // The game may make the local path absolute: match "...menu[/\]05_dummy.tpf.dcx" outside our folder
                if (path.EndsWith("05_dummy.tpf.dcx", StringComparison.OrdinalIgnoreCase)
                    && path[..^16].TrimEnd("/\\").EndsWith("menu", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("DynamicKeyPrompts", StringComparison.OrdinalIgnoreCase))
                {
                    s_built.Wait(TimeSpan.FromSeconds(20));
                    fixed (char* f = s_cacheFile)
                    {
                        nint h = s_createFile(f, access, share, security, disposition, flags, template);
                        Loader.LogOnce($"icons: game opens {path} -> {s_cacheFile}: {(h != -1 ? "ok" : $"failed ({Marshal.GetLastSystemError()})")}");
                        if (h != -1) { s_opened = true; return h; }
                    }
                }
                else if (Config.Diagnostics && (path.Contains("05_dummy", StringComparison.OrdinalIgnoreCase) || path.Contains("/////", StringComparison.Ordinal)))
                    Loader.LogOnce($"file: {path}");
            }
            catch { }
        return s_createFile(name, access, share, security, disposition, flags, template);
    }

    // ModEngine2 removes this call for loose files in Dark Souls III.
    static void PatchLooseFileCheck()
    {
        byte* p = (byte*)(Loader.GameBase + Rva.LooseFileCheckCall);
        if (p[0] == 0x90) return; // already done (ModEngine2)
        if (p[0] != 0xE8) { Loader.Log("icons: loose file patch site not found"); return; }
        Native.Patch((nint)p, [0x90, 0x90, 0x90, 0x90, 0x90]);
    }
}
