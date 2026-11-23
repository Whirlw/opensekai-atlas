using NUnit.Framework;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class RankingBadgeGalleryTests
    {
        /// <summary>A score inside 银河.</summary>
        private const long TOTAL = 9540;

        [Test]
        public void EachTierStartsWhereTheBackendSaysItDoes()
        {
            Assert.That(RankingTiers.Start(0), Is.EqualTo(0L));
            for (int tier = 1; tier < RankingTiers.Count; tier++)
            {
                long start = RankingTiers.Start(tier);
                Assert.That(RankingRules.Tier(start), Is.EqualTo(tier), RankingTiers.Name(tier) + " starts at " + start);
                Assert.That(RankingRules.Tier(start - 1), Is.EqualTo(tier - 1), "one point short of " + RankingTiers.Name(tier));
            }
        }

        [Test]
        public void TheCardShowsEveryTierAndWhereThePlayerStands()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var gallery = new RankingBadgeGallery((RectTransform)canvas.transform);
                Assert.That(gallery.IsOpen, Is.False);
                gallery.Open(TOTAL);
                Assert.That(gallery.IsOpen, Is.True);
                Assert.That(gallery.Count, Is.EqualTo(RankingTiers.Count));
                int current = RankingRules.Tier(TOTAL);
                Assert.That(gallery.CurrentTier, Is.EqualTo(current));
                for (int tier = 0; tier < gallery.Count; tier++)
                {
                    Assert.That(gallery.BadgeTier(tier), Is.EqualTo(tier), "each tile wears its own tier");
                    string expected = tier < current ? "已达成"
                        : tier == current ? "当前评级"
                        : "还差 " + (RankingTiers.Start(tier) - TOTAL).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " 分";
                    Assert.That(gallery.Status(tier), Is.EqualTo(expected), RankingTiers.Name(tier));
                }

                // An unreadable profile still shows every badge, with no standing against them.
                gallery.Open(null);
                Assert.That(gallery.CurrentTier, Is.EqualTo(-1));
                for (int tier = 0; tier < gallery.Count; tier++)
                {
                    Assert.That(gallery.Status(tier), Is.Empty);
                }

                gallery.Close();
                Assert.That(gallery.IsOpen, Is.False);
                gallery.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void AReplayChargesAsTheTierBelowAndRevealsAsItsOwn()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var gallery = new RankingBadgeGallery((RectTransform)canvas.transform);
                gallery.Open(TOTAL);
                int top = RankingTiers.TOP;

                gallery.SeekReplay(top, -0.1f);
                Assert.That(gallery.BadgeTier(top), Is.EqualTo(top - 1), "charging as the tier below");
                gallery.SeekReplay(top, 0.2f);
                Assert.That(gallery.BadgeTier(top), Is.EqualTo(top), "revealed as its own");

                // Starting another replay puts the first back at rest.
                gallery.SeekReplay(2, -0.1f);
                Assert.That(gallery.BadgeTier(top), Is.EqualTo(top));
                Assert.That(gallery.BadgeTier(2), Is.EqualTo(1));

                // 星尘 has no tier below it: it charges and reveals as itself.
                gallery.SeekReplay(0, -0.1f);
                Assert.That(gallery.BadgeTier(0), Is.EqualTo(0));
                Assert.That(gallery.BadgeTier(2), Is.EqualTo(2));

                gallery.SeekReplay(4, -0.05f);
                gallery.Close();
                Assert.That(gallery.BadgeTier(4), Is.EqualTo(4), "closing mid-replay leaves the badge at rest");
                gallery.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
