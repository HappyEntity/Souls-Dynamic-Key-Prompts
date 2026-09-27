namespace DynamicKeyPrompts.Icons;

/// Text names of the key codes KeycapSet uses (DirectInput scan code + 0x45, extended keys packed after it;
/// Dark Souls II and III share them) and of mouse buttons, for Style=text and for games without own names.
public static class KeyCodeNames
{
    const string Qwerty = "QWERTYUIOP", Asdf = "ASDFGHJKL", Zxcv = "ZXCVBNM";
    static readonly string[] NumPad = ["7", "8", "9", "-", "4", "5", "6", "+", "1", "2", "3", "0", "."];

    public static string Key(int code)
    {
        switch (code)
        {
            case 175: return "NumEnter";
            case 176: return "RCtrl";
            case 185: return "Num/";
            case 187: return "AltGr";
            case 189: return "Home";
            case 190: return "↑";
            case 191: return "PgUp";
            case 192: return "←";
            case 193: return "→";
            case 194: return "End";
            case 195: return "↓";
            case 196: return "PgDn";
            case 197: return "Ins";
            case 198: return "Del";
        }
        int dik = code - 0x45, i;
        if ((i = dik - 0x10) is >= 0 and < 10) return Qwerty[i].ToString();
        if ((i = dik - 0x1E) is >= 0 and < 9) return Asdf[i].ToString();
        if ((i = dik - 0x2C) is >= 0 and < 7) return Zxcv[i].ToString();
        if (dik is >= 2 and <= 0x0A) return ((char)('1' + dik - 2)).ToString();
        if (dik == 0x0B) return "0";
        if (dik is >= 0x3B and <= 0x44) return "F" + (dik - 0x3A);
        if (dik is 0x57 or 0x58) return "F" + (dik - 0x57 + 11);
        if (dik is >= 0x47 and <= 0x53) return "Num" + NumPad[dik - 0x47];
        return dik switch
        {
            0x01 => "Esc", 0x0C => "-", 0x0D => "=", 0x0E => "Backspace", 0x0F => "Tab", 0x1A => "[", 0x1B => "]",
            0x1C => "Enter", 0x1D => "Ctrl", 0x27 => ";", 0x28 => "'", 0x29 => "`", 0x2A => "Shift", 0x2B => "\\",
            0x33 => ",", 0x34 => ".", 0x35 => "/", 0x36 => "RShift", 0x37 => "Num*", 0x38 => "Alt", 0x39 => "Space",
            0x3A => "CapsLock", 0x45 => "NumLock", 0x46 => "ScrollLock",
            _ => $"#{code:X}",
        };
    }

    public static string Mouse(KeycapSpec.Mouse m, bool russian)
    {
        string name = (m.Button, m.WheelDirection) switch
        {
            (_, > 0) => russian ? "Колесо↑" : "Wheel↑",
            (_, < 0) => russian ? "Колесо↓" : "Wheel↓",
            (MouseButton.Left, _) => russian ? "ЛКМ" : "LMB",
            (MouseButton.Right, _) => russian ? "ПКМ" : "RMB",
            _ => russian ? "СКМ" : "MMB",
        };
        return m.DoubleClick ? "2×" + name : name;
    }

    public static string MouseMovement(bool russian) => russian ? "Мышь" : "Mouse";
}
