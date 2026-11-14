using NUnit.Framework;
using OpenSekai.Shared.Scoring;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Guards the lane and swipe tolerances: they live in data, so the risk is silent drift, not logic.
    /// </summary>
    public sealed class JudgeTuningAssetTests
    {
        [Test]
        public void NullDataYieldsNoTolerance()
        {
            JudgeTuning tuning = LiveBundleBuildData.GetJudgeTuning(null);

            Assert.That(tuning.NormalLaneOffset, Is.EqualTo(0f));
            Assert.That(tuning.LaneOffsetFor(NoteCategory.Flick), Is.EqualTo(0f));
        }

        [Test]
        public void ShippedAssetStillCarriesTheExpectedNumbers()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<LiveBundleBuildData>(
                "Assets/Graphics/Resources/live/config/LiveBundleBuildData.asset");
            Assert.That(asset, Is.Not.Null, "the live tuning asset is missing");

            JudgeTuning tuning = LiveBundleBuildData.GetJudgeTuning(asset);

            Assert.That(tuning.NormalLaneOffset, Is.EqualTo(JudgeTuning.ShippedNormalLaneOffset).Within(1e-5f));
            Assert.That(tuning.LongLaneOffset, Is.EqualTo(JudgeTuning.ShippedLongLaneOffset).Within(1e-5f));
            Assert.That(tuning.FlickLaneOffset, Is.EqualTo(JudgeTuning.ShippedFlickLaneOffset).Within(1e-5f));
            Assert.That(tuning.FlickDistanceInches, Is.EqualTo(JudgeTuning.ShippedFlickDistanceInches).Within(1e-5f));
        }
    }
}
