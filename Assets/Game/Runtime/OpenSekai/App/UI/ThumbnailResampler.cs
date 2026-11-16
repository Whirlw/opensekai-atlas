using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>Pixels of an image, rows bottom-up as <c>Texture2D.GetPixels32</c> returns them.</summary>
    internal readonly struct PixelImage
    {
        internal PixelImage(Color32[] pixels, int width, int height)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
        }

        internal Color32[] Pixels { get; }
        internal int Width { get; }
        internal int Height { get; }
    }

    /// <summary>
    /// Shrinks an image on the CPU by averaging the source pixels under each target pixel. It
    /// touches no Unity object, so it runs on a worker thread and keeps the downscale of a
    /// full-size jacket off the main thread.
    /// </summary>
    internal static class ThumbnailResampler
    {
        /// <summary>
        /// Fits the image inside a <paramref name="maxSize"/> square, keeping its aspect ratio.
        /// An image that already fits is returned as is.
        /// </summary>
        internal static PixelImage Downscale(PixelImage source, int maxSize)
        {
            int longest = Mathf.Max(source.Width, source.Height);
            if (maxSize <= 0 || longest <= maxSize)
            {
                return source;
            }

            int width = Mathf.Max(1, Mathf.RoundToInt((float)source.Width * maxSize / longest));
            int height = Mathf.Max(1, Mathf.RoundToInt((float)source.Height * maxSize / longest));
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                int sourceTop = y * source.Height / height;
                int sourceBottom = Mathf.Max(sourceTop + 1, (y + 1) * source.Height / height);
                for (int x = 0; x < width; x++)
                {
                    int sourceLeft = x * source.Width / width;
                    int sourceRight = Mathf.Max(sourceLeft + 1, (x + 1) * source.Width / width);
                    pixels[y * width + x] = Average(source, sourceLeft, sourceRight, sourceTop, sourceBottom);
                }
            }

            return new PixelImage(pixels, width, height);
        }

        private static Color32 Average(PixelImage source, int left, int right, int top, int bottom)
        {
            int r = 0;
            int g = 0;
            int b = 0;
            int a = 0;
            for (int y = top; y < bottom; y++)
            {
                int row = y * source.Width;
                for (int x = left; x < right; x++)
                {
                    Color32 pixel = source.Pixels[row + x];
                    r += pixel.r;
                    g += pixel.g;
                    b += pixel.b;
                    a += pixel.a;
                }
            }

            int count = (right - left) * (bottom - top);
            int half = count / 2;
            return new Color32(
                (byte)((r + half) / count), (byte)((g + half) / count), (byte)((b + half) / count), (byte)((a + half) / count));
        }
    }
}
