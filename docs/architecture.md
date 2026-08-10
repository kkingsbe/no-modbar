# NO Mod Bar — Architecture

## Overview

`NoModBar.dll` is a standalone BepInEx 5 plugin. It renders a ScreenSpaceOverlay
uGUI bar (sortingOrder 1000) in the top-left corner and toggles consumer-mod
panels on click. Consumer mods integrate through a reflection bridge and never
reference the bar assembly at compile time.

## Components

- `Plugin` — BepInEx entry; binds `Bar` config; spawns `ModBarController`.
- `ModBarApi` (public) — static, lock-protected registry keyed by mod `Id`.
  `Register(object)` reads the frozen contract properties via reflection, so the
  argument can be an anonymous object from any consumer assembly. A `_version`
  counter is bumped on every mutation.
- `ModBarController` — MonoBehaviour; every `Update()`:
  1. Gates bar visibility on `RequireInGame` and `GameManager.GetLocalAircraft`.
  2. Applies live config offsets.
  3. Rebuilds buttons when `ModBarApi.Version` changes.
  4. Repaints button active states from each entry's `IsVisible`.
- `ModBarCanvas` — builds the bar strip (collapsible via a `<<`/`>>` button),
  one text-labeled button per entry, and a hover tooltip. Buttons are the only
  raycast targets, so the bar does not block clicks elsewhere.

## Data flow

```
consumer mod Start() --reflection--> ModBarApi.Register(entry)
                                          |
                                          v
                          static registry (thread-safe dict, version++)
                                          |
ModBarController.Update() <--snapshot--  v
        | rebuild when version changed
        v
   ModBarCanvas buttons --click--> entry.Toggle()
        | frame poll
        v
   active highlight from entry.IsVisible()
```

## Deployment

- Debug builds copy `NoModBar.dll` to `BepInEx/scripts/`; `deploy.ps1` removes any
  stale `BepInEx/plugins/` copy so ScriptEngine is the single loader (F6 hot reload).
- Release builds produce a dist ZIP that mirrors the standard install layout
  (`BepInEx/plugins/NoModBar.dll`) for end users without ScriptEngine.
- Assembly simple name `NoModBar` resolves for `Type.GetType` from any consumer mod
  once it is loaded by ScriptEngine; `NoModBar.dll` sorts first alphabetically in
  `scripts/`, so it loads before `NOVor.dll`/`Sitrep.dll`.

## Limitations

- The bar only receives pointer input while the OS cursor is unlocked; during
  locked-cursor flight it renders passively. Opening any mod panel unlocks the
  cursor (existing behavior), making the bar clickable.
- Hot-reloading the bar mod alone orphans existing registrations until the
  consumer mods re-register (reload them too, or restart the game).
- The bar shows even when it has no registered mods (a lone collapse button).
