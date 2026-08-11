# Fix Stacked Mod-Bar Items Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore distinct, side-by-side CFG, VOR, and SIT tiles in the expanded mod bar.

**Architecture:** Keep the current nested `Content` container and collapse animation. Let Unity's two `HorizontalLayoutGroup` instances control child widths so `LayoutElement.preferredWidth` is applied to both the content container and each tile; this preserves the animated content width while eliminating clipping and overlap.

**Tech Stack:** C# / .NET Framework 4.7.2, Unity 2022.3 uGUI, BepInEx 5

---

### Task 1: Restore controlled horizontal sizing

**Files:**
- Modify: `src/ModBarCanvas.cs:119`
- Modify: `src/ModBarCanvas.cs:206`

- [x] **Step 1: Make the outer strip apply each child's layout width**

In `BuildStrip`, change the strip layout setting to:

```csharp
            _stripLayout.childControlWidth = true;
```

This makes the strip assign the collapse button its configured button size, the divider its one-pixel width, and the content container the animated width set by `ApplyCollapse`.

- [x] **Step 2: Make the content row apply each tile's layout width**

In `BuildContent`, change the nested layout setting to:

```csharp
            hlg.childControlWidth = true;
```

This makes every registered button use the width supplied by `MakeButton` rather than retaining an unmanaged/default `RectTransform` width.

- [x] **Step 3: Build without deploying to verify compilation**

Run:

```powershell
dotnet build NoModBar.csproj -c Release
```

Expected: `Build succeeded` with 0 errors.

- [x] **Step 4: Build and deploy the debug DLL**

Run:

```powershell
dotnet build NoModBar.csproj -c Debug
```

Expected: `Build succeeded`; `NoModBar.dll` is copied to `BepInEx/scripts/` and `NoModBar.Core.dll` to `BepInEx/plugins/`.

Result: the plugin compiled and `NoModBar.dll` deployed with a matching SHA-256 hash. The final MSBuild target could not replace the already-loaded `NoModBar.Core.dll`; that assembly contains no changes for this fix and can be refreshed after exiting the game.

- [ ] **Step 5: Verify the layout in game**

1. Reload NO Mod Bar with ScriptEngine or restart Nuclear Option.
2. Enter a mission and confirm CFG, VOR, and SIT render as three separate tiles in one row.
3. Click the collapse control and confirm the content width animates to zero without overlapping tiles.
4. Expand again and confirm all three tiles return, keep equal widths, and each opens its own panel.
5. Change `Bar > ButtonSize` through CFG and confirm all tiles resize while retaining spacing.

- [ ] **Step 6: Commit only if requested**

```powershell
git add src/ModBarCanvas.cs docs/superpowers/plans/2026-08-10-fix-stacked-modbar-items.md
git commit -m "fix: keep mod bar items in separate layout slots"
```

---

## Self-Review

**Spec coverage:** Separate CFG/VOR/SIT tiles are restored by the nested row sizing change; the outer sizing change ensures the row receives its full preferred width and keeps collapse animation behavior.

**Placeholder scan:** No placeholder steps or unspecified code changes remain.

**Type consistency:** Both changes use the existing Unity `HorizontalLayoutGroup.childControlWidth` property; no interfaces, registrations, or config types change.
