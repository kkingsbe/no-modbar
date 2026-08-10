# NO Mod Bar

A BepInEx 5 mod for Nuclear Option that renders a KSP-style toolbar in the top-left
corner of the screen while flying. Any mod's uGUI panel can register with the bar
and be opened/closed by clicking its button, so you never have to memorize hotkeys.

## Install

Copy `NoModBar.dll` to `<game>/BepInEx/plugins/`. No other dependencies.

## Build (dev)

Development follows the ScriptEngine hot-reload workflow (same as NO-VOR):

```
deploy.ps1                # dotnet build -c Debug + copy to BepInEx/scripts, drop stale plugins/ copy
dotnet build -c Debug     # just builds + deploys to BepInEx/scripts
dotnet build -c Release   # produces bin\Release\nomodbar-1.0.0.zip
```

Game path comes from `$(NuclearOptionRoot)` (see `Local.props.example`).

## Config (`BepInEx/config/dev.kilo.modbar.cfg`)

- `Bar > OffsetX` / `OffsetY` — bar position from the top-left corner (px). Live.
- `Bar > RequireInGame` — only show while a local aircraft is present (default on).
- `Bar > ButtonSize` — square button side length in px.

## Registering a mod (for mod authors)

Do NOT add a compile-time reference to `NoModBar.dll`. Copy the reflection bridge
below into your project and call `Register` once (e.g. in `Start`) and `Unregister`
in `OnDestroy`. If the bar mod is not installed, the calls no-op and your mod is
unaffected.

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
        private static bool _resolved;

        private static void Resolve()
        {
            _resolved = true;
            _api = Type.GetType("NoModBar.ModBarApi, NoModBar");
            if (_api == null) return;
            _register = _api.GetMethod("Register", new[] { typeof(object) });
            _unregister = _api.GetMethod("Unregister", new[] { typeof(string) });
        }

        public static bool Register(string id, string name, string tooltip, Func<bool> isVisible, Action toggle)
        {
            if (!_resolved) Resolve();
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
            if (!_resolved) Resolve();
            if (_unregister == null) return false;
            try { return (bool)_unregister.Invoke(null, new object[] { id }); }
            catch { return false; }
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
