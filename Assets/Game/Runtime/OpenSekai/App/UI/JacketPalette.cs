using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Colors the page borrows from a jacket: a light accent for the selection ring, the level
    /// number and the start button, and a darker glow for the page background.
    /// </summary>
    internal readonly struct JacketPalette
    {
        private const float GRAY_SATURATION = 0.08f;
        private const byte MIN_ALPHA = 32;

        internal JacketPalette(Color32 accent, Color32 glow)
        {
            Accent = accent;
            Glow = glow;
        }

        internal Color32 Accent { get; }
        internal Color32 Glow { get; }

        /// <summary>Used while a jacket loads and for packages without one.</summary>
        internal static JacketPalette Default =>
            new JacketPalette(LibraryTheme.Colors.DefaultAccent, new Color32(37, 52, 92, 255));

        /// <summary>
        /// Picks the hue the jacket is mostly made of. Vivid pixels weigh more than dull ones, so a
        /// colored subject on a gray or white background still sets the hue. Pure math: safe on a
        /// worker thread.
        /// </summary>
        internal static JacketPalette From(PixelImage image)
        {
            double red = 0d;
            double green = 0d;
            double blue = 0d;
            double weightSum = 0d;
            Color32[] pixels = image.Pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a < MIN_ALPHA)
                {
                    continue;
                }

                int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                double weight = 1d + (max - min);
                red += pixel.r * weight;
                green += pixel.g * weight;
                blue += pixel.b * weight;
                weightSum += weight;
            }

            if (weightSum <= 0d)
            {
                return Default;
            }

            var average = new Color(
                (float)(red / weightSum / 255d), (float)(green / weightSum / 255d), (float)(blue / weightSum / 255d));
            Color.RGBToHSV(average, out float hue, out float saturation, out float _);
            if (saturation < GRAY_SATURATION)
            {
                // A black-and-white jacket has no hue worth borrowing; stay neutral.
                return new JacketPalette(new Color32(196, 204, 216, 255), new Color32(52, 58, 68, 255));
            }

            Color accent = Color.HSVToRGB(hue, Mathf.Clamp(saturation, 0.28f, 0.45f), 1f);
            Color glow = Color.HSVToRGB(hue, Mathf.Clamp(saturation * 1.2f, 0.45f, 0.8f), 0.55f);
            return new JacketPalette(accent, glow);
        }
    }

    /// <summary>A decoded jacket thumbnail and the colors taken from it.</summary>
    internal readonly struct JacketThumbnail
    {
        internal JacketThumbnail(Sprite sprite, JacketPalette palette)
        {
            Sprite = sprite;
            Palette = palette;
        }

        /// <summary>Null when the package has no usable jacket.</summary>
        internal Sprite Sprite { get; }

        internal JacketPalette Palette { get; }

        internal static JacketThumbnail None => new JacketThumbnail(null, JacketPalette.Default);
    }
}
