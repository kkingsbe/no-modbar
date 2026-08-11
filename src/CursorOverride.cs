using System;
using UnityEngine;

namespace NoModBar
{
    /// <summary>
    /// Owns the mod's private bits in the game's CursorManager flag set.
    /// CursorFlags bits 0..8 are used by the game (GameMenu..CameraControlUI);
    /// we claim high bits the game never touches, so CursorManager.Refresh()
    /// applies our state alongside the game's own flags without conflict.
    /// </summary>
    internal static class CursorOverride
    {
        // Private flags (verified free in CursorFlags: bits 0-8 used by the game).
        private static readonly CursorFlags FreeCursorFlag = (CursorFlags)(1 << 20);
        private static readonly CursorFlags ModUiFlag = (CursorFlags)(1 << 21);

        /// <summary>Set while the free-cursor hotkey is held.</summary>
        public static void SetFreeCursor(bool on) => SetFlagSafe(FreeCursorFlag, on, "free-cursor");

        /// <summary>Set while the mod's own settings panel is open.</summary>
        public static void SetModUi(bool on) => SetFlagSafe(ModUiFlag, on, "mod-ui");

        /// <summary>Clear both bits (hot reload / shutdown cleanup).</summary>
        public static void ClearAll()
        {
            SetFreeCursor(false);
            SetModUi(false);
        }

        private static void SetFlagSafe(CursorFlags flag, bool value, string tag)
        {
            try
            {
                CursorManager.SetFlag(flag, value);
                // Refresh recomputes Cursor.visible / lockState from the flag set;
                // safe to call even if SetFlag already refreshed internally.
                CursorManager.Refresh();
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"ModBar: CursorManager.SetFlag({tag}={value}) failed: {e.Message}");
            }
        }
    }
}
