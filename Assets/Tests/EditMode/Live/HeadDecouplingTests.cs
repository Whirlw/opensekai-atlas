using System.Collections.Generic;
using NUnit.Framework;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    public sealed class HeadDecouplingTests
    {
        private LiveConfig.NoteTypeJudgeData _config;
        private MusicScoreInfo _frame;
        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);
        private static LiveTouch Touch(float time) => new LiveTouch { fingerId = 1, touchId = 1, musicTime = time, phase = TouchPhase.Began };

        private sealed class Bar : LongNote
        {
            internal Bar(bool flick = false) : base(Info(0), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0)
            {
                AddNoteListAndSetupChildNote(flick
                    ? (NoteBase)new FlickNote(Info(1), 2, 4, 5, NoteCategory.Flick, 1, NoteType.Default, NoteDirection.Left, 0, 0)
                    : new NormalNote(Info(10), 2, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
                Excute(Info(0), 0);
            }
            internal void Visual(NoteState value) => State = value;
        }

        [SetUp]
        public void SetUp()
        {
            _config = LiveConfig.noteTypeJudgeData;
            _frame = MusicScore.CurrentFrameInfo;
            LiveConfig.LoadMasterConfig();
            MusicScore.CurrentFrameInfo = Info(0);
        }

        [TearDown]
        public void TearDown()
        {
            LiveConfig.noteTypeJudgeData = _config;
            MusicScore.CurrentFrameInfo = _frame;
        }

        [Test]
        public void AHeldBarPublishesItsHeadOnce()
        {
            var bar = new Bar();
            int judgments = 0;
            var order = new List<string>();
            bar.OnDamage = n => order.Add("damage");
            bar.OnUpdateCombo = n => order.Add("combo");
            bar.OnJudgment = n => { judgments++; order.Add("judge"); };
            bar.OnUpdateScore = n => order.Add("score");
            var touch = Touch(0);
            bar.Judgment(ref touch, 4.5f);
            for (int i = 1; i <= 10; i++)
            {
                touch.musicTime = i / 60f;
                bar.Excute(Info(touch.musicTime), 0);
                bar.Judgment(ref touch, 4.5f);
                Assert.That(judgments, Is.EqualTo(1));
            }
            Assert.That(order, Is.EqualTo(new[] { "damage", "combo", "judge", "score" }));

            // Pool reuse must rearm the head, and AutoJudgment must not depend on the body setter.
            bar.ResetNote();
            bar.Visual(NoteState.Scrolling);
            bar.AutoJudgment(Info(0));
            bar.AutoJudgment(Info(0.02f));
            Assert.That(judgments, Is.EqualTo(2));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.Auto));

            foreach (bool hidden in new[] { false, true })
            {
                LongNote friction = hidden
                    ? new FrictionHideLongNote(Info(0), 3, 4, 5, NoteCategory.FrictionHideLong, NoteType.Default, null, 1)
                    : new FrictionLongNote(Info(0), 3, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1);
                friction.AddNoteListAndSetupChildNote(new NormalNote(Info(10), 4, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));
                int heads = 0;
                // Only an ordinary head borrows the bar as its visual object; a rub head speaks for
                // itself, so its judgement callback carries the head's identity, not the bar.
                friction.OnJudgment = n => { if (ReferenceEquals(n, friction.Head.Identity)) heads++; };
                touch = Touch(0);
                friction.Excute(Info(0), 0);
                friction.Judgment(ref touch, 4.5f);
                for (int i = 1; i <= 10; i++)
                {
                    friction.Excute(Info(i / 60f), 0);
                    friction.Judgment(ref touch, 4.5f);
                }
                Assert.That(heads, Is.EqualTo(hidden ? 0 : 1));
                Assert.That(friction.Result, Is.EqualTo(hidden ? NoteResult.Pass : NoteResult.JustPerfect));
            }
        }

        [Test]
        public void AJudgedHeadIsNotOverwrittenByTheGraceEnding()
        {
            var bar = new Bar();
            int judgments = 0;
            bar.OnJudgment = n => judgments++;
            var touch = Touch(0);
            bar.Judgment(ref touch, 4.5f);
            bar.Excute(Info(0.02f), 0); // The press frame remains lit.
            bar.Excute(Info(0.04f), 0); // No refresh: enter Last.
            Assert.That(bar.State, Is.EqualTo(NoteState.Last));
            bar.Excute(Info(0.06f), 0); // Leave Last through the real lifecycle.
            bar.Head.SettleExpiredHead(1f); // The head's own clock: it is already settled, so this is a no-op.
            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect));
            Assert.That(judgments, Is.EqualTo(1));

            // Any visual replay of the old publishing transition must also be silent.
            bar.Visual(NoteState.Last);
            bar.Visual(NoteState.InputBegan);
            Assert.That(judgments, Is.EqualTo(1));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.JustPerfect));
        }

        [Test]
        public void AnEarlySwipeAddsNoMiss()
        {
            var bar = new Bar(true);
            var headResults = new List<NoteResult>();
            var tailResults = new List<NoteResult>();
            bar.OnJudgment = n => { if (ReferenceEquals(n, bar)) headResults.Add(n.Result); else if (ReferenceEquals(n, bar.ChildNote)) tailResults.Add(n.Result); };
            var touch = Touch(0);
            bar.Judgment(ref touch, 4.5f);
            bar.Excute(Info(0.9f), 0);
            Assert.That(bar.TrySwipeTail(new Vector2(-60, 0), 1f / 60f, 0.9f), Is.False);
            Assert.That(bar.HasDeferredSwipe, Is.True);
            bar.Excute(Info(0.92f), 0);
            Assert.That(bar.State, Is.EqualTo(NoteState.Last));
            bar.NotifyLaneRelease(); // The input coordinator signals the lift separately from Excute.
            bar.TickSwipeTail(0.92f);
            bar.Excute(Info(0.94f), 0);
            bar.TickSwipeTail(0.94f);
            bar.TickSwipeTail(0.96f);
            bar.TickSwipeTail(0.98f);
            bar.TickSwipeTail(1f);
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(headResults, Is.EqualTo(new[] { NoteResult.JustPerfect }));
            Assert.That(tailResults.Count, Is.EqualTo(1));
            Assert.That(tailResults[0], Is.EqualTo(bar.ChildNote.Result));
            Assert.That(tailResults[0], Is.Not.EqualTo(NoteResult.Miss));
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.JustPerfect), "tail grading must not replace the owned head result");
        }

        [Test]
        public void ARerefreshKeepsTheBarLitWithoutRepublishing()
        {
            var bar = new Bar();
            int combos = 0;
            bar.OnUpdateCombo = n => combos++;
            var touch = Touch(0);
            bar.Judgment(ref touch, 4.5f);
            for (int i = 1; i <= 10; i++)
            {
                bar.Excute(Info(i / 60f), 0);
                bar.RefreshHeldInput(ref touch, 4.5f);
                LongNoteGraceTests.AssertHeldBody(bar);
                Assert.That(combos, Is.EqualTo(1));
            }
        }
        [Test]
        public void AnEndedBarLeavesItsHeadToItsOwnWindow()
        {
            // The head answers to its own window and to nothing else. A bar ended by its tail before the
            // head window closed used to publish the head's Miss on the way out; that made the bar's
            // lifecycle decide a result the head owns. Now the ending changes no head at all, and the
            // head settles the frame its own window closes.
            var bar = new Bar();
            int published = 0;
            bar.OnJudgment = n => { if (ReferenceEquals(n, bar)) published++; };
            bar.Visual(NoteState.Last); // the bar is past the point a press could still take it
            Assert.That(bar.Head.IsSettled, Is.False, "nothing has settled the head yet");

            bar.ForceTerminate((NoteBase)bar.ChildNote); // the tail ends the bar

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(bar.Head.IsSettled, Is.False, "ending the bar is not a verdict on its head");
            Assert.That(published, Is.Zero);

            bar.Head.SettleExpiredHead(1f); // the head's own window closes, one second past the bar

            Assert.That(bar.Head.IsSettled, Is.True);
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Miss), "a head nobody took is a Miss");
            Assert.That(published, Is.EqualTo(1), "and it is published once, from the head's own clock");
        }

        [Test]
        public void AnEndingAskedTwiceStillPublishesTheHeadOnce()
        {
            var bar = new Bar();
            int published = 0;
            bar.OnJudgment = n => { if (ReferenceEquals(n, bar)) published++; };
            bar.Visual(NoteState.Last);

            bar.ForceTerminate((NoteBase)bar.ChildNote);
            NoteResult headResult = bar.Head.Result;
            NoteResult barResult = bar.Result;

            bar.ForceTerminate((NoteBase)bar.ChildNote);
            bar.ForceTerminate();

            Assert.That(bar.Head.Result, Is.EqualTo(headResult), "a repeated ending must not rewrite the head");
            Assert.That(bar.Result, Is.EqualTo(barResult), "nor the bar's own result");
            Assert.That(published, Is.Zero, "an ending is not the head's verdict, so it publishes nothing");

            bar.Head.SettleExpiredHead(1f);
            Assert.That(published, Is.EqualTo(1), "the head's own clock publishes it exactly once");

            bar.Head.SettleExpiredHead(1.2f);
            Assert.That(published, Is.EqualTo(1), "however many times its window is ticked");
        }

        [Test]
        public void APressOnAFinishedBarChangesNothing()
        {
            // Answers §12.4-4 of tail-classes.md: TryJudge still refuses a bar that is already Done.
            // The refusal is redundant with IsJudgment (a Done bar is never a candidate again), so it
            // is kept as a second lock rather than removed for tidiness - what matters is that the
            // outcome is "nothing changes", which is what this pins.
            var bar = new Bar();
            bar.Visual(NoteState.Last);
            bar.ForceTerminate((NoteBase)bar.ChildNote);
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));

            var touch = Touch(0);
            bool judged = bar.Judgment(ref touch, 4.5f);

            Assert.That(judged, Is.False, "a finished bar takes no input");
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.None),
                "and nothing settles the head here: its own window does, on its own clock");
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
        }
    }
}
