# NO Mod Bar

A BepInEx 5 mod for Nuclear Option that renders a persistent KSP-style toolbar at
the top-center of every game screen. Any mod's uGUI panel can register with
the bar and be opened/closed by clicking its button, so you never have to memorize
hotkeys.

Hold **Left Alt** (rebindable) while flying to free the mouse cursor from
mouse-look, click bar buttons or panels, then release to lock back into the
cockpit. The bar's **CFG** button opens the mod's own settings panel, where the
hotkey can be rebound by pressing the new key(s).

## Install

Copy `NoModBar.dll` and `NoModBar.Core.dll` to `<game>/BepInEx/plugins/`. No other
dependencies.

## Build (dev)

The bar ships as two assemblies:

- `NoModBar.Core.dll` — the stable registry + registration API. Lives in
  `BepInEx/plugins/` (Chainloader), so it is loaded once and survives hot reloads.
- `NoModBar.dll` — the bar UI plugin. Lives in `BepInEx/scripts/` (ScriptEngine),
  so the UI is hot-reloadable with F6 / the file watcher.

```
deploy.ps1                # builds + deploys both DLLs to the right places
dotnet build NoModBar.csproj -c Debug   # just builds + deploys
dotnet build -c Release   # produces bin\Release\nomodbar-1.0.0.zip (both DLLs flat at the zip root, NOMM-compatible)
```

Game path comes from `$(NuclearOptionRoot)` (see `Local.props.example`).

## Config (`BepInEx/config/dev.kilo.modbar.cfg`)

- `Bar > CenterOffsetX` — horizontal offset from the screen's top-center (px). Live.
- `Bar > OffsetY` — vertical offset from the top edge (px). Live.
- `Bar > ButtonSize` — square button side length in px.
- `Cursor > FreeCursorKey` — hold-to-free-cursor shortcut (default `LeftAlt`).
  Rebindable in game via the CFG panel.

## Registering a mod (for mod authors)

Do NOT add a compile-time reference to `NoModBar.Core.dll`. Copy the reflection
bridge below into your project and call `Register` once (e.g. in `Start`) and
`Unregister` in `OnDestroy`. If the bar mod is not installed, the calls no-op and
your mod is unaffected. The bridge locates the stable core assembly by scanning
`AppDomain.CurrentDomain.GetAssemblies()` for `NoModBar.Core`, so it works
regardless of load order and ScriptEngine hot reloads.

```csharp
using System;
using System.Reflection;

namespace YourMod.Integrations
{
    internal static class ModBarBridge
    {
        private static Type _api;
        private static MethodInfo _register;
        private static MethodInfo _unregister;

        public static bool Register(string id, string name, string tooltip, Func<bool> isVisible, Action toggle)
        {
            Resolve();
            if (_register == null) return false;
            try
            {
                var entry = new { Id = id, Name = name, Tooltip = tooltip, IsVisible = isVisible, Toggle = toggle };
                return (bool)_register.Invoke(null, new object[] { entry });
            }
            catch { return false; }
        }

        public static bool Unregister(string id)
        {
            Resolve();
            if (_unregister == null) return false;
            try { return (bool)_unregister.Invoke(null, new object[] { id }); }
            catch { return false; }
        }

        private static void Resolve()
        {
            if (_api != null) return;
            var asm = FindApiAssembly();
            if (asm == null) return;
            _api = asm.GetType("NoModBar.Core.ModBarApi");
            if (_api == null) return;
            _register = _api.GetMethod("Register", new[] { typeof(object) });
            _unregister = _api.GetMethod("Unregister", new[] { typeof(string) });
        }

        private static Assembly FindApiAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    if (assemblies[i].GetName().Name == "NoModBar.Core")
                        return assemblies[i];
                }
                catch { }
            }
            return null;
        }
    }
}
```

Contract (frozen for v1):

| Property | Type | Required | Meaning |
|----------|------|----------|---------|
| `Id` | `string` | yes | Stable unique id, e.g. `"no.sitrep"` |
| `Name` | `string` | yes | Short button label, e.g. `"SIT"` (<= 4 chars recommended) |
| `Tooltip` | `string` | no | Full name shown on hover |
| `IsVisible` | `Func<bool>` | no | Polled to highlight the button while the panel is open |
| `Toggle` | `Action` | yes | Opens/closes your panel |
