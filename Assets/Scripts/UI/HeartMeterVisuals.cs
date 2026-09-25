using UnityEngine;

namespace Runner.UI
{
    /// <summary>
    /// Runtime-generated sprites for the heart collection meter. Shared by the canvas HUD
    /// and the legacy IMGUI HUD so both styles render the exact same bar.
    /// </summary>
    public static class HeartMeterVisuals
    {
        private const int BarWidth = 256;
        private const int BarHeight = 32;
        private const int BarRadius = 14;

        private static readonly Color FillHot = new Color(1f, 0.16f, 0.52f, 1f);
        private static readonly Color FillSoft = new Color(1f, 0.64f, 0.85f, 1f);

        private static Sprite barSprite;
        private static Sprite fillSprite;
        private static Sprite glowSprite;
        private static Sprite sheenSprite;

        /// <summary>Rounded white plate used for the empty track (tint it).</summary>
        public static Sprite Bar
        {
            get { return barSprite != null ? barSprite : (barSprite = BuildRounded(BarWidth, BarHeight, BarRadius, FlatWhite)); }
        }

        /// <summary>Rounded hot-pink to coral gradient used for the filled portion.</summary>
        public static Sprite Fill
        {
            get { return fillSprite != null ? fillSprite : (fillSprite = BuildRounded(BarWidth, BarHeight, BarRadius, Gradient)); }
        }

        /// <summary>Soft radial bloom used for the leading edge of the fill.</summary>
        public static Sprite Glow
        {
            get { return glowSprite != null ? glowSprite : (glowSprite = BuildRadial(64)); }
        }

        /// <summary>Soft vertical light band that sweeps across the fill.</summary>
        public static Sprite Sheen
        {
            get { return sheenSprite != null ? sheenSprite : (sheenSprite = BuildSheen()); }
        }

        private static Color FlatWhite(float u, float v)
        {
            return Color.white;
        }

        private static Color Gradient(float u, float v)
        {
            Color c = Color.Lerp(FillHot, FillSoft, u);
            // Baked top gloss so the liquid reads as glossy without an extra draw call.
            float gloss = Mathf.Clamp01((v - 0.68f) / 0.32f) * 0.30f;
            return Color.Lerp(c, Color.white, gloss);
        }

        private static Sprite BuildRounded(int width, int height, int radius, System.Func<float, float, Color> colorAt)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            Color32[] pixels = new Color32[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float corner = Mathf.Min(radius, Mathf.Min(halfW, halfH));

            for (int y = 0; y < height; y++)
            {
                float v = height > 1 ? y / (float)(height - 1) : 0f;
                for (int x = 0; x < width; x++)
                {
                    float u = width > 1 ? x / (float)(width - 1) : 0f;

                    float dx = (x + 0.5f) - halfW;
                    float dy = (y + 0.5f) - halfH;
                    float qx = Mathf.Abs(dx) - (halfW - corner);
                    float qy = Mathf.Abs(dy) - (halfH - corner);
                    float radial = Mathf.Sqrt(Mathf.Max(qx * qx, 0f) + Mathf.Max(qy * qy, 0f));
                    float sdf = Mathf.Min(Mathf.Max(qx, qy), 0f) + Mathf.Max(radial - corner, 0f);

                    Color c = colorAt(u, v);
                    c.a *= 1f - Mathf.SmoothStep(-1f, 1f, sdf);
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.name = "HeartMeterBar_" + width + "x" + height;

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildRadial(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - half;
                    float dy = (y + 0.5f) - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / half;

                    float k = Mathf.Clamp01(1f - r);
                    float a = k * k;
                    if (r < 0.20f) a = 1f;

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.name = "HeartMeterGlow";

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildSheen()
        {
            int width = 128;
            int height = 16;

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = height > 1 ? y / (float)(height - 1) : 0f;
                float vertical = Mathf.Clamp01(Mathf.Min(v, 1f - v) * 5f);

                for (int x = 0; x < width; x++)
                {
                    float u = width > 1 ? x / (float)(width - 1) : 0f;
                    float bell = Mathf.Sin(u * Mathf.PI);
                    float a = bell * bell * vertical * 0.9f;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.name = "HeartMeterSheen";

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
