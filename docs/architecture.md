# NO Mod Bar — Architecture

## Overview

The bar ships as **two assemblies** so it is both hot-reloadable and reliable.

- `NoModBar.Core.dll` (in `BepInEx/plugins/`, loaded once by the Chainloader) owns
  the **static registry** and the **registration API**. Because it is never
  hot-reloaded, its state survives every ScriptEngine reload of the mods and the
  bar UI.
- `NoModBar.dll` (in `BepInEx/scripts/`, loaded by ScriptEngine) is the **bar UI
  plugin**: `Plugin` (config + entry), `ModBarController` (frame loop), and
  `ModBarCanvas` (the uGUI strip). It reads from the stable core registry, so a
  hot reload of the UI immediately re-renders whatever is registered.

Consumer mods integrate through a reflection bridge and never reference either
bar assembly at compile time.

## Components

- `NoModBar.Core.ModBarApi` (public) — lock-protected static registry keyed by mod
  `Id`. `Register(object)` reads the frozen contract properties via reflection, so
  the argument can be an anonymous object from any consumer assembly. A `_version`
  counter is bumped on every mutation. Logging is wired through `LogInfo` /
  `LogWarning` delegates set by the plugin.
- `NoModBar.Plugin` — BepInEx entry; binds `Bar` config; sets core logging;
  spawns `ModBarController`.
- `NoModBar.ModBarController` — MonoBehaviour; every `Update()`:
  1. Keeps the persistent bar available across every game screen.
  2. Applies live config offsets.
  3. Rebuilds buttons when `ModBarApi.Version` changes.
  4. Repaints button active states from each entry's `IsVisible`.
- `NoModBar.ModBarCanvas` — builds the bar strip (collapsible via a `<<`/`>>`
  button) anchored at the screen's top-center, one text-labeled button per entry,
  and a hover tooltip. Buttons are the only raycast targets, so the bar does not
  block clicks elsewhere.
- `NoModBar.CursorOverride` — owns the mod's private bits in the game's static
  `CursorManager` flag set (`1 << 20` free-cursor, `1 << 21` mod UI; the game uses
  bits 0–8). Set/clear goes through `CursorManager.SetFlag` + `Refresh()`, so the
  game's own cursor logic applies our state alongside its own flags.
- `NoModBar.FreeCursor` — polls the `Cursor > FreeCursorKey` shortcut each frame;
  while held in game it sets the free-cursor flag (cursor unlocked/visible),
  releasing clears it and the game re-locks through its normal path.
- `NoModBar.ModSettingsPanel` — the mod's own settings panel, registered on the
  bar as `CFG` (dogfoods the registry). Hosts the free-cursor hotkey with a
  click-to-rebind capture (Esc cancels); holds the mod-ui cursor flag while open
  so it stays clickable. Its canvas sorts above the bar (1010 vs 1000).

## Data flow

```
consumer mod Start() --reflection--> NoModBar.Core.ModBarApi.Register(entry)
                                          |
                                          v
                    stable static registry (thread-safe dict, version++)
                                          |
NoModBar.ModBarController.Update() <--snapshot-- v
        | rebuild when version changed
        v
   ModBarCanvas buttons --click--> entry.Toggle()
        | frame poll
        v
   active highlight from entry.IsVisible()
```

## Deployment

- Debug builds copy `NoModBar.dll` to `BepInEx/scripts/` (ScriptEngine, F6 hot
  reload) and `NoModBar.Core.dll` to `BepInEx/plugins/` (Chainloader, stable).
  `deploy.ps1` also removes stale copies so each assembly has a single loader.
- Release builds produce a dist ZIP with both DLLs flat at the archive root (a
  NOMM-compatible layout), for end users who do not use ScriptEngine.
- Consumer mods' bridges resolve `NoModBar.Core` by scanning
  `AppDomain.CurrentDomain.GetAssemblies()`, which works for any load order and
  across ScriptEngine reloads because the core assembly is stable.

## Limitations

- The bar receives pointer input whenever the OS cursor is unlocked: while the
  free-cursor hotkey is held (default `LeftAlt`), while the CFG panel or any mod
  panel is open, or while a game menu is up. During locked-cursor flight it
  renders passively.
- Changes to `NoModBar.Core.dll` require a game restart (it is not hot-reloadable
  by design). The bar UI in `NoModBar.dll` hot-reloads freely.
- The bar shows even when it has no registered mods (a lone collapse button).
- If a future game update claims `CursorFlags` bits `1 << 20` / `1 << 21`, the
  cursor override flags must move (single constants in `CursorOverride`).
