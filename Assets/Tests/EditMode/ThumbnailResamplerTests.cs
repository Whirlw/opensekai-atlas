using NUnit.Framework;
using OpenSekai.App.UI;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class ThumbnailResamplerTests
    {
        [Test]
        public void ImagesThatAlreadyFitAreReturnedUntouched()
        {
            var source = new PixelImage(new Color32[4 * 2], 4, 2);

            PixelImage result = ThumbnailResampler.Downscale(source, 4);

            Assert.That(result.Pixels, Is.SameAs(source.Pixels));
        }

        [Test]
        public void TheLongerSideFitsAndTheAspectRatioIsKept()
        {
            var source = new PixelImage(new Color32[512 * 384], 512, 384);

            PixelImage result = ThumbnailResampler.Downscale(source, 256);

            Assert.That((result.Width, result.Height), Is.EqualTo((256, 192)));
            Assert.That(result.Pixels.Length, Is.EqualTo(256 * 192));
        }

        [Test]
        public void EachTargetPixelAveragesTheSourcePixelsUnderIt()
        {
            var black = new Color32(0, 0, 0, 255);
            var white = new Color32(255, 255, 255, 255);
            var red = new Color32(255, 0, 0, 255);
            // 4×2, bottom row first: the left half mixes black and white, the right half is red.
            var source = new PixelImage(
                new[]
                {
                    black, white, red, red,
                    white, black, red, red
                },
                4, 2);

            PixelImage result = ThumbnailResampler.Downscale(source, 2);

            Assert.That((result.Width, result.Height), Is.EqualTo((2, 1)));
            Assert.That(result.Pixels[0], Is.EqualTo(new Color32(128, 128, 128, 255)));
            Assert.That(result.Pixels[1], Is.EqualTo(red));
        }
    }
}
