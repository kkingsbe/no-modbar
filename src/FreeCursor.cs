using BepInEx.Configuration;
using UnityEngine;

namespace NoModBar
{
    /// <summary>
    /// Hold-to-free-cursor: while the configured shortcut is held in game,
    /// the OS cursor is unlocked/visible (decoupled from mouse look) so the
    /// bar and mod panels can be clicked. Release re-locks via the game's
    /// normal cursor path.
    /// </summary>
    internal static class FreeCursor
    {
        private static bool _active;

        public static bool Active => _active;

        public static void Tick(bool inGame)
        {
            KeyboardShortcut ks = Plugin.FreeCursorKey.Value;
            bool want = inGame
                        && ks.MainKey != KeyCode.None
                        && ks.IsPressed();

            if (want == _active) return;
            _active = want;
            CursorOverride.SetFreeCursor(want);
            Plugin.Log?.LogInfo($"ModBar: free cursor {(want ? "engaged" : "released")}");
        }

        /// <summary>Reset local state and clear our cursor flags (hot reload / shutdown).</summary>
        public static void Reset()
        {
            _active = false;
            CursorOverride.ClearAll();
        }
    }
}
