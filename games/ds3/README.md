# Dynamic Key Prompts — Dark Souls III

[← All games](../../README.md)

Dark Souls III always shows Xbox buttons in its prompts, even when you play with keyboard and mouse.
This mod shows the keys and mouse buttons **you bound** instead — in menus, the help bar, interaction
prompts ("Rest at bonfire"), messages and tutorial texts — and follows your changes in the game's Key
Bindings screen.

**Download:** [GitHub Releases](https://github.com/HappyEntity/Souls-Dynamic-Key-Prompts/releases) ·
[Changelog](../../CHANGELOG.md)

- Key and mouse icons in the style of the game's button icons (themes `dark`, `minimal`, `silver`), or plain text labels
- Context aware: menu buttons resolve to menu actions (Confirm, Cancel, tabs), world prompts to game actions (Interact, Roll, …)
- Mouse movement shown for the camera, Shift combinations shown as Shift + key
- The game's files are not modified; delete one DLL to uninstall
- Works together with **Seamless Co-op** and **ModEngine2**

## Requirements

- Dark Souls III, Steam, current version (1.15.2)
- Windows 10/11 x64

## Installation

1. Open the game folder: Steam → Dark Souls III → Manage → Browse local files → `Game`.
2. Copy `dinput8.dll` and the `DynamicKeyPrompts` folder there (next to `DarkSoulsIII.exe`).
3. Start the game as usual — through `ds3sc_launcher.exe` for Seamless Co-op, through
   `launchmod_darksouls3.bat` for ModEngine2.

Uninstall: delete `dinput8.dll` and the `DynamicKeyPrompts` folder.

> If another mod already uses `dinput8.dll`, use the alternative loader `xinput1_3.dll`
> (separate download, the same file as for Dark Souls II).

### With ModEngine2

The mod does not have to be in the game folder: put `dinput8.dll` and the `DynamicKeyPrompts` folder
into any folder, e.g. `ModEngine2\dkp\`, and list the DLL in `config_darksouls3.toml`:

```toml
external_dlls = [
    'C:\...\ModEngine2\dkp\dinput8.dll',
]
```

The DLL may be renamed (e.g. `DynamicKeyPrompts-loader.dll`). List the loader, not
`DynamicKeyPrompts\DynamicKeyPrompts.dll`.

If another ModEngine2 mod replaces `menu\05_Dummy.tpf.dcx`, the key icons are added to that mod's
version of the file, so both work.

## Settings

`DynamicKeyPrompts\DynamicKeyPrompts.ini` (changes apply on the next start):

| Setting | Values | |
|---|---|---|
| `Style` | `icons` / `text` | Key icons or text labels such as `[E]` |
| `IconTheme` | `dark` / `minimal` / `silver` | Look of the icons |
| `Labels` | `auto` / `mouse` / `both` | Which binding to show when an action has a key and a mouse button |
| `Format`, `Separator`, `Color` | | Text style options |

## Playing online

The mod only changes what you see and sends nothing over the network.

- **Seamless Co-op:** safe. Seamless uses its own network and doesn't connect to FromSoftware's servers.
- **Official online:** like most DLL mods it hooks game code and disables the game's Arxan
  anti-tamper, so there is no 100% guarantee. Use at your own risk.

## Troubleshooting

`DynamicKeyPrompts\DynamicKeyPrompts.log` tells what the mod did; `Diagnostics=1` makes it more
detailed. If icons are missing, `Style=text` shows whether the mod itself works. Please attach the log
when [reporting a problem](https://github.com/HappyEntity/Souls-Dynamic-Key-Prompts/issues).

## How it works

- The loader is the same as for Dark Souls II: it neuters Arxan with [dearxan](https://github.com/tremwil/dearxan)
  and loads the main module. Under Seamless Co-op and ModEngine2 it skips dearxan and starts the main
  module once the game's own code runs.
- Dark Souls III draws button icons as Scaleform images (`<img src='img://KG_…'>`) whose names come
  from the game's messages. The mod hooks that lookup, finds out which action the button stands for
  and returns the icon (or text) of the key bound to it. The bindings are read live from the game's
  key config.
- The key icons are added to a copy of one of the game's menu texture files
  (`menu\05_Dummy.tpf.dcx`, in `DynamicKeyPrompts\cache`), which the game reads instead of the original
  — the same way ModEngine2 loads replaced files.

---

## Кратко по-русски

Мод показывает в подсказках Dark Souls III клавиши и кнопки мыши, которые **вы назначили**, вместо
кнопок геймпада. Установка: скопировать `dinput8.dll` и папку `DynamicKeyPrompts` в папку `Game`
рядом с `DarkSoulsIII.exe`. Настройки — в `DynamicKeyPrompts\DynamicKeyPrompts.ini`. Совместим с
Seamless Co-op и ModEngine2. Файлы игры не изменяются.
