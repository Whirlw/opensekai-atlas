using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// What a verdict publishes, in which order, and how often: the four callbacks a judge point
    /// reaches (judgement, combo, score, damage), the base score each score callback consumes, and the
    /// running totals. Every scenario runs through the real OnUpdate / OnInput loop with the scheduler's
    /// own callbacks, and the score board records what reached it.
    ///
    /// The orders are per kind and are pinned as they are: an ordinary note and a tail publish
    /// judgement, combo, score, damage; an interior point publishes score, combo, damage, judgement; a
    /// head publishes damage, combo, judgement (on its bar), score, and a head that timed out skips the
    /// score callback, so it does not consume a base score.
    /// </summary>
    public sealed class ResultPublicationTests
    {
        private const double Clock = 10d;
        private LiveHarness _harness;
        private Recorder _board;
        private readonly Dictionary<object, string> _labels = new Dictionary<object, string>();

        /// <summary>The score board, recording each call and the base score index it moved.</summary>
        private sealed class Recorder : ScoreLogic
        {
            private static readonly FieldInfo ScoredCount =
                typeof(ScoreLogic).GetField("_scoredNoteCount", BindingFlags.Instance | BindingFlags.NonPublic);

            internal readonly List<string> Log = new List<string>();
            internal Func<NoteBase, string> Label = note => note?.GetType().Name;
            internal Action<NoteBase> OnJudged;

            internal Recorder() : base(null) { }

            public override void UpdateNoteResult(NoteBase note)
            {
                Log.Add($"J {Label(note)} {note.Result}");
                base.UpdateNoteResult(note);
                OnJudged?.Invoke(note);
            }

            public override void UpdateCombo(NoteBase note)
            {
                Log.Add($"C {Label(note)} {note.Result}");
                base.UpdateCombo(note);
            }

            public override int CalculateAddScore(NoteBase note, float factor = 1f)
            {
                int before = (int)ScoredCount.GetValue(this);
                int add = base.CalculateAddScore(note, factor);
                int after = (int)ScoredCount.GetValue(this);
                Log.Add($"S {Label(note)} {note.Result} #{before}->{after} +{add}");
                return add;
            }

            public override void Damage(NoteBase note)
            {
                Log.Add($"D {Label(note)} {note.Result}");
                base.Damage(note);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _harness = new LiveHarness();
            _labels.Clear();
        }

        [TearDown]
        public void TearDown() => _harness.Dispose();

        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120f, 4f, 1f, 1f);

        private static Vector3 At(float lane)
        {
            int left = Mathf.FloorToInt(lane);
            Vector2 a = LiveConfig.JudgmentPositions[left];
            Vector2 b = LiveConfig.JudgmentPositions[Mathf.Min(left + 1, LiveConfig.JudgmentPositions.Length - 1)];
            return Vector2.Lerp(a, b, lane - left);
        }

        private static LiveTouchSample Sample(int id, float lane, float time, InputTouchPhase phase, float screenX = 800f) =>
            new LiveTouchSample(id, new Vector2(screenX, 600f), At(lane), Clock + time, phase, Vector2.zero);

        private T Named<T>(T note, string label) where T : class
        {
            _labels[note] = label;
            return note;
        }

        private Recorder Arrange(bool auto, params NoteBase[] notes)
        {
            var score = new MusicScore(new[] { Info(0f) }, Array.Empty<EventBase>(), notes);
            _harness.Arrange(score, auto: auto);
            _board = new Recorder { Label = note => note != null && _labels.TryGetValue(note, out string name) ? name : note?.GetType().Name };
            _harness.Logic.scoreLogic = _board;
            _board.Setup(null, score);
            _harness.BindViews();
            return _board;
        }

        private void Frame(float time, params LiveTouchSample[] touches) => _harness.Step(time, Clock + time, touches);

        private void AutoFrame(float time)
        {
            _harness.OnUpdate(time, Clock + time);
            _harness.Logic.OnAutoInput();
            _harness.OnView();
        }

        /// <summary>Holds finger 1 on lane 4.5 from <paramref name="from"/> to <paramref name="to"/>, every 20 ms.</summary>
        private void Hold(float from, float to)
        {
            for (float t = from; t < to - 1e-4f; t += 0.02f)
            {
                Frame(t, Sample(1, 4.5f, t, InputTouchPhase.Stationary));
            }
        }

        private LongNote Bar(NoteBase end, float head = 0.5f)
        {
            var bar = Named(new LongNote(Info(head), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f), "bar");
            bar.AddNoteListAndSetupChildNote(end);
            Named(bar.Head.Identity, "head");
            return bar;
        }

        private NormalNote Tail(float time = 1.5f) =>
            Named(new NormalNote(Info(time), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f), "tail");

        private static string[] Of(Recorder board, string label) =>
            board.Log.Where(entry => entry.Split(' ')[1] == label).ToArray();

        [Test]
        public void APressedKeyPublishesJudgementComboScoreDamageOnce()
        {
            var tap = Named(new NormalNote(Info(0.5f), 1, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f), "tap");
            Recorder board = Arrange(false, tap);

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4f, 0.5f, InputTouchPhase.Began));
            Frame(0.6f);
            Frame(1.0f);

            Assert.That(board.Log, Is.EqualTo(new[]
            {
                "J tap JustPerfect",
                "C tap JustPerfect",
                "S tap JustPerfect #0->1 +1000000",
                "D tap JustPerfect",
            }));
            Assert.That(board.Score.totalScore, Is.EqualTo(1000000));
            Assert.That(board.Score.combo, Is.EqualTo(1));
            Assert.That(board.Score.life, Is.EqualTo(1000));
        }

        [Test]
        public void AKeyThatTimesOutPublishesAMissWithItsScoreCallback()
        {
            var tap = Named(new NormalNote(Info(0.5f), 1, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f), "tap");
            Recorder board = Arrange(false, tap);

            Frame(0.4f);
            Frame(0.7f);
            Frame(1.0f);

            Assert.That(board.Log, Is.EqualTo(new[]
            {
                "J tap Miss",
                "C tap Miss",
                "S tap Miss #0->1 +0",
                "D tap Miss",
            }));
            Assert.That(board.Score.combo, Is.Zero);
            Assert.That(board.Score.life, Is.EqualTo(1000 - LiveConfig.Damages[NoteResult.Miss]));
        }

        [Test]
        public void AHeldBarPublishesItsHeadOnTheBarAndItsTailOnce()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            Recorder board = Arrange(false, bar);

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4.5f, 0.5f, InputTouchPhase.Began));
            Hold(0.52f, 1.5f);
            Frame(1.5f, Sample(1, 4.5f, 1.5f, InputTouchPhase.Ended));
            Frame(1.6f);
            Frame(2.0f);

            Assert.That(board.Log, Is.EqualTo(new[]
            {
                "D head JustPerfect",
                "C head JustPerfect",
                "J bar JustPerfect",
                "S head JustPerfect #0->1 +500000",
                "J tail JustPerfect",
                "C tail JustPerfect",
                "S tail JustPerfect #1->2 +500000",
                "D tail JustPerfect",
            }));
            Assert.That(board.Score.totalScore, Is.EqualTo(1000000));
            Assert.That(board.Score.combo, Is.EqualTo(2));
        }

        /// <summary>
        /// A head that times out publishes damage, combo and judgement but no score callback, so it does
        /// not consume a base score: the next score callback takes the index the head would have taken.
        /// </summary>
        [Test]
        public void AHeadThatTimesOutSkipsTheScoreCallbackAndItsBaseScore()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            var tap = Named(new NormalNote(Info(2.0f), 3, 8, 8, NoteCategory.Normal, 1f, NoteType.Default, 0f), "tap");
            Recorder board = Arrange(false, bar, tap);

            for (float t = 0.4f; t < 2.0f - 1e-4f; t += 0.05f) Frame(t);
            Frame(2.0f, Sample(2, 8f, 2.0f, InputTouchPhase.Began));
            Frame(2.1f);

            Assert.That(Of(board, "head"), Is.EqualTo(new[] { "D head Miss", "C head Miss" }));
            Assert.That(Of(board, "bar"), Is.EqualTo(new[] { "J bar Miss" }));
            Assert.That(Of(board, "tail"), Is.EqualTo(new[]
            {
                "J tail Miss", "C tail Miss", "S tail Miss #0->1 +0", "D tail Miss",
            }));
            Assert.That(Of(board, "tap"), Is.EqualTo(new[]
            {
                "J tap JustPerfect", "C tap JustPerfect", "S tap JustPerfect #1->2 +333334", "D tap JustPerfect",
            }));
            Assert.That(board.Log.Take(3), Is.EqualTo(new[] { "D head Miss", "C head Miss", "J bar Miss" }),
                "the head is the first verdict on the board");
        }

        /// <summary>
        /// Generated interior points share one chart id, so the id cannot tell them apart; each still
        /// publishes once, as itself.
        /// </summary>
        [Test]
        public void InteriorPointsPublishScoreComboDamageJudgementEachOnceEvenWithOneSharedId()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            var first = Named(new LongHoldCombo(Info(0.8f), NoteType.Default, 1f, 0f), "p1");
            var second = Named(new LongHoldCombo(Info(1.1f), NoteType.Default, 1f, 0f), "p2");
            bar.AddHoldCombo(first);
            bar.AddHoldCombo(second);
            bar.SortNoteList();
            Assert.That(first.Id, Is.EqualTo(second.Id), "Precondition: generated points share an id.");
            Recorder board = Arrange(false, bar);

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4.5f, 0.5f, InputTouchPhase.Began));
            Hold(0.52f, 1.5f);
            Frame(1.5f, Sample(1, 4.5f, 1.5f, InputTouchPhase.Ended));
            Frame(2.0f);

            string[] p1 = Of(board, "p1");
            string[] p2 = Of(board, "p2");
            Assert.That(p1.Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "S", "C", "D", "J" }), string.Join(" | ", board.Log));
            Assert.That(p2.Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "S", "C", "D", "J" }), string.Join(" | ", board.Log));
            Assert.That(Of(board, "tail"), Has.Length.EqualTo(4));
            Assert.That(board.Log.IndexOf(p1[0]), Is.LessThan(board.Log.IndexOf(p2[0])));
            Assert.That(board.Log.IndexOf(p2[3]), Is.LessThan(board.Log.IndexOf(Of(board, "tail")[0])));
            Assert.That(board.Score.totalScore, Is.EqualTo(1000000));
            Assert.That(board.Score.combo, Is.EqualTo(4));
        }

        [Test]
        public void APointTheBodyNeverReachedIsPublishedPerfectAfterTheTailThatEndedIt()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            var late = Named(new LongHoldCombo(Info(1.45f), NoteType.Default, 1f, 0f), "late");
            bar.AddHoldCombo(late);
            bar.SortNoteList();
            Recorder board = Arrange(false, bar);

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4.5f, 0.5f, InputTouchPhase.Began));
            Hold(0.52f, 1.4f);
            Frame(1.4f, Sample(1, 4.5f, 1.4f, InputTouchPhase.Ended));
            Frame(1.6f);
            Frame(2.0f);

            string[] tailEntries = Of(board, "tail");
            Assert.That(tailEntries, Has.Length.EqualTo(4), string.Join(" | ", board.Log));
            Assert.That(Of(board, "late"), Is.EqualTo(new[]
            {
                "S late Perfect #2->3 +333333", "C late Perfect", "D late Perfect", "J late Perfect",
            }), string.Join(" | ", board.Log));
            Assert.That(board.Log.IndexOf(tailEntries[3]), Is.LessThan(board.Log.IndexOf("S late Perfect #2->3 +333333")),
                "the tail's verdict ends the bar, and the bar's end settles the point");
        }

        [Test]
        public void AHiddenEndPublishesNothing()
        {
            var end = Named(new HiddenConnectionNote(Info(1.5f), 2, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f), "hidden");
            LongNote bar = Bar(end);
            Recorder board = Arrange(false, bar);

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4.5f, 0.5f, InputTouchPhase.Began));
            Hold(0.52f, 1.5f);
            Frame(1.5f, Sample(1, 4.5f, 1.5f, InputTouchPhase.Ended));
            Frame(2.0f);

            Assert.That(Of(board, "hidden"), Is.Empty);
            Assert.That(board.Log, Is.EqualTo(new[]
            {
                "D head JustPerfect", "C head JustPerfect", "J bar JustPerfect", "S head JustPerfect #0->1 +1000000",
            }));
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
        }

        [Test]
        public void ALateLiftAfterTheBodyEndedPublishesTheTailOnce()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            Recorder board = Arrange(false, bar);

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4.5f, 0.5f, InputTouchPhase.Began));
            Hold(0.52f, 1.6f);
            Frame(1.6f, Sample(1, 4.5f, 1.6f, InputTouchPhase.Ended));
            for (float t = 1.62f; t < 2.2f; t += 0.02f) Frame(t);

            string[] entries = Of(board, "tail");
            Assert.That(entries.Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "J", "C", "S", "D" }), string.Join(" | ", board.Log));
            Assert.That(tail.Result, Is.GreaterThan(NoteResult.Miss).And.LessThan(NoteResult.Perfect),
                "a lift 100 ms late is graded late");
            Assert.That(Of(board, "head"), Has.Length.EqualTo(3));
        }

        /// <summary>
        /// An early swipe on a flick tail settles on the third game frame after the release. When that
        /// countdown crosses the body's geometric end, the tail still publishes once.
        /// </summary>
        [Test]
        public void AnEarlySwipeWhoseCountdownCrossesTheBodyEndPublishesOnce()
        {
            var flick = Named(new FlickNote(Info(1.5f), 2, 4, 5, NoteCategory.Flick, 1f, NoteType.Default,
                NoteDirection.Default, 0f, 0f), "tail");
            LongNote bar = Bar(flick);
            Recorder board = Arrange(false, bar);
            float judgedAt = -1f;
            board.OnJudged = note => { if (ReferenceEquals(note, flick)) judgedAt = _harness.Logic.currentFrameInfo.time; };

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4.5f, 0.5f, InputTouchPhase.Began));
            Hold(0.52f, 1.44f);
            Frame(1.44f, Sample(1, 4.5f, 1.44f, InputTouchPhase.Moved, 740f));
            Frame(1.47f, Sample(1, 4.5f, 1.47f, InputTouchPhase.Ended, 740f));
            for (float t = 1.49f; t < 1.8f; t += 0.02f) Frame(t);

            Assert.That(Of(board, "tail").Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "J", "C", "S", "D" }),
                string.Join(" | ", board.Log));
            Assert.That(flick.Result, Is.GreaterThan(NoteResult.Miss));
            Assert.That(judgedAt, Is.GreaterThan(1.5f), "the countdown ran past the body's end");
        }

        [Test]
        public void AutoplayPublishesTheSameShapes()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            var tap = Named(new NormalNote(Info(2.0f), 3, 8, 8, NoteCategory.Normal, 1f, NoteType.Default, 0f), "tap");
            Recorder board = Arrange(true, bar, tap);

            for (float t = 0.4f; t < 2.3f; t += 0.02f) AutoFrame(t);

            Assert.That(Of(board, "head").Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "D", "C", "S" }), string.Join(" | ", board.Log));
            Assert.That(Of(board, "bar").Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "J" }));
            Assert.That(Of(board, "tail").Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "J", "C", "S", "D" }));
            Assert.That(Of(board, "tap").Select(e => e.Substring(0, 1)), Is.EqualTo(new[] { "J", "C", "S", "D" }));
            Assert.That(board.Log.Where(e => e.StartsWith("J")).All(e => e.EndsWith(" Auto")), Is.True, string.Join(" | ", board.Log));
            Assert.That(board.Score.totalScore, Is.EqualTo(1000000));
            Assert.That(board.Score.autoCount, Is.EqualTo(3));
        }

        [Test]
        public void ARestartPublishesEveryPointOnceAgain()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            Recorder board = Arrange(false, bar);

            // A retry starts the music again from zero while the game clock keeps running.
            void Play(double gameOffset, int finger)
            {
                void At(float t, params LiveTouchSample[] touches) => _harness.Step(t, Clock + gameOffset + t, touches);
                LiveTouchSample Touch(float t, InputTouchPhase phase) =>
                    new LiveTouchSample(finger, new Vector2(800f, 600f), ResultPublicationTests.At(4.5f), Clock + gameOffset + t, phase, Vector2.zero);
                At(0.4f);
                At(0.5f, Touch(0.5f, InputTouchPhase.Began));
                for (float t = 0.52f; t < 1.5f - 1e-4f; t += 0.02f) At(t, Touch(t, InputTouchPhase.Stationary));
                At(1.5f, Touch(1.5f, InputTouchPhase.Ended));
                At(2.0f);
            }

            Play(0d, 1);
            Assert.That(board.Log, Has.Count.EqualTo(8));
            string[] firstTake = board.Log.ToArray();

            // What a retry does: the take is open again, the cursors and tick lists go, every note is reset.
            _harness.Logic.result = 0;
            _harness.Logic.Notes.Reset();
            _harness.Logic.Session.MusicScore.ResetNote();
            board.Setup(null, _harness.Logic.Session.MusicScore);
            board.Log.Clear();

            Play(100d, 2);
            Assert.That(board.Log, Is.EqualTo(firstTake));
        }

        [Test]
        public void ThePublicationMarkIsPerPointAndOnlyAResetClearsIt()
        {
            var first = new LongHoldCombo(Info(0.5f), NoteType.Default, 1f, 0f);
            var second = new LongHoldCombo(Info(0.5f), NoteType.Default, 1f, 0f);
            int judged = 0;
            var callbacks = new ResultCallbacks(_ => judged++, null, null, null);

            Assert.That(ResultPublication.Publish(first, callbacks, first, PublishShape.InteriorPoint), Is.True);
            Assert.That(ResultPublication.Publish(first, callbacks, first, PublishShape.InteriorPoint), Is.False,
                "a point publishes once");
            Assert.That(ResultPublication.Publish(second, callbacks, second, PublishShape.InteriorPoint), Is.True,
                "another point with the same id is another point");
            Assert.That(judged, Is.EqualTo(2));

            first.ResetNote();
            Assert.That(first.HasPublishedResult, Is.False);
            Assert.That(ResultPublication.Publish(first, callbacks, first, PublishShape.InteriorPoint), Is.True,
                "a new take publishes again");
            Assert.That(judged, Is.EqualTo(3));
        }

        [Test]
        public void TheVisualObjectReceivesOnlyTheJudgementCallback()
        {
            var point = new NoteBase(Info(0.5f), 1, 4, 4, NoteCategory.Normal, 1f, 0f);
            var visual = new NoteBase(Info(0.5f), 2, 4, 4, NoteCategory.Normal, 1f, 0f);
            var seen = new List<string>();
            var callbacks = new ResultCallbacks(
                n => seen.Add("J" + n.Id), n => seen.Add("C" + n.Id), n => seen.Add("S" + n.Id), n => seen.Add("D" + n.Id));

            ResultPublication.Publish(point, callbacks, visual, PublishShape.Head);
            Assert.That(seen, Is.EqualTo(new[] { "D1", "C1", "J2", "S1" }));
        }

        [Test]
        public void AHeadSettledTwiceOutsideTheLoopPublishesOnce()
        {
            LongNote bar = Bar(Tail());
            int judged = 0, scored = 0;
            bar.OnJudgment = _ => judged++;
            bar.OnUpdateScore = _ => scored++;

            bar.Head.Settle((NoteResult.Great, NoteResultDescription.None));
            bar.Head.Settle((NoteResult.Miss, NoteResultDescription.None), updateScore: false);
            Assert.That(judged, Is.EqualTo(1));
            Assert.That(scored, Is.EqualTo(1));
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Great));

            bar.ResetNote();
            bar.Head.Settle((NoteResult.Miss, NoteResultDescription.None), updateScore: false);
            Assert.That(judged, Is.EqualTo(2), "a reset head publishes again");
            Assert.That(scored, Is.EqualTo(1), "and a timeout still skips the score callback");
        }

        [Test]
        public void ASettlementReenteredFromACallbackPublishesNothingMore()
        {
            NormalNote tail = Tail();
            LongNote bar = Bar(tail);
            var tap = Named(new NormalNote(Info(0.5f), 3, 8, 8, NoteCategory.Normal, 1f, NoteType.Default, 0f), "tap");
            Recorder board = Arrange(false, bar, tap);
            board.OnJudged = note =>
            {
                if (ReferenceEquals(note, bar)) bar.Head.Settle((NoteResult.Miss, NoteResultDescription.None));
                if (ReferenceEquals(note, tap)) tap.FinishWith(NoteResult.Miss);
                if (ReferenceEquals(note, tail)) tail.FinishWith(NoteResult.Miss);
            };

            Frame(0.4f);
            Frame(0.5f, Sample(1, 4.5f, 0.5f, InputTouchPhase.Began), Sample(2, 8f, 0.5f, InputTouchPhase.Began));
            Hold(0.52f, 1.5f);
            Frame(1.5f, Sample(1, 4.5f, 1.5f, InputTouchPhase.Ended));
            Frame(2.0f);

            Assert.That(Of(board, "head"), Has.Length.EqualTo(3), string.Join(" | ", board.Log));
            Assert.That(Of(board, "bar"), Has.Length.EqualTo(1));
            Assert.That(Of(board, "tap"), Has.Length.EqualTo(4));
            Assert.That(Of(board, "tail"), Has.Length.EqualTo(4));
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(tap.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(tail.Result, Is.EqualTo(NoteResult.JustPerfect));
        }
    }
}
