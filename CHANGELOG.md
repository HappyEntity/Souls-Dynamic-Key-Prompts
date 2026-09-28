# Changelog

## Dark Souls III

### 0.1.2

- Key icons no longer push the prompt text down: help bar lines and pop-up menus with key icons now
  sit where they are with gamepad buttons.

### 0.1.1

- When another ModEngine2 mod replaces `menu\05_Dummy.tpf.dcx`, the key icons are added to that
  mod's version of the file instead of the game's, so the other mod's textures are kept.

### 0.1.0

First test version.

- Gamepad button prompts replaced with the keys and mouse buttons bound in the game, read live from its
  key config: menus, help bar, world prompts and tutorial texts
- Key and mouse icons (themes `dark`, `minimal`, `silver`) added to a copy of a menu texture file, or
  text labels (`Style=text`)
- Compatible with Seamless Co-op and ModEngine2 (also as an `external_dlls` entry)

## Dark Souls II: Scholar of the First Sin

### 1.0.2

- The mod can be loaded by Ultimate ASI Loader or another mod's DLL loader (rename dinput8.dll to
  DynamicKeyPrompts.asi), for setups where dinput8.dll and xinput1_3.dll are both taken. Loaded that
  way it starts without its own Arxan workaround, like under Seamless Co-op.

### 1.0.1

- Fixed the game closing right after start with Seamless Co-op on some systems (crash
  `0xC0000026` in `ntdll.dll`). When the game is started by Seamless Co-op's launcher the mod
  waits for Seamless to finish loading and starts without its own Arxan workaround (dearxan).
- The .NET runtime of the mod only handles faults inside the mod, not exceptions of the game
  or other mods.

### 1.0.0

First release.

- Gamepad button prompts replaced with the keys and mouse buttons bound in the game, updated live
- Menu and world prompts resolved separately (Confirm/Cancel vs Interact/Roll, …)
- Key and mouse icons drawn into a copy of the game font; themes `dark`, `minimal`, `silver`,
  editable sprite sheets and custom themes
- Text label mode (`Style=text`)
- Compatible with Seamless Co-op, DS2 Lighting Engine and OptiScaler
- Two loaders: `dinput8.dll` (default) and `xinput1_3.dll` (optional, for setups where
  dinput8.dll is taken by another mod)
