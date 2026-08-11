using UnityEngine;

namespace NoModBar
{
    /// <summary>
    /// Two-hue palette: desaturated phosphor green for chrome/idle,
    /// amber for state (active toggles, attention). Explicit hover/pressed
    /// backgrounds replace ColorMultiplier tints so states stay tunable.
    /// </summary>
    internal static class UiColors
    {
        // Panel chrome — desaturated military green on near-black.
        public static readonly Color BgPanel = Hex(0x0a0f0c, 0.93f);
        public static readonly Color BgPanelRaised = Hex(0x141d17);
        public static readonly Color BgHover = Hex(0x1e2d23);
        public static readonly Color BgPressed = Hex(0x293b2f);
        public static readonly Color BorderSubtle = Hex(0x1d3026);
        public static readonly Color BorderPanel = Hex(0x2e4c3a);
        public static readonly Color EdgeShadow = Hex(0x000000, 0.55f);

        // Phosphor green — primary accent (titles, highlights).
        public static readonly Color HudGreen = Hex(0x7fd4a8);

        // Amber — state accent (active toggles, attention).
        public static readonly Color Amber = Hex(0xffb340);
        public static readonly Color AmberBg = Hex(0xffb340, 0.16f);
        public static readonly Color AmberBgHover = Hex(0xffb340, 0.26f);
        public static readonly Color AmberBgPressed = Hex(0xffb340, 0.34f);

        // Text ramp.
        public static readonly Color TextPrimary = Hex(0xd9e8dd);
        public static readonly Color TextSecondary = Hex(0x9db5a4);
        public static readonly Color TextMuted = Hex(0x607f6b);

        private static Color Hex(int rgb, float alpha = 1f)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, alpha);
        }
    }
}
