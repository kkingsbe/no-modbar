using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using NoModBar.Core;

namespace NoModBar
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance { get; private set; }

        internal static ConfigEntry<float> OffsetX;
        internal static ConfigEntry<float> OffsetY;
        internal static ConfigEntry<bool> RequireInGame;
        internal static ConfigEntry<float> ButtonSize;

        private GameObject _controller;

        private void Awake()
        {
            if (Instance != null)
            {
                Log?.LogWarning("ModBar duplicate instance detected, destroying");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Log = Logger;
            ModBarApi.LogInfo = msg => Logger.LogInfo(msg);
            ModBarApi.LogWarning = msg => Logger.LogWarning(msg);

            OffsetX = Config.Bind("Bar", "OffsetX", 12f,
                new ConfigDescription("Horizontal offset of the bar from the top-left corner (screen px).",
                    new AcceptableValueRange<float>(-500f, 3000f)));
            OffsetY = Config.Bind("Bar", "OffsetY", 12f,
                new ConfigDescription("Vertical offset of the bar from the top-left corner (screen px).",
                    new AcceptableValueRange<float>(-500f, 3000f)));
            RequireInGame = Config.Bind("Bar", "RequireInGame", true,
                "Only show the bar while flying (local aircraft present).");
            ButtonSize = Config.Bind("Bar", "ButtonSize", 30f,
                new ConfigDescription("Side length of each mod button in the bar (px).",
                    new AcceptableValueRange<float>(18f, 64f)));

            _controller = new GameObject("NoModBarController");
            _controller.AddComponent<ModBarController>();
            DontDestroyOnLoad(_controller);

            Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded.");
        }

        private void OnDestroy()
        {
            if (_controller != null) Destroy(_controller);
            Log?.LogInfo("ModBar shut down.");
        }
    }
}
