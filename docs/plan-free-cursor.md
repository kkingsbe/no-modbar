# Plan — Hold-to-Free-Cursor Hotkey

## Goal

While a configurable hotkey is **held**, the OS cursor is unlocked and visible so the
pilot can click mod bar buttons (or any uGUI panel) without opening a menu. On
release, the cursor re-locks and mouse-look resumes.

## What we know about the game (verified by reflection recon, 2026-08-10)

Recon scripts: `docs/recon-cursor*.ps1` (ReflectionOnly scan of `Assembly-CSharp.dll`).

- **`CursorManager`** — `public static` class in Assembly-CSharp. Flag-based cursor
  state:
  - `static void SetFlag(CursorFlags flag, bool value)`
  - `static CursorFlags GetFlags()` / `static bool GetFlag(CursorFlags flag)`
  - `static void Refresh()` / `static void SetLockState()` — apply state to
    `Cursor.visible` / `Cursor.lockState`
  - `static bool Visible { get; }` — true when any flag set and not force-hidden
  - `static void ClearGameplayFlags()` — clears the game's own bits
  - Handles `Application_focusChanged` itself (re-hides on focus loss)
- **`CursorFlags`** enum (bits 0–8 used): `GameMenu=1, Map=2, SelectionMenu=4,
  Dialogue=8, NotInGame=16, Chat=32, Loading=64, EmptyScene=128, CameraControlUI=256`.
  High bits are free — we can own a private bit, e.g. `(CursorFlags)(1 << 20)`.
- Camera look lives in the `CameraBaseState` subclasses
  (`CameraCockpitState`, `CameraChaseState`, `CameraFreeState`, …) driven by
  `CameraStateManager` (`EnterState/LeaveState/UpdateState/FixedUpdateState`).
- Input is **Rewired** (`Rewired_Core.dll` present); no legacy `"Mouse X"` axis
  strings. No `InputSystem` references in Assembly-CSharp.
- HarmonyX is available (`BepInEx/core/0Harmony.dll`) if a patch is needed.

## Design

### Unlock mechanism — use the game's own flag system (recommended)

While the key is held: `CursorManager.SetFlag((CursorFlags)(1 << 20), true)`.
On release: `SetFlag(..., false)`.

Why this beats writing `Cursor.lockState` directly:

- CursorManager's `Refresh()`/`SetLockState()` does the real `Cursor.visible` /
  `lockState` work and won't fight us — flags are cumulative.
- If a menu/map opens while the key is held, both flags coexist; releasing our key
  leaves the menu's flag intact, so the cursor stays correctly unlocked.
- On release during normal flight, `GetFlags()` returns to `None` and the game
  re-locks through its normal path.
- `ClearGameplayFlags()` masks only the game's known bits, so our private bit
  survives scene transitions (we clear it ourselves on release anyway).

Fallback if the flag approach misbehaves: write `Cursor.lockState = None;
Cursor.visible = true` every `LateUpdate` while held and let the game's next
`Refresh()` restore on release.

### Input

- New config entries in `Plugin.cs` (section `"Cursor"`):
  - `FreeCursorKey` — `KeyboardShortcut`, default `LeftAlt` (hold).
  - Optionally `FreeCursorToggle` (bool, default false) — toggle vs. hold mode.
- Poll in `ModBarController.Update()` (already the frame loop; hot-reloadable,
  no `NoModBar.Core` change needed):
  - `shortcut.IsDown()` → set flag once; `IsUp()` / no longer pressed → clear flag.
  - Track `_freeCursorActive` to avoid spamming SetFlag every frame (cheap anyway,
    but keeps logs clean).
- Gate on the same `inGame` condition as the bar, so the key does nothing in menus
  where the game already manages the cursor.

### Mouse-look decoupling — verify, then patch only if needed

Unknown: whether the camera states gate look input on cursor visibility.

- **Step 1 (in-game test):** set the flag and observe. Two outcomes:
  - Look stops when cursor unlocks (game checks `CursorManager.Visible` or
    `Cursor.lockState`) → nothing else to do.
  - Camera still tracks the mouse while moving it over the bar → step 2.
- **Step 2 (Harmony patch, only if needed):** prefix-patch the look application in
  the active camera state (most likely `CameraCockpitState.UpdateState` and
  `CameraChaseState.UpdateState`; confirm the exact method with an IL scan for
  Rewired `GetAxis` calls at implementation time) to no-op while our flag bit is
  set. Patch lives in `NoModBar.dll` (hot-reloadable) using BepInEx.Harmony.

### Interaction with mod panels

Design decision (default): releasing the key always re-locks, even if a mod panel
is open — "hold key = cursor" is simple and predictable. If that feels bad in
practice, add a `KeepCursorWhilePanelOpen` config that keeps our flag set while any
registered entry reports `IsVisible() == true`.

## Implementation checklist

1. `Plugin.cs` — add `Cursor` config section (`FreeCursorKey`, maybe
   `FreeCursorToggle`).
2. New `src/FreeCursorController.cs` (or fold into `ModBarController`):
   - poll shortcut, set/clear private `CursorFlags` bit, log state changes at
     Debug level.
3. In-game smoke test with `LogOutput.log` open:
   - cursor appears while held, clicks land on bar buttons;
   - camera look behavior → decide on Harmony patch.
4. (Conditional) Harmony prefix on camera look; verify no residual drift.
5. Docs: update `README.md` (config section) and remove/rewrite the
   "bar only receives pointer input while the OS cursor is unlocked" limitation in
   `docs/architecture.md`.

## Risks / open questions

- **Look decoupling** is the only real unknown; everything else uses verified APIs.
  Mitigation: Harmony fallback, scoped to a single flag check.
- A future game update could claim bit `1 << 20` in `CursorFlags` — low risk; if it
  ever collides, symptoms are obvious (cursor stuck) and the bit is one constant.
- Hotkey conflicts with other mods/game bindings — mitigated by BepInEx config.
- `CameraCockpitState` failed reflection-only load during recon (dependency
  resolution); if we need to patch it, resolve the exact signature from the IL scan
  rather than assuming.
