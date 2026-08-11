using System.Collections.Generic;
using UnityEngine;

namespace NoModBar
{
    internal static class TextureFactory
    {
        // Keyed by full parameter set — the previous single-slot cache returned
        // the wrong texture whenever a caller asked for a different size/border.
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> FramedSpriteCache = new Dictionary<string, Sprite>();

        // Unity's default canvas referencePixelsPerUnit is 100. Combined with a
        // PPU=1 framed sprite, this multiplier keeps one border texel equal to one
        // canvas unit instead of letting it balloon to 100 units.
        public const float FramedPpuMultiplier = 100f;

        /// <summary>
        /// SITREP-style 9-slice frame whose border remains exactly borderPx canvas
        /// units thick at any panel size. Unlike a stretched RawImage texture, the
        /// frame cannot scale inward and consume the layout padding.
        /// </summary>
        public static Sprite CreateFramedSprite(Color bgColor, Color borderColor, int borderPx = 1)
        {
            string key = ColorUtility.ToHtmlStringRGBA(bgColor) + ":"
                + ColorUtility.ToHtmlStringRGBA(borderColor) + ":" + borderPx;
            Sprite cached;
            if (FramedSpriteCache.TryGetValue(key, out cached) && cached != null)
                return cached;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool onBorder = x < borderPx || x >= size - borderPx
                        || y < borderPx || y >= size - borderPx;
                    pixels[y * size + x] = onBorder ? borderColor : bgColor;
                }

            tex.SetPixels(pixels);
            tex.Apply();

            var border = new Vector4(borderPx, borderPx, borderPx, borderPx);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, border);
            FramedSpriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Solid panel with an inset border. When <paramref name="edgeShadow"/> is set,
        /// a 1 px dark ring is drawn outside the border so the panel separates from
        /// bright backgrounds (sky, desert) without needing a real drop shadow.
        /// </summary>
        public static Texture2D CreatePanelBackground(int width, int height, Color bgColor,
            Color borderColor, float borderWidth = 1f, bool edgeShadow = false)
        {
            string key = width + "x" + height + ":"
                + ColorUtility.ToHtmlStringRGBA(bgColor) + ":"
                + ColorUtility.ToHtmlStringRGBA(borderColor) + ":"
                + borderWidth + ":" + edgeShadow;
            Texture2D tex;
            if (Cache.TryGetValue(key, out tex) && tex != null)
                return tex;

            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            int shadow = edgeShadow ? 1 : 0;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Distance from the nearest texture edge.
                    int d = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                    Color c;
                    if (d < shadow)
                        c = UiColors.EdgeShadow;
                    else if (d < shadow + borderWidth)
                        c = borderColor;
                    else
                        c = bgColor;
                    pixels[y * width + x] = c;
                }
            tex.SetPixels(pixels);
            tex.Apply();

            Cache[key] = tex;
            return tex;
        }
    }
}
