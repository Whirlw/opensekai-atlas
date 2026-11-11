using System.Reflection;
using System.Text;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
        /// <summary>The two opening heads of the Test16 chart: both are graded, and a bar that is never pressed still misses.</summary>
    public sealed class HeadSettlementTests
    {
        private static Vector3 LanePosition(float lane)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1);
            return LiveConfig.JudgmentPositions[index];
        }

        private static bool TryRun(bool pressRight, out string trace, out NoteResult leftResult, out NoteResult rightResult)
        {
            string json = System.IO.File.ReadAllText("Temp/test16-score.json");
            var doc = ChartJson.Read(json);
            var bundle = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
            var score = RuntimeChartConverter.Convert(doc, bundle);

            using (var harness = new LiveHarness())
            {
                harness.Arrange(score);

                LongNote left = null;
                LongNote right = null;
                foreach (var n in score.NoteArray)
                {
                    var bar = n as LongNote;
                    if (bar == null) { continue; }
                    if (bar.IsOrdinaryHead && bar.LaneStart <= 3) { left = bar; }
                    else if (bar.IsOrdinaryHead && bar.LaneStart >= 8) { right = bar; }
                }

                Assert.That(left, Is.Not.Null, "left bar");
                Assert.That(right, Is.Not.Null, "right bar");

                Vector3 leftFinger = LanePosition(2.3f);
                Vector3 rightFinger = LanePosition(10f);
                var sb = new StringBuilder();

                for (int frame = -36; frame <= 40; frame++)
                {
                    float now = frame / 60f;
                    if (frame <= -35) { harness.OnUpdate(now, now); continue; }

                    TouchPhase phase = frame <= -34 ? TouchPhase.Began : TouchPhase.Stationary;
                    var samples = pressRight
                        ? new[] { LiveHarness.Touch(1, leftFinger, now, phase), LiveHarness.Touch(2, rightFinger, now, phase) }
                        : new[] { LiveHarness.Touch(1, leftFinger, now, phase) };
                    harness.Step(now, now, samples);

                    if (frame % 4 == 0)
                    {
                        sb.Append("now=").Append(now.ToString("F2"))
                          .Append(" left=").Append(left.State).Append('/').Append(left.Result)
                          .Append(" right=").Append(right.State).Append('/').Append(right.Result)
                          .AppendLine();
                    }
                }

                trace = sb.ToString();
                leftResult = left.Result;
                rightResult = right.Result;
                return true;
            }
        }

        [Test]
        public void PressingBothOpeningNotesGradesBothHeads()
        {
            TryRun(true, out string trace, out NoteResult left, out NoteResult right);
            Debug.Log("[PROBE both] " + trace);
            Assert.That(left, Is.Not.EqualTo(NoteResult.None), "left head");
            Assert.That(right, Is.Not.EqualTo(NoteResult.None), "right head");
        }

        [Test]
        public void PressingOnlyTheLeftStillGradesTheRightHeadAsAMiss()
        {
            TryRun(false, out string trace, out NoteResult left, out NoteResult right);
            Debug.Log("[PROBE left-only] " + trace);
            Assert.That(right, Is.Not.EqualTo(NoteResult.None), "right head must not vanish");
        }
    

        [Test]
        public void ADirtySpeedRatioMakesTheHeadDisappear()
        {
            string json = System.IO.File.ReadAllText("Temp/test16-score.json");
            var doc = ChartJson.Read(json);
            var bundle = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
            var score = RuntimeChartConverter.Convert(doc, bundle);

            using (var harness = new LiveHarness())
            {
                harness.Arrange(score);

                LongNote right = null;
                foreach (var n in score.NoteArray)
                {
                    var bar = n as LongNote;
                    if (bar != null && bar.IsOrdinaryHead && bar.LaneStart >= 8) { right = bar; }
                }

                Assert.That(right, Is.Not.Null);
                right.speedRatio = 2f;   // what a recycled note looks like when the pool forgot to reset it

                Vector3 finger = LanePosition(10f);
                for (int frame = -36; frame <= 40; frame++)
                {
                    float now = frame / 60f;
                    if (frame <= -35) { harness.OnUpdate(now, now); continue; }
                    TouchPhase phase = frame <= -34 ? TouchPhase.Began : TouchPhase.Stationary;
                    harness.Step(now, now, LiveHarness.Touch(2, finger, now, phase));
                }

                Debug.Log("[PROBE dirty] right=" + right.State + "/" + right.Result);
                Assert.That(right.Result, Is.Not.EqualTo(NoteResult.None),
                    "a bar whose speedRatio was not 1 must still be judged");
            }
        }
    }
}