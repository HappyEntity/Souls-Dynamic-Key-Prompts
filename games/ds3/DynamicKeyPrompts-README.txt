=====================================================================
  Dynamic Key Prompts  -  Dark Souls III
=====================================================================

Shows the keys and mouse buttons YOU bound in every button prompt,
instead of Xbox buttons: menus, the help bar at the bottom, "Rest at
bonfire" style prompts, messages and tutorial texts. Rebind a key in the
game's Key Bindings screen and the prompts follow.


REQUIREMENTS
------------
- Dark Souls III, Steam version 1.15.2 (the latest update)
- Windows 10 or 11 (64-bit)


INSTALLATION
------------
1. Find the game folder:
   Steam > Library > right-click "DARK SOULS III" > Manage >
   Browse local files > open the "Game" folder (the one that contains
   DarkSoulsIII.exe).

2. Copy into that folder:
     dinput8.dll
     DynamicKeyPrompts   (the whole folder)
   (this readme does not need to be copied)

   It should look like this:
     Game\DarkSoulsIII.exe
     Game\dinput8.dll
     Game\DynamicKeyPrompts\DynamicKeyPrompts.dll
     Game\DynamicKeyPrompts\DynamicKeyPrompts.ini

3. Start the game as usual (with Seamless Co-op: through ds3sc_launcher.exe;
   with ModEngine2: through launchmod_darksouls3.bat).


UNINSTALLING
------------
Delete dinput8.dll and the DynamicKeyPrompts folder from the Game folder.
The mod does not change any of the game's own files.


SETTINGS
--------
Open DynamicKeyPrompts\DynamicKeyPrompts.ini with Notepad. Changes apply
the next time you start the game.

  Style=icons        key and mouse icons (default)
  Style=text         key names as text, e.g. [E]

  IconTheme=dark     dark stone keys with a bronze rim (default)
  IconTheme=minimal  only a thin frame around the key name
  IconTheme=silver   light metal keys

  Labels=auto        the key if the action has one, else the mouse button
  Labels=mouse       the mouse button if the action has one
  Labels=both        both

The file itself describes every option.


COMPATIBILITY
-------------
Works together with:
  - Seamless Co-op (started through ds3sc_launcher.exe, or loaded by
    ModEngine2)
  - ModEngine2

With ModEngine2 the mod does not have to be in the Game folder: put
dinput8.dll and the DynamicKeyPrompts folder into any folder, e.g.
ModEngine2\dkp\, and list the DLL in config_darksouls3.toml:

  external_dlls = [
      'C:\...\ModEngine2\dkp\dinput8.dll',
  ]

(The DLL may be renamed, e.g. to DynamicKeyPrompts-loader.dll. List the
loader, not DynamicKeyPrompts\DynamicKeyPrompts.dll.)

If another ModEngine2 mod replaces menu\05_Dummy.tpf.dcx, the key icons are
added to that mod's version of the file, so both work.

A game unpacked with UXM or Nuxe works too: the key icons are then added to
the unpacked Game\menu\05_Dummy.tpf.dcx. So does BootBoost.


HOW IT WORKS
------------
The key icons are added to a copy of one of the game's menu texture files
(menu\05_Dummy.tpf.dcx), kept in DynamicKeyPrompts\cache\ds3 and read by
the game instead of the original. Nothing in the game folder is changed.

The mod loads through dinput8.dll. If another mod already uses dinput8.dll,
use the optional "xinput1_3 loader" instead.

If both names are taken, load the mod with Ultimate ASI Loader (or another
mod's DLL loader): rename the mod's dinput8.dll to DynamicKeyPrompts.asi and
keep the DynamicKeyPrompts folder next to it.


PLAYING ONLINE
--------------
The mod only changes what you see and sends nothing over the network, but
it hooks game code and disables the game's Arxan anti-tamper like most DLL
mods, so there is no 100% guarantee for FromSoftware's official servers.
Use it online at your own risk.


TROUBLESHOOTING
---------------
The mod writes DynamicKeyPrompts\DynamicKeyPrompts.log every time the
game starts.

- Prompts still show gamepad buttons: make sure dinput8.dll is next to
  DarkSoulsIII.exe and look at the log.
- Icons missing or broken: set Style=text in the ini to confirm the mod
  works, and report it with the log (Diagnostics=1 makes it more detailed).

Report problems on GitHub:
  https://github.com/HappyEntity/Souls-Dynamic-Key-Prompts/issues


CREDITS
-------
- dearxan by tremwil          https://github.com/tremwil/dearxan
- MinHook by Tsuda Kageyu      https://github.com/TsudaKageyu/minhook
- ModEngine2 (soulsmods), for showing how Dark Souls III loads files
- The Souls modding community for documenting the game's file formats

License: MIT, see DynamicKeyPrompts\LICENSE.
Third-party licenses: DynamicKeyPrompts\THIRD-PARTY-NOTICES.md


---------------------------------------------------------------------
  ПО-РУССКИ
---------------------------------------------------------------------
Мод показывает в подсказках клавиши и кнопки мыши, которые вы назначили
в игре, вместо кнопок геймпада.

Установка: скопируйте dinput8.dll и папку DynamicKeyPrompts в папку
Game игры (там, где DarkSoulsIII.exe).

Удаление: удалите dinput8.dll и папку DynamicKeyPrompts. Файлы игры мод
не изменяет.

Настройки: DynamicKeyPrompts\DynamicKeyPrompts.ini
  Style      - icons (значки) или text (текст вида [E])
  IconTheme  - dark, minimal, silver
  Labels     - auto, mouse, both (клавиша или кнопка мыши)

Онлайн: мод меняет только то, что видите вы, и ничего не отправляет по
сети, но на официальных серверах 100% гарантии, как и у любого DLL-мода,
нет.
