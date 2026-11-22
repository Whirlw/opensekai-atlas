using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// The picture's lead over the logic clock (visual latency plus judge offset) reaches the keys
    /// through <see cref="LiveConfig.ViewProgressOf"/>: a travelling key is drawn ahead, a pinned key stays
    /// on the line, and nothing the logic computes moves.
    /// </summary>
    public sealed class ViewLeadTests
    {
        private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

        [TearDown]
        public void TearDown() => LiveConfig.ViewLeadSeconds = 0f;

        private static NormalNote TravellingNote(float now)
        {
            var note = new NormalNote(Info(1f), 1, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
            note.Excute(Info(now), 1f);
            return note;
        }

        [Test]
        public void WithoutALeadTheKeyIsDrawnAtItsLogicProgress()
        {
            NormalNote note = TravellingNote(0.5f);
            Assert.That(LiveConfig.ViewProgressOf(note), Is.EqualTo(note.Progress));
        }

        [Test]
        public void ALeadDrawsATravellingKeyAheadAndLeavesTheLogicAlone()
        {
            NormalNote note = TravellingNote(0.5f);
            float logic = note.Progress;
            LiveConfig.ViewLeadSeconds = 0.04f;

            Assert.That(LiveConfig.ViewProgressOf(note), Is.EqualTo(logic + 0.04f).Within(1e-5f));
            Assert.That(note.Progress, Is.EqualTo(logic), "the logic progress is not touched");
        }

        [TestCase(1.02f, -0.04f, 0.98f)]
        [TestCase(0.98f, 0.04f, 1.02f)]
        public void AScrollingKeyCrossesOnDisplayTime(float now, float lead, float expected)
        {
            NormalNote note = TravellingNote(now);
            LiveConfig.ViewLeadSeconds = lead;
            Assert.That(LiveConfig.ViewProgressOf(note, now), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void ALeadDoesNotPushAPinnedKeyPastTheLine()
        {
            var host = new GameObject("lead key");
            try
            {
                var view = host.AddComponent<LongNoteView>();
                var bar = new LongNote(Info(0f), 2, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
                bar.AddNoteListAndSetupChildNote(new NormalNote(Info(2f), 3, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f));
                bar.Excute(Info(0.5f), 0.5f);
                bar.SetStateUnnotice(NoteState.Input);
                view.Spawn(bar, 0f);

                view.Move(0.5f);
                Vector3 pinned = view.transform.localPosition;

                LiveConfig.ViewLeadSeconds = 0.05f;
                view.Move(0.5f);
                Assert.That(view.transform.localPosition, Is.EqualTo(pinned), "a pinned head stays where it is");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
