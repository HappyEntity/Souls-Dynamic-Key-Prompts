using System.Runtime.InteropServices;
using DynamicKeyPrompts.Icons;

namespace DynamicKeyPrompts;

/// Serves the game a copy of its font with keycap glyphs added, without touching the game's
/// files: CreateFileW / GetFileAttributesExW in the exe's import table are redirected for
/// font\<language>\FeFont_{Small,Big}.fontbnd.dcx to DynamicKeyPrompts\cache\<language>\….
/// The copy is (re)built from whatever font is installed, so other font mods keep working.
static unsafe class FontRedirect
{
    static delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint> s_createFileW;
    static delegate* unmanaged<char*, int, void*, int> s_getFileAttributesExW;
    static readonly object s_lock = new();
    static readonly Dictionary<string, string?> s_redirects = new(StringComparer.OrdinalIgnoreCase);

    /// True once the game has been given a patched font: keycap glyphs can be used in text.
    public static bool IconsActive { get; private set; }

    public static void Install()
    {
        nint cf = Iat.Patch(Loader.GameBase, "kernel32.dll", "CreateFileW", (nint)(delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint>)&CreateFileWDetour);
        nint ga = Iat.Patch(Loader.GameBase, "kernel32.dll", "GetFileAttributesExW", (nint)(delegate* unmanaged<char*, int, void*, int>)&GetFileAttributesExWDetour);
        s_createFileW = (delegate* unmanaged<char*, uint, uint, nint, uint, uint, nint, nint>)cf;
        s_getFileAttributesExW = (delegate* unmanaged<char*, int, void*, int>)ga;
        Loader.Log($"font: import hooks CreateFileW={(cf != 0 ? "ok" : "FAILED")} GetFileAttributesExW={(ga != 0 ? "ok" : "FAILED")}");
    }

    [UnmanagedCallersOnly]
    static nint CreateFileWDetour(char* name, uint access, uint share, nint sa, uint disposition, uint flags, nint template)
    {
        string? target = Redirect(name);
        if (target == null) return s_createFileW(name, access, share, sa, disposition, flags, template);
        fixed (char* p = target) return s_createFileW(p, access, share, sa, disposition, flags, template);
    }

    [UnmanagedCallersOnly]
    static int GetFileAttributesExWDetour(char* name, int level, void* info)
    {
        string? target = Redirect(name);
        if (target == null) return s_getFileAttributesExW(name, level, info);
        fixed (char* p = target) return s_getFileAttributesExW(p, level, info);
    }

    static string? Redirect(char* name)
    {
        if (name == null || Config.Style != LabelStyle.Icons) return null;
        // Cheap filter before allocating: only paths ending in ".fontbnd.dcx".
        int len = 0;
        while (name[len] != 0) len++;
        if (len < 12 || !new ReadOnlySpan<char>(name + len - 12, 12).Equals(".fontbnd.dcx", StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            string path = new string(name, 0, len).Replace('/', '\\');
            lock (s_lock)
            {
                if (!s_redirects.TryGetValue(path, out string? target))
                    s_redirects[path] = target = Build(path);
                return target;
            }
        }
        catch (Exception e)
        {
            Loader.Log($"font: redirect failed: {e.Message}");
            return null;
        }
    }

    static string? Build(string path)
    {
        string file = Path.GetFileName(path);
        if (!file.StartsWith("FeFont_", StringComparison.OrdinalIgnoreCase)) return null;
        string full = Path.GetFullPath(path);
        string? lang = Path.GetFileName(Path.GetDirectoryName(full));
        if (lang == null || !File.Exists(full)) return null;

        string cacheDir = Path.Combine(Loader.ModDir, "cache", lang);
        string cached = Path.Combine(cacheDir, file);
        var src = new FileInfo(full);
        string stampFile = cached + ".stamp";
        try
        {
            var (sheetPng, sheetTxt) = IconSheets.Files(Config.IconTheme);
            string stamp = $"v{KeycapSet.Version} {Config.IconTheme} {src.Length} {src.LastWriteTimeUtc.Ticks} " +
                           $"{IconSheets.Fingerprint(sheetPng)} {IconSheets.Fingerprint(sheetTxt)}";
            if (!File.Exists(cached) || !File.Exists(stampFile) || File.ReadAllText(stampFile) != stamp)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                KeycapRenderer.T = KeycapRenderer.ThemeByName(Config.IconTheme);
                var result = FontPatcher.Patch(File.ReadAllBytes(full), IconSheets.Load(Config.IconTheme));
                Directory.CreateDirectory(cacheDir);
                File.WriteAllBytes(cached, result.FontBndDcx);
                File.WriteAllText(stampFile, stamp);
                Loader.Log($"font: built {cached} in {sw.ElapsedMilliseconds} ms ({result.Log})");
            }
            else
                Loader.Log($"font: using cached {cached}");
        }
        catch (Exception e)
        {
            Loader.Log($"font: cannot build keycap font from {full}: {e.Message} - using text labels");
            return null;
        }

        if (!IconsActive)
        {
            IconsActive = true;
            Bindings.Invalidate(); // re-render texts produced before the font was swapped
        }
        return cached;
    }
}
