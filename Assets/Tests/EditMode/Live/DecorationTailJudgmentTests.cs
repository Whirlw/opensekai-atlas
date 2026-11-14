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
    }
}
