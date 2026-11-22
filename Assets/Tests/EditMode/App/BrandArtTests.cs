using System;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace OpenSekai.Tests.EditMode
{
    public sealed class BrandArtTests
    {
        private const int SIZE = 256;

        [TestCase("OPEN SEKAI ATLAS", 34f, 14f)]
        [TestCase("OPEN SEKAI ATLAS", 84f, 18f)]
        [TestCase("DAN", 22f, 28f)]
        public void HomeTitlesKeepTheirTrackingWhenTheFontIsPopulated(string text, float size, float spacing)
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                TextMeshProUGUI label;
                if (text == "DAN")
                {
                    var card = new HomeFeatureCard(canvas.transform, "DanCard", text, "段位", "", Color.white, null, null);
                    card.Root.sizeDelta = new Vector2(700f, 400f);
                    label = card.Root.GetComponentsInChildren<TextMeshProUGUI>().Single(item => item.text == text);
                }
                else
                {
                    label = HomeView.CreateWordmark("Wordmark", canvas.transform, size, spacing);
                    label.rectTransform.sizeDelta = new Vector2(2400f, 160f);
                }

                float settled = ReadLetterGap(label, text);
                label.font.TryAddCharacters("ACHIEVEMENTS OPEN SEKAI ATLAS DAN", true);
                LibraryUi.SetLetterSpacing(label, 64f);
                LibraryUi.SetLetterSpacing(label, 64f);
                Assert.That(ReadLetterGap(label, text) - settled, Is.EqualTo((64f - spacing) * size / 100f).Within(0.01f));
                LibraryUi.SetLetterSpacing(label, spacing);
                Assert.That(ReadLetterGap(label, text), Is.EqualTo(settled).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        private static float ReadLetterGap(TextMeshProUGUI label, string text)
        {
            label.ForceMeshUpdate();
            Assert.That(label.GetParsedText(), Is.EqualTo(text));
            Assert.That(label.fontFeatures.Contains(OTL_FeatureTag.kern), Is.False);
            var info = label.textInfo;
            float? firstGap = null;
            for (int i = 1; i < info.characterCount; i++)
            {
                var left = info.characterInfo[i - 1];
                var right = info.characterInfo[i];
                if (!left.isVisible || !right.isVisible) continue;
                var rightEdge = info.meshInfo[left.materialReferenceIndex].vertices[left.vertexIndex + 2];
                var leftEdge = info.meshInfo[right.materialReferenceIndex].vertices[right.vertexIndex];
                float gap = leftEdge.x - rightEdge.x;
                firstGap ??= gap;
                Assert.That(gap, Is.EqualTo(firstGap.Value).Within(0.01f), $"{left.character}/{right.character}");
            }

            Assert.That(firstGap.HasValue, Is.True);
            return firstGap.Value;
        }

        [Test]
        public void ImportedSplashContainsEveryNamedLayer()
        {
            foreach (BrandArt.Part part in Enum.GetValues(typeof(BrandArt.Part)))
            {
                Assert.That(BrandSprites.Layers[part].name, Is.EqualTo(part.ToString()));
            }
        }

        [Test]
        public void EveryLayerHasOnePixelPerTexel()
        {
            foreach (BrandArt.Part part in Enum.GetValues(typeof(BrandArt.Part)))
            {
                Assert.That(BrandArt.Render(part, SIZE).Length, Is.EqualTo(SIZE * SIZE * 4), part.ToString());
            }
        }

        [Test]
        public void TheLegsMeetAtTheApexAndLeaveTheCornersEmpty()
        {
            byte[] rails = BrandArt.Render(BrandArt.Part.RailsWhole, SIZE);

            Assert.That(AlphaAt(rails, BrandArt.APEX_X, BrandArt.APEX_Y - 0.01f), Is.GreaterThan(250));
            Assert.That(AlphaAt(rails, 0.02f, 0.98f), Is.Zero);
            Assert.That(AlphaAt(rails, 0.98f, 0.02f), Is.Zero);
            // The middle of the letter is open: the highway runs between the legs.
            Assert.That(AlphaAt(rails, BrandArt.APEX_X, 0.3f), Is.Zero);
        }

        [Test]
        public void TheOrbitIsCutIntoTheLegsOnlyWhereItPassesInFront()
        {
            byte[] whole = BrandArt.Render(BrandArt.Part.RailsWhole, SIZE);
            byte[] cut = BrandArt.Render(BrandArt.Part.Rails, SIZE);

            int removed = 0;
            for (int i = 3; i < whole.Length; i += 4)
            {
                Assert.That(cut[i], Is.LessThanOrEqualTo(whole[i]), "a cut never adds coverage");
                if (whole[i] - cut[i] > 128)
                {
                    removed++;
                }
            }

            Assert.That(removed, Is.GreaterThan(20), "the front of the orbit crosses both legs");
            Assert.That(removed, Is.LessThan(whole.Length / 4 / 40), "but only in narrow bands");
        }

        [Test]
        public void TheAdaptiveForegroundStaysInsideTheLauncherMask()
        {
            byte[] foreground = BrandArt.Render(BrandArt.Icon.AdaptiveForeground, SIZE);

            // Launchers mask the 108 dp layer to a 72 dp shape; a circle is the tightest of them.
            const float VISIBLE_RADIUS = 72f / 108f * 0.5f;
            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float dx = (x + 0.5f) / SIZE - 0.5f;
                    float dy = (y + 0.5f) / SIZE - 0.5f;
                    if (Math.Sqrt(dx * dx + dy * dy) > VISIBLE_RADIUS)
                    {
                        // Only the faint glow may reach past it, never the mark itself.
                        Assert.That(foreground[(y * SIZE + x) * 4 + 3], Is.LessThan(96), "pixel " + x + "," + y);
                    }
                }
            }
        }

        [Test]
        public void TheFullIconIsATileWithTransparentCorners()
        {
            byte[] icon = BrandArt.Render(BrandArt.Icon.Full, SIZE);

            Assert.That(icon[3], Is.Zero, "bottom-left corner");
            Assert.That(icon[(SIZE * SIZE - 1) * 4 + 3], Is.Zero, "top-right corner");
            Assert.That(icon[((SIZE / 2) * SIZE + SIZE / 2) * 4 + 3], Is.EqualTo(255), "center");
        }

        [Test]
        public void RelativeTimesReadTheWayTheLibraryCardShowsThem()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            Assert.That(HomeScreen.DescribeWhen(now.AddSeconds(-20)), Is.EqualTo("刚刚"));
            Assert.That(HomeScreen.DescribeWhen(now.AddMinutes(-12)), Is.EqualTo("12 分钟前"));
            Assert.That(HomeScreen.DescribeWhen(now.AddHours(-3)), Is.EqualTo("3 小时前"));
            Assert.That(HomeScreen.DescribeWhen(now.AddDays(-2)), Is.EqualTo("2 天前"));
            Assert.That(HomeScreen.DescribeWhen(now.AddDays(-30)), Does.Match(@"^\d{4}-\d{2}-\d{2}$"));
        }

        [Test]
        public void SpansClampAndEase()
        {
            Assert.That(HomeMotion.Span(0.5f, 1f, 2f), Is.Zero);
            Assert.That(HomeMotion.Span(2f, 1f, 2f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(HomeMotion.Span(9f, 1f, 2f), Is.EqualTo(1f));
            Assert.That(HomeMotion.Span(1f, 1f, 0f), Is.EqualTo(1f), "a zero-length span is a step");
        }

        private static int AlphaAt(byte[] pixels, float u, float v)
        {
            int x = Math.Min(SIZE - 1, (int)(u * SIZE));
            int y = Math.Min(SIZE - 1, (int)(v * SIZE));
            return pixels[(y * SIZE + x) * 4 + 3];
        }
    }
}
