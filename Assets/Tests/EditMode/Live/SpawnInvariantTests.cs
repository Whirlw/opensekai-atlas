using NUnit.Framework;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// The "pool handed out a dirty note" invariant fired on every spawned note, because the State
    /// setter assigns Scrolling before it calls OnSpawnNote and the check demanded Wait. These pin
    /// the check to what a clean spawn actually looks like and keep it quiet on a normal run and a retry.
    /// </summary>
    public sealed class SpawnInvariantTests
    {
        private LiveConfig.NoteTypeJudgeData _previousJudge;
        private MusicScoreInfo _previousFrame;
        private bool _previousEnabled;
        private LiveTraceCategory _previousCategories;

        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

        [SetUp]
        public void SetUp()
        {
            _previousJudge = LiveConfig.noteTypeJudgeData;
            _previousFrame = MusicScore.CurrentFrameInfo;
            _previousEnabled = LiveTrace.Enabled;
            _previousCategories = LiveTrace.Categories;
            MusicScore.CurrentFrameInfo = Info(0);
            LiveConfig.LoadMasterConfig();
            LiveTrace.Enabled = true;
            LiveTrace.Categories = LiveTraceCategory.All;
            LiveTrace.ClearBuffer();
        }

        [TearDown]
        public void TearDown()
        {
            LiveConfig.noteTypeJudgeData = _previousJudge;
            MusicScore.CurrentFrameInfo = _previousFrame;
            LiveTrace.Enabled = _previousEnabled;
            LiveTrace.Categories = _previousCategories;
            LiveTrace.ClearBuffer();
        }

        [Test]
        public void ACleanSpawnIsScrollingWithNoResult()
        {
            Assert.That(NoteBase.IsDirtyOnSpawn(NoteState.Scrolling, NoteResult.None), Is.False);
        }

        [TestCase(NoteState.Wait)]
        [TestCase(NoteState.Last)]
        [TestCase(NoteState.Done)]
        public void ASpawnInAnyOtherStateIsDirty(NoteState state)
        {
            Assert.That(NoteBase.IsDirtyOnSpawn(state, NoteResult.None), Is.True);
        }

        [Test]
        public void ASpawnCarryingAResultIsDirty()
        {
            Assert.That(NoteBase.IsDirtyOnSpawn(NoteState.Scrolling, NoteResult.Perfect), Is.True);
        }

        [Test]
        public void SpawningAndRespawningAfterResetLogsNoInvariant()
        {
            var tap = new NormalNote(Info(1f), 1, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0);
            var bar = new LongNote(Info(1f), 2, 4, 5, NoteCategory.Long, NoteType.Default, 1, 0);
            bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 3, 4, 5, NoteCategory.Normal, 1, NoteType.Default, 0));

            tap.Excute(Info(0.5f), 1f);
            bar.Excute(Info(0.5f), 1f);
            Assert.That(tap.State, Is.EqualTo(NoteState.Scrolling), "the tap must actually spawn for this test to mean anything");
            Assert.That(bar.State, Is.EqualTo(NoteState.Scrolling), "the bar must actually spawn for this test to mean anything");

            tap.ResetNote();
            bar.ResetNote();
            tap.Excute(Info(0.5f), 1f);
            bar.Excute(Info(0.5f), 1f);

            StringAssert.DoesNotContain("dirty note", LiveTrace.Recent(200));
        }
    }
}
