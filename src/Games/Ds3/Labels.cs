namespace DynamicKeyPrompts;

/// Turns the parts of a prompt into what the markup needs: key icon images (Style=icons) or text.
/// A prompt is a list of entries (one per bound action), each a list of parts (Shift + key, ...).
static class Labels
{
    /// Replacement for the icon name inside L"<img src='img://%s' vspace='-8'>".
    public static nint InImageSource(Part[][] prompt)
    {
        if (Icons(prompt) is { } icons)
        {
            // name' size vspace><img src='img://name' ... - the format closes the last image
            var s = string.Join(" vspace='-8'><img src='img://", icons.Select(i => $"{i.Name}' width='{i.W}' height='{i.H}'"));
            return Keep(s[..^1]);
        }
        // Close the image as an empty one, show the text, and let the rest of the format close a second one.
        return Keep($"{Empty}' width='0' height='0'>{Text(prompt)}<img src='img://{Empty}' width='0' height='0' alt='");
    }

    /// Complete markup for a tutorial tag.
    public static nint Markup(Part[][] prompt) =>
        Icons(prompt) is { } icons
            ? Keep(string.Concat(icons.Select(i => $"<img src='img://{i.Name}' width='{i.W}' height='{i.H}' vspace='-8'>")))
            : Keep(Text(prompt));

    const string Empty = "DKP_None";

    static List<(string Name, int W, int H)>? Icons(Part[][] prompt)
    {
        if (Config.Style != LabelStyle.Icons) return null;
        var list = new List<(string, int, int)>();
        foreach (var part in prompt.SelectMany(p => p))
        {
            if (KeyIcons.Get(part.Id) is not { } icon) return null;
            list.Add(icon);
        }
        return list;
    }

    /// "[E]", "[Shift+LMB]", "[WASD]", "[←/→]"
    public static string Text(Part[][] prompt)
    {
        var names = prompt.Select(p => string.Join("+", p.Select(x => x.Text))).Distinct().ToArray();
        string label = string.Join(names.All(n => n.Length == 1) ? "" : Config.Separator, names);
        string text = string.Format(Config.Format, Escape(label));
        return Config.Color.Length == 6 ? $"<font color='#{Config.Color}'>{text}</font>" : text;
    }

    /// The key alone, for the narrow text fields next to the menu tabs (about three characters wide):
    /// "Shift+←" is shown as "←" - the full combination is in the help bar below.
    public static nint TabText(Part[] parts) => Keep(parts[^1].Text);

    static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    static nint Keep(string s) => Native.PermanentString(s);
}
