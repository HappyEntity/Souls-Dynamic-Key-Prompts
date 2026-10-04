namespace DynamicKeyPrompts;

/// Offsets in DarkSoulsIII.exe 1.15.2 (Steam).
static class Rva
{
    public const uint ExpectedTimeDateStamp = 0x639C4DDD;

    /// Static pointer to the CSPcKeyConfig singleton.
    public const int KeyConfig = 0x47571D0;

    /// MsgRepository lookup: (repository, 0, category, id) -> wchar* or null.
    public const int GetMsg = 0x2354090;

    /// Replaces one tutorial tag (L"<?kgAttackR?>") in a message: (source, out, tag, replacement markup).
    public const int ReplaceTag = 0xA67740;

    /// Return address inside the builder of L"<img src='img://%s' vspace='-8'>" after its icon name lookup.
    public const int MarkupBuilderReturn = 0xA675DC;

    /// Registry of the menu tags (conclusion, cancel, ...): button functions called from here are menu buttons.
    public const int MenuTagsBegin = 0xAC1000, MenuTagsEnd = 0xAC3000;

    /// Return address in the builder of the action list shown at objects ("Rest at bonfire" / "Switch action"):
    /// the menu tags it formats mean game actions (Switch action = two-hand), not menu keys.
    public const int ActionListPrompt = 0xA66233;

    /// Return addresses in the Key Bindings screen that draw the Controller column's button icons
    /// (first page, selected row, pages after switching tabs).
    public const int KeyBindingsPadIcon1 = 0xA41A25, KeyBindingsPadIcon2 = 0xABA8AE, KeyBindingsPadIcon3 = 0xABDCA8;

    /// DLString replace, used by the virtual file system to turn "menu:/x" into "data1:/menu/x"
    /// (ModEngine2's virtual_to_archive_path for Dark Souls III).
    public const int PathReplace = 0x7D660;

    /// A call ModEngine2 removes so that files replaced by loose files load in Dark Souls III.
    public const int LooseFileCheckCall = 0xEA1B83;
}
