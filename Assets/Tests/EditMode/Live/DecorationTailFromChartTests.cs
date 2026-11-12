using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;

using Sekai.Core.Live;
using Sekai.Live;

using UnityEngine;
using UnityEngine.InputSystem;

using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// A real chart (the reporter's Test10) driven the way the game drives it: converted by
    /// <see cref="RuntimeChartConverter"/>, then stepped through the real OnUpdate/OnInput loop with a
    /// scripted finger. Everything is taken from the chart, so a decorative bar and the note at its end
    /// keep exactly the shapes, lanes and times the player meets on device.
    /// </summary>
    [TestFixture]
    public class DecorationTailFromChartTests
    {
        private const double InputTime = 10d;
        private const string ChartPath = "Tools/fixtures/deco-tail-test10/score.json";

        private bool _previousLegacyTiming;
        private LiveConfig.NoteTypeJudgeData _previousJudgeData;
        private MusicScoreInfo _previousFrame;
        private Mouse _isolatedMouse;
        private Mouse _previousMouse;

        [SetUp]
        public void SetUp()
        {
            _previousLegacyTiming = LiveConfig.UseLegacyJudgeTiming;
            _previousJudgeData = LiveConfig.noteTypeJudgeData;
            _previousFrame = MusicScore.CurrentFrameInfo;
            LiveConfig.LoadMasterConfig();
            LiveConfig.UseLegacyJudgeTiming = false;
            _previousMouse = Mouse.current;
            _isolatedMouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(_isolatedMouse);
            if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            LiveConfig.UseLegacyJudgeTiming = _previousLegacyTiming;
            LiveConfig.noteTypeJudgeData = _previousJudgeData;
            MusicScore.CurrentFrameInfo = _previousFrame;
        }

        private sealed class Harness
        {
            public LiveLogic Logic;
            public FakeTouchSource Source;
        }


        private static MusicScore LoadScore()
        {
            var config = ScriptableObject.CreateInstance<LiveBundleBuildData>();
            var document = ChartJson.Read(System.IO.File.ReadAllText(ChartPath));
            return RuntimeChartConverter.Convert(document, config);
        }

        private static Harness Arrange(MusicScore score, NoteBase[] notes)
        {
            var logic = new LiveLogic(null);
            var source = new FakeTouchSource();
            logic.SetTouchSource(source);
            logic.Session.MusicScore = score;
            logic.Notes.Bind(notes);
            logic.Session.NoteDisplayTimeOffset = 1f;
            logic.Session.Views = Array.Empty<LiveViewBase>();
            return new Harness { Logic = logic, Source = source };
        }

        /// <summary>Mid point of the lane range a note ultimately judges on.</summary>
        private static float JudgeLane(NoteBase note) => (note.JudgeLaneStart + note.JudgeLaneEnd) * 0.5f;

        private static Vector3 LanePosition(float lane)
        {
            int low = Mathf.Clamp(Mathf.FloorToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 2);
            float t = Mathf.Clamp01(lane - low);
            return Vector3.Lerp(LiveConfig.JudgmentPositions[low], LiveConfig.JudgmentPositions[low + 1], t);
        }

        private static LiveTouchSample Touch(int id, Vector3 position, double time, InputTouchPhase phase, Vector2 delta) =>
            new LiveTouchSample(id, new Vector2(800f, 600f), position, time, phase, delta);

        private static string Describe(NoteBase note) =>
            $"{note.Category}/{note.GetType().Name} t={note.MusicScoreInfo.time:F3} lane={JudgeLane(note):F2} result={note.Result}";

        [Test]
        public void EveryPlayableTailOfADecorativeBarIsJudgedFromTheChart()
        {
            MusicScore score = LoadScore();
            LongNote[] bars = score.NoteArray.OfType<LongNote>()
                .Where(bar => !bar.HasJudgment && bar is not FrictionHideLongNote && bar.ChildNote is NoteBase tail && tail.HasJudgment)
                .OrderBy(bar => bar.MusicScoreInfo.time)
                .ToArray();

            Assert.That(bars.Length, Is.GreaterThan(0), "the chart must contain decorative bars with a playable end");

            var outcomes = new List<string>();
            var failures = new List<string>();

            foreach (LongNote bar in bars.Take(6))
            {
                NoteBase tail = (NoteBase)bar.ChildNote;
                Harness harness = Arrange(score, new NoteBase[] { bar });
                LiveLogic logic = harness.Logic;

                float tailTime = tail.MusicScoreInfo.time;
                float start = Mathf.Max(0f, bar.MusicScoreInfo.time - 0.1f);
                float from = JudgeLane(tail);
                Vector3 position = LanePosition(from);

                double last = 0d;
                for (float t = start; t <= tailTime + 0.02f; t += 1f / 240f)
                {
                    logic.OnUpdate(t, InputTime + t);
                    last = InputTime + t;
                }

                harness.Source.Clear();
                bool needsSwipe = tail.Category == NoteCategory.Flick || tail.Category == NoteCategory.FrictionFlick;
                if (needsSwipe)
                {
                    harness.Source.Add(Touch(1, position, last, InputTouchPhase.Began, Vector2.zero));
                    harness.Source.Add(Touch(1, position + new Vector3(-60f, 0f, 0f), last, InputTouchPhase.Moved, new Vector2(-60f, 0f)));
                }
                else
                {
                    harness.Source.Add(Touch(1, position, last, InputTouchPhase.Began, Vector2.zero));
                }

                logic.OnInput();
                harness.Source.Clear();
                for (float t = tailTime; t <= tailTime + 0.2f; t += 1f / 60f)
                {
                    logic.OnUpdate(t, InputTime + t);
                    harness.Source.Add(Touch(1, position, InputTime + t, InputTouchPhase.Stationary, Vector2.zero));
                    logic.OnInput();
                    harness.Source.Clear();
                }

                outcomes.Add(Describe(tail));
                if (tail.Result == NoteResult.None) failures.Add(Describe(tail));
            }

            Assert.That(failures, Is.Empty,
                "every playable end of a decorative bar has to be judged; outcomes: " + Environment.NewLine + string.Join(Environment.NewLine, outcomes));
        }
    }
}
