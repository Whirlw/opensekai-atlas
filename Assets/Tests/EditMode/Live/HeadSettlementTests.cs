using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class HeadSettlementTests
    {
        private static (NoteResult left, NoteResult right) Run(bool pressLeft, bool pressRight, float rightSpeedRatio = 1f)
        {
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
                "head-settlement", "score.json"));
            var document = ChartJson.Read(File.ReadAllText(fixture));
            var bundle = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
            Assert.That(bundle, Is.Not.Null);
            var score = RuntimeChartConverter.Convert(document, bundle);
            var bars = score.NoteArray.OfType<LongNote>().OrderBy(note => note.LaneStart).ToArray();
            Assert.That(bars, Has.Length.EqualTo(2));
            Assert.That(bars.All(bar => bar.IsOrdinaryHead && bar.ChildNote != null), Is.True);
            Assert.That(bars[0].MusicScoreInfo.time, Is.EqualTo(bars[1].MusicScoreInfo.time));
            using var harness = new LiveHarness();
            harness.Arrange(score);
            bars[1].speedRatio = rightSpeedRatio;
            float hitTime = bars[0].MusicScoreInfo.time;
            for (int frame = -36; frame <= 40; frame++)
            {
                float now = hitTime + frame / 60f;
                double inputTime = 10d + now;
                harness.OnUpdate(now, inputTime);
                if (frame >= 0)
                {
                    var phase = frame == 0 ? TouchPhase.Began : TouchPhase.Stationary;
                    if (pressLeft)
                        harness.AddTouch(1, LiveConfig.JudgmentPositions[2], inputTime, phase);
                    if (pressRight)
                        harness.AddTouch(2, LiveConfig.JudgmentPositions[10], inputTime, phase);
                }
                harness.OnInput();
            }

            return (bars[0].Head.Result, bars[1].Head.Result);
        }

        [Test]
        public void PressingBothOpeningNotesGradesBothHeads()
        {
            var results = Run(pressLeft: true, pressRight: true);
            Assert.That(results.left, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss));
            Assert.That(results.right, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void PressingOnlyTheLeftStillGradesTheRightHeadAsAMiss()
        {
            var results = Run(pressLeft: true, pressRight: false);
            Assert.That(results.left, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss));
            Assert.That(results.right, Is.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void ADirtySpeedRatioDoesNotMakeTheHeadDisappear()
        {
            // Simulate a recycled note whose speedRatio was not reset by the pool.
            var results = Run(pressLeft: false, pressRight: true, rightSpeedRatio: 2f);
            Assert.That(results.right, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss));
        }
    }
}
