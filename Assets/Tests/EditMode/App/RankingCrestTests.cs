using System;
using NUnit.Framework;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class RankingCrestTests
    {
        private static readonly RankingCrestArt.Layer[] _layers = (RankingCrestArt.Layer[])Enum.GetValues(typeof(RankingCrestArt.Layer));

        [Test]
        public void EveryTierTheBackendCanReachHasANameAndAColor()
        {
            int top = RankingRules.Tier(long.MaxValue);
            Assert.That(RankingTiers.Count, Is.EqualTo(top + 1), "one entry per backend tier");
            Assert.That(RankingTiers.TOP, Is.EqualTo(top));
            Assert.That(RankingCrestArt.TOP_TIER, Is.EqualTo(top), "every tier above 星尘 has a crest");
            for (int tier = 0; tier <= top; tier++)
            {
                Assert.That(RankingTiers.Name(tier), Is.Not.Empty);
                Assert.That(RankingTiers.LatinName(tier), Is.Not.Empty);
            }
        }

        [Test]
        public void StardustKeepsThePlainBadge()
        {
            Assert.That(RankingCrestArt.HasCrest(0), Is.False);
            Assert.That(RankingCrestArt.HasCrest(1), Is.True);
        }

        [Test]
        public void EachTierKeepsEverythingTheTierBelowHas()
        {
            for (int tier = RankingCrestArt.FIRST_TIER + 1; tier <= RankingCrestArt.TOP_TIER; tier++)
            {
                foreach (RankingCrestArt.Layer layer in _layers)
                {
                    if (RankingCrestArt.Has(tier - 1, layer))
                    {
                        Assert.That(RankingCrestArt.Has(tier, layer), Is.True, RankingTiers.Name(tier) + " keeps " + layer);
                    }
                }

                Assert.That(RankingCrestArt.Glints(tier).Length, Is.GreaterThanOrEqualTo(RankingCrestArt.Glints(tier - 1).Length),
                    RankingTiers.Name(tier) + " has at least as much to glint");
            }
        }

        [Test]
        public void EveryLayerDrawsSomethingInsideItsBounds()
        {
            for (int tier = RankingCrestArt.FIRST_TIER; tier <= RankingCrestArt.TOP_TIER; tier++)
            {
                Color32 color = RankingTiers.Color(tier);
                var ink = new RankingCrestArt.Rgb(color.r, color.g, color.b);
                foreach (RankingCrestArt.Layer layer in _layers)
                {
                    if (!RankingCrestArt.Has(tier, layer))
                    {
                        continue;
                    }

                    byte[] pixels = RankingCrestArt.Render(tier, layer, ink, 96f, out int width, out int height);
                    Assert.That(pixels.Length, Is.EqualTo(width * height * 4));
                    int opaque = 0;
                    for (int i = 3; i < pixels.Length; i += 4)
                    {
                        if (pixels[i] > 0) opaque++;
                    }

                    string what = RankingTiers.Name(tier) + " " + layer;
                    Assert.That(opaque, Is.GreaterThan(0), what + " draws something");
                    // Nothing is cut off at the layer's edge: its rim stays clear.
                    for (int x = 0; x < width; x++)
                    {
                        Assert.That(pixels[x * 4 + 3], Is.Zero, what + " bottom edge");
                        Assert.That(pixels[((height - 1) * width + x) * 4 + 3], Is.Zero, what + " top edge");
                    }

                    for (int y = 0; y < height; y++)
                    {
                        Assert.That(pixels[y * width * 4 + 3], Is.Zero, what + " left edge");
                        Assert.That(pixels[(y * width + width - 1) * 4 + 3], Is.Zero, what + " right edge");
                    }
                }
            }
        }

        [Test]
        public void ABadgeTakesEveryTierAndAnUpgrade()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var badge = new RankingBadge(canvas.transform, 148f, animate: true);
                for (int tier = 0; tier < RankingTiers.Count; tier++)
                {
                    badge.SetTier(tier);
                    Assert.That(badge.Tier, Is.EqualTo(tier));
                    foreach (float reveal in new[] { 0f, 0.3f, 0.7f, 1f })
                    {
                        badge.SetMotion(1.1f, 0.5f, reveal);
                        badge.Tick(2.5d);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }
    }
}
