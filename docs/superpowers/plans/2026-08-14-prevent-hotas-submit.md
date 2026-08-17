# Prevent HOTAS Submit from Toggling Mod Bar Windows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure a HOTAS trigger cannot activate a NO Mod Bar button after a ScriptEngine hot reload.

**Architecture:** The fallback event system created by NO Mod Bar will continue to use Unity's `StandaloneInputModule` for mouse/pointer clicks, but its Submit and Cancel action names will be blank. That prevents any controller or HOTAS binding which Unity exposes as Submit from invoking the currently selected button. Before a bar canvas is destroyed, clear selection only when that selected object belongs to the bar.

**Tech Stack:** BepInEx 5, Unity 2022 uGUI/EventSystem, C# net472.

---

### Task 1: Constrain fallback UI input to pointers

**Files:**
- Modify: `C:\Users\Kyle\Documents\code\no-modbar\src\ModBarCanvas.cs:84-90`

- [ ] **Step 1: Reproduce the current controller-submit path in game**

Start Nuclear Option with a HOTAS trigger bound to Unity's `Submit` input. Hot reload NO Mod Bar, click the `CFG` tile once to make it the selected uGUI object, then press the trigger. Expected before the change: the selected tile's window toggles.

- [ ] **Step 2: Configure the fallback module after creating it**

Replace the fallback event-system block with the following code so pointer clicks remain enabled but controller Submit/Cancel actions are ignored:

```csharp
if (Object.FindObjectOfType<EventSystem>() == null)
{
    var esGo = new GameObject("NoModBarEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    var input = esGo.GetComponent<StandaloneInputModule>();
    input.submitButton = string.Empty;
    input.cancelButton = string.Empty;
    Object.DontDestroyOnLoad(esGo);
}
```

- [ ] **Step 3: Build the plugin**

Run: `dotnet build C:\Users\Kyle\Documents\code\no-modbar\NoModBar.csproj -c Release`

Expected: `Build succeeded` with no compilation errors.

### Task 2: Remove stale selection during bar teardown

**Files:**
- Modify: `C:\Users\Kyle\Documents\code\no-modbar\src\ModBarCanvas.cs:583-587`

- [ ] **Step 1: Add selection cleanup before the canvas is destroyed**

Replace `Destroy()` with the following implementation:

```csharp
public void Destroy()
{
    var eventSystem = EventSystem.current;
    var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
    if (selected != null && _root != null && selected.transform.IsChildOf(_root.transform))
        eventSystem.SetSelectedGameObject(null);

    if (_root != null)
        Object.Destroy(_root);
}
```

- [ ] **Step 2: Build the plugin again**

Run: `dotnet build C:\Users\Kyle\Documents\code\no-modbar\NoModBar.csproj -c Release`

Expected: `Build succeeded` with no compilation errors.

- [ ] **Step 3: Verify the hot-reload regression manually**

In Nuclear Option, open a bar window and hot reload NO Mod Bar while the cursor is over that tile. Press the HOTAS trigger repeatedly, then click the tile with the mouse. Expected: trigger presses do not open or close any bar window; a mouse click still toggles the tile; game menus retain their normal controller navigation because only NO Mod Bar's fallback event system has Submit disabled.

- [ ] **Step 4: Commit**

```powershell
git -C C:\Users\Kyle\Documents\code\no-modbar add src/ModBarCanvas.cs docs/superpowers/plans/2026-08-14-prevent-hotas-submit.md
git -C C:\Users\Kyle\Documents\code\no-modbar commit -m "fix: prevent HOTAS submit from toggling mod bar"
```

### Self-review

- **Spec coverage:** Task 1 blocks the specific HOTAS-to-Submit activation path; Task 2 removes the selected stale button created by a reload; Task 2's manual acceptance verifies both prevention and ordinary mouse use.
- **Placeholder scan:** No TBD/TODO instructions or unspecified implementation details remain.
- **Type consistency:** `StandaloneInputModule`, `EventSystem`, `currentSelectedGameObject`, and `GameObject.transform.IsChildOf` are all available from the `UnityEngine.EventSystems` and `UnityEngine` imports already present in `ModBarCanvas.cs`.
