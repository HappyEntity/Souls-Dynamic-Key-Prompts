using System.Text.RegularExpressions;

namespace DynamicKeyPrompts;

/// The mod folders of ModEngine2, when the game was started by it: its launcher passes the path of its
/// config (config_darksouls3.toml) in MODENGINE_CONFIG.
static class ModEngine
{
    /// The file that replaces a game file (path like "menu/05_dummy.tpf.dcx") in the first enabled
    /// ModEngine2 mod that has it, or null.
    public static string? FindModFile(string relativePath)
    {
        try
        {
            foreach (string dir in ModFolders())
            {
                string file = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(file)) return file;
            }
        }
        catch (Exception e) { Loader.Log("modengine: reading its config failed: " + e.Message); }
        return null;
    }

    // [extension.mod_loader]
    // enabled = true
    // mods = [ { enabled = true, name = "default", path = "mod" }, ... ]    (path relative to the config)
    static IEnumerable<string> ModFolders()
    {
        string? config = Environment.GetEnvironmentVariable("MODENGINE_CONFIG");
        if (string.IsNullOrEmpty(config) || !File.Exists(config)) yield break;
        string text = File.ReadAllText(config);
        var section = Regex.Match(text, @"^\[extension\.mod_loader\][^\n]*\n(.*?)(?=^\[|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        if (!section.Success || Regex.IsMatch(section.Groups[1].Value, @"^\s*enabled\s*=\s*false", RegexOptions.Multiline)) yield break;

        string baseDir = Path.GetDirectoryName(Path.GetFullPath(config))!;
        foreach (Match mod in Regex.Matches(section.Groups[1].Value, @"\{([^{}]*)\}"))
        {
            string fields = mod.Groups[1].Value;
            if (Regex.IsMatch(fields, @"\benabled\s*=\s*false")) continue;
            var path = Regex.Match(fields, @"\bpath\s*=\s*(?:'([^']*)'|""((?:[^""\\]|\\.)*)"")");
            if (!path.Success) continue;
            string value = path.Groups[1].Success ? path.Groups[1].Value : Regex.Unescape(path.Groups[2].Value);
            yield return Path.GetFullPath(Path.Combine(baseDir, value));
        }
    }
}
