using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// A decorative bar is only a moving lane. The note at its end is a real note of its own kind and
    /// has to be judgeable exactly like a free-standing one: pressed for an ordinary end, swiped for a
    /// flick, rubbed for a friction end. Each end type is driven through the real OnUpdate / OnInput
    /// loop once, so a difference between the types shows up here instead of only on device.
    /// <see cref="DecorationTailFromChartTests"/> covers the same promise on a converted chart.
    /// </summary>
    [TestFixture]
    public sealed class DecorationTailJudgmentTests
    {
        private const float Frame = 1f / 60f;
        private const double InputBase = 10d;

        private LiveHarness _harness;

        [SetUp]
        public void SetUp() => _harness = new LiveHarness();

        [TearDown]
        public void TearDown() => _harness.Dispose();

        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

        private static Vector3 Position =>
            Vector3.Lerp(LiveConfig.JudgmentPositions[4], LiveConfig.JudgmentPositions[5], 0.5f);

        private NoteBase ArrangeDecorativeBar(NoteBase tail)
        {
            var bar = new LongNote(Info(0f), 10, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
            bar.SetDecoration(true);
            bar.AddNoteListAndSetupChildNote(tail);
            _harness.Arrange(new MusicScore(new[] { bar.MusicScoreInfo },
                System.Array.Empty<EventBase>(), new NoteBase[] { bar }));
            Assert.That(tail.IsDecoration, Is.False, "the end of a decorative bar is a real note");
            return tail;
        }

        private void Step(float now, params LiveTouchSample[] touches) =>
            _harness.Step(now, InputBase + now, touches);

        private void Idle(float from, float until)
        {
            for (float now = from; now < until - Frame * 0.5f; now += Frame)
            {
                Step(now);
            }
        }

        private static LiveTouchSample Sample(TouchPhase phase, float now, float screenX = 800f) =>
            new LiveTouchSample(1, new Vector2(screenX, 600f), Position, InputBase + now, phase, Vector2.zero);

        /// <summary>A finger rests on the end from <paramref name="from"/> until just before <paramref name="until"/>.</summary>
        private void Rest(float from, float until)
        {
            Step(from, Sample(TouchPhase.Began, from));
            for (float now = from + Frame; now < until - Frame * 0.5f; now += Frame)
            {
                Step(now, Sample(TouchPhase.Stationary, now));
            }
        }

        /// <summary>A leftward drag starting at <paramref name="at"/>, then the finger leaves.</summary>
        private void SwipeLeft(float at)
        {
            Step(at, Sample(TouchPhase.Moved, at, 560f));
            Step(at + Frame, Sample(TouchPhase.Ended, at + Frame, 320f));
            Idle(at + Frame * 2f, at + 0.2f);
        }

        private static void AssertPlayed(NoteBase tail, string what) =>
            Assert.That(tail.Result, Is.Not.EqualTo(NoteResult.None).And.Not.EqualTo(NoteResult.Miss), what);

        [Test]
        public void AnOrdinaryEndOfADecorativeBarIsPressed()
        {
            NoteBase tail = ArrangeDecorativeBar(
                new NormalNote(Info(1f), 11, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
            Idle(0f, 1f);
            Step(1f, Sample(TouchPhase.Began, 1f));
            Step(1f + Frame, Sample(TouchPhase.Ended, 1f + Frame));
            AssertPlayed(tail, "the press has to grade the end note");
        }

        [Test]
        public void AFlickEndOfADecorativeBarIsSwiped()
        {
            NoteBase tail = ArrangeDecorativeBar(new FlickNote(Info(1f), 12, 4, 5,
                NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Left, 0f, 0f));
            Idle(0f, 1f - Frame);
            Step(1f - Frame, Sample(TouchPhase.Began, 1f - Frame));
            SwipeLeft(1f);
            AssertPlayed(tail, "the swipe has to grade the flick end");
        }

        [Test]
        public void AFrictionEndOfADecorativeBarIsRubbed()
        {
            NoteBase tail = ArrangeDecorativeBar(new FrictionNote(Info(1f), 13, 4, 5,
                NoteCategory.Friction, null, NoteType.Default, 1f));
            Idle(0f, 0.9f);
            Rest(0.9f, 1.2f);
            AssertPlayed(tail, "a finger on the friction end has to grade it");
        }

        [Test]
        public void AFrictionFlickEndOfADecorativeBarIsSwiped()
        {
            NoteBase tail = ArrangeDecorativeBar(new FrictionFlickNote(Info(1f), 14, 4, 5,
                NoteCategory.FrictionFlick, 1f, NoteType.Default, NoteDirection.Left, 0f, 0f));
            Idle(0f, 0.9f);
            Rest(0.9f, 1f);
            SwipeLeft(1f);
            AssertPlayed(tail, "the swipe has to grade the friction flick end");
        }

        // Controls: the cases above must be graded by the gesture, not by the scaffolding.

        [Test]
        public void AnUntouchedEndOfADecorativeBarIsNotPlayed()
        {
            NoteBase tail = ArrangeDecorativeBar(
                new NormalNote(Info(1f), 15, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
            Idle(0f, 1.5f);
            Assert.That(tail.Result, Is.EqualTo(NoteResult.None).Or.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void AFlickEndOfADecorativeBarIsNotPlayedByAPressAlone()
        {
            NoteBase tail = ArrangeDecorativeBar(new FlickNote(Info(1f), 16, 4, 5,
                NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Left, 0f, 0f));
            Idle(0f, 1f - Frame);
            Rest(1f - Frame, 1.1f);
            Step(1.1f, Sample(TouchPhase.Ended, 1.1f));
            Idle(1.1f + Frame, 1.5f);
            Assert.That(tail.Result, Is.EqualTo(NoteResult.None).Or.EqualTo(NoteResult.Miss));
        }

        // A decorative note is not judged, exactly like a guide bar: no effect, no sound, no text, no score.

        private static NoteBase Decorated(NoteBase note)
        {
            note.SetDecoration(true);
            return note;
        }

        private static System.Collections.Generic.IEnumerable<TestCaseData> Flags()
        {
            yield return new TestCaseData(new NormalNote(Info(1f), 1, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f), true, true).SetName("Flags_Normal");
            yield return new TestCaseData(new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f), true, true).SetName("Flags_Long");
            yield return new TestCaseData(Decorated(new NormalNote(Info(1f), 1, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f)), false, true).SetName("Flags_DecorativeNormal");
            yield return new TestCaseData(Decorated(new FlickNote(Info(1f), 1, 4, 5, NoteCategory.Flick, 1f, NoteType.Default, NoteDirection.Left, 0f, 0f)), false, true).SetName("Flags_DecorativeFlick");
            yield return new TestCaseData(Decorated(new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f)), false, true).SetName("Flags_DecorativeLong");
            yield return new TestCaseData(new GuideNote(Info(1f), 1, 4, 5, NoteCategory.Guide, NoteType.Default, null, 1f), false, true).SetName("Flags_Guide");
            yield return new TestCaseData(new FrictionHideLongNote(Info(1f), 1, 4, 5, NoteCategory.FrictionHideLong, NoteType.Default, null, 1f), false, true).SetName("Flags_RubCarrier");
            yield return new TestCaseData(new HiddenConnectionNote(Info(1f), 1, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f), false, false).SetName("Flags_HiddenConnection");
            yield return new TestCaseData(new GuideHiddenConnectionNote(Info(1f), 1, 4, 5, NoteCategory.GuideHidden, null, NoteType.Default, 1f), false, false).SetName("Flags_GuideHiddenConnection");
            yield return new TestCaseData(new FrictionHideNote(Info(1f), 1, 4, 5, NoteCategory.Hidden, null, NoteType.Default, 1f, 0), false, false).SetName("Flags_FrictionHide");
            yield return new TestCaseData(new GuideEndNote(Info(1f), 1, 4, 5, NoteCategory.GuideEnd, null, NoteType.Default, 1f), false, false).SetName("Flags_GuideEnd");
        }

        [TestCaseSource(nameof(Flags))]
        public void EveryNoteAnswersTheTwoFlags(NoteBase note, bool judged, bool visible)
        {
            Assert.That(note.HasJudgment, Is.EqualTo(judged), "judged");
            Assert.That(note.IsVisible, Is.EqualTo(visible), "visible");
        }

        [Test]
        public void ADecorativeTapRetiresWithoutEffectSoundOrText()
        {
            var tap = Decorated(new NormalNote(Info(0.5f), 20, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
            _harness.Arrange(new MusicScore(new[] { Info(0f) }, System.Array.Empty<EventBase>(), new NoteBase[] { tap }));
            var recorder = new GameObject("decoration-recorder").AddComponent<RecordingView>();
            try
            {
                _harness.BindViews(recorder);
                Idle(0f, 1f);
            }
            finally
            {
                Object.DestroyImmediate(recorder.gameObject);
            }

            Assert.That(tap.State, Is.EqualTo(NoteState.Done), "it still leaves when its time has come");
            Assert.That(tap.Result, Is.EqualTo(NoteResult.Pass), "it is passed, not graded");
            Assert.That(recorder.Judged, Is.Empty, "no effect and no sound for a note nobody played");
            Assert.That(recorder.Texts, Is.Empty, "no judgement text");
        }

        [Test]
        public void ADecorativeBarPlaysNoHeadEffect()
        {
            var bar = Decorated(new LongNote(Info(0.5f), 21, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f));
            ((LongNote)bar).AddNoteListAndSetupChildNote(new NormalNote(Info(1.5f), 22, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
            _harness.Arrange(new MusicScore(new[] { Info(0f) }, System.Array.Empty<EventBase>(), new NoteBase[] { bar }));
            var recorder = new GameObject("decoration-recorder").AddComponent<RecordingView>();
            try
            {
                _harness.BindViews(recorder);
                Idle(0f, 1f);
            }
            finally
            {
                Object.DestroyImmediate(recorder.gameObject);
            }

            Assert.That(bar.Result, Is.EqualTo(NoteResult.Pass), "the head is passed silently");
            Assert.That(recorder.Judged, Has.No.Member(bar), "the head of a decorative bar plays nothing");
        }

        private sealed class RecordingView : LiveViewBase
        {
            internal readonly System.Collections.Generic.List<NoteBase> Judged = new();
            internal readonly System.Collections.Generic.List<NoteBase> Texts = new();

            public override void JudgmentNote(NoteBase note)
            {
                if (note != null) Judged.Add(note);
            }

            public override void JudgmentText(NoteBase note)
            {
                if (note != null) Texts.Add(note);
            }
        }
    }
}
