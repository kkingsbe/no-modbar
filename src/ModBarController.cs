using UnityEngine;
using NoModBar.Core;

namespace NoModBar
{
    internal class ModBarController : MonoBehaviour
    {
        private ModBarCanvas _bar;
        private ModSettingsPanel _settings;
        private int _lastVersion = -1;

        private void Awake()
        {
            // Defensive: clear any cursor flags left by a previous (hot-reloaded) instance.
            FreeCursor.Reset();

            _settings = new ModSettingsPanel();
            _settings.Create();

            ModBarApi.Register(new
            {
                Id = ModSettingsPanel.EntryId,
                Name = "CFG",
                Tooltip = "NO Mod Bar settings",
                IsVisible = (System.Func<bool>)(() => _settings.IsOpen),
                Toggle = (System.Action)(() => _settings.Toggle())
            });

            _bar = new ModBarCanvas();
            _bar.Create();
            Plugin.Log?.LogInfo($"ModBar: canvas created, {ModBarApi.Snapshot().Count} registration(s) already present");
        }

        private void Update()
        {
            _bar.ApplyOffsets();
            _bar.Tick(Time.unscaledDeltaTime);

            if (ModBarApi.Version != _lastVersion)
            {
                _lastVersion = ModBarApi.Version;
                var entries = ModBarApi.Snapshot();
                _bar.Rebuild(entries);
                Plugin.Log?.LogInfo($"ModBar: rebuilt bar with {entries.Count} registration(s)");
            }

            _bar.RefreshActiveStates();

            FreeCursor.Tick(IsInGame());
            _settings.Tick();
        }

        internal static bool IsInGame()
        {
            try
            {
                Aircraft aircraft;
                return GameManager.GetLocalAircraft(out aircraft);
            }
            catch
            {
                return false;
            }
        }

        private void OnDestroy()
        {
            ModBarApi.Unregister(ModSettingsPanel.EntryId);
            FreeCursor.Reset();
            if (_settings != null) _settings.Destroy();
            if (_bar != null) _bar.Destroy();
        }
    }
}
