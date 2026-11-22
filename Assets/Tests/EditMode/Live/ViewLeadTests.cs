using NUnit.Framework;
using OpenSekai.Charts;
using OpenSekai.Shared.Live.Simulation;
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
            Assert.That(LiveConfig.ViewProgressOf(note, now + lead), Is.EqualTo(expected).Within(1e-5f));
        }

        [TestCase(false, 100f)]
        [TestCase(false, -100f)]
        [TestCase(true, 100f)]
        [TestCase(true, -100f)]
        public void HoldMeshAndKeysTravelTogetherOnTheDisplayClock(bool native, float visualLatencyMs)
        {
            const float TIMELINE = 0.8f;
            var timing = new TimingCompensation(0f, visualLatencyMs, 30f);
            float viewTime = timing.ViewTime(TIMELINE);
            var frame = Info(timing.LogicTime(TIMELINE));
            var bar = new LongNote(Info(1f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
            var tail = new NormalNote(Info(1.3f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
            if (native)
            {
                var scroll = new NextRushScrollTimeline(System.Array.Empty<NextRushScrollTimeline.Change>(), 0);
                bar.SetNextRushScroll(scroll, 1f);
                tail.SetNextRushScroll(scroll, 1.3f);
                bar.NativeConnector = new NextRushConnectorSegment(1, 1, 1f, 1f);
                bar.PrepareNativeVisual(1f);
                tail.PrepareNativeVisual(1f);
            }
            bar.AddNoteListAndSetupChildNote(tail);
            bar.CalcTimeOffset = tail.CalcTimeOffset = _ => 1f;
            bar.Excute(frame, 1f);
            tail.Excute(frame, 1f);
            LiveConfig.ViewLeadSeconds = timing.ViewLeadSeconds;

            var host = new GameObject("travelling hold");
            try
            {
                var line = host.AddComponent<NoteLineView>();
                line.Setup(null, "longNoteLine.png", 1f);
                line.Add(bar);
                line.Excute(viewTime);
                var vertices = host.GetComponent<MeshFilter>().sharedMesh.vertices;
                Assert.That(vertices.Length, Is.GreaterThanOrEqualTo(8));
                CheckEndpoint<LongNoteView>(bar, vertices[0].y);
                CheckEndpoint<DefaultNoteView>(tail, vertices[vertices.Length - 1].y);

                void CheckEndpoint<T>(NoteBase note, float meshY) where T : BaseNoteView
                {
                    var child = new GameObject("key");
                    child.transform.SetParent(host.transform, false);
                    var key = child.AddComponent<T>();
                    key.Setup(LiveConfig.SpawnPosition, LiveConfig.JudgmentPositions, 1f);
                    key.Spawn(note, 0f);
                    key.Move(viewTime);
                    float progress = LiveConfig.GetNoteViewProgress(1f + viewTime - note.MusicScoreInfo.time);
                    float expectedY = Mathf.Lerp(LiveConfig.SpawnPosition.y, LiveConfig.JudgmentPositions[0].y, progress);
                    Assert.That(meshY, Is.EqualTo(expectedY).Within(1e-4f), "body endpoint");
                    Assert.That(key.transform.localPosition.y, Is.EqualTo(meshY).Within(1e-4f), "key on body endpoint");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SlidingHeadFollowsTheVisibleSegmentWhenTheClocksStraddleABend()
        {
            var bar = new LongNote(Info(1f), 1, 1, 2, NoteCategory.Long, NoteType.Default, 1f, 0f);
            var bend = new ConnectionNote(Info(1.5f), 2, 8, 9, NoteCategory.Connection, NoteType.Default, 1f, 0f);
            var tail = new NormalNote(Info(2f), 3, 2, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
            bar.AddConnectionNote(bend);
            bar.AddNoteListAndSetupChildNote(tail);
            bar.CalcTimeOffset = bend.CalcTimeOffset = tail.CalcTimeOffset = _ => 1f;
            bar.Excute(Info(1.6f), 1f);
            bar.SetStateUnnotice(NoteState.Input);
            LiveConfig.ViewLeadSeconds = -0.2f;

            var host = new GameObject("sliding head");
            try
            {
                var line = host.AddComponent<NoteLineView>();
                line.Setup(null, "longNoteLine.png", 1f);
                line.Add(bar);
                line.Excute(1.4f);
                var vertices = host.GetComponent<MeshFilter>().sharedMesh.vertices;
                Assert.That(vertices.Length, Is.GreaterThanOrEqualTo(8));
                var child = new GameObject("key");
                child.transform.SetParent(host.transform, false);
                var key = child.AddComponent<LongNoteView>();
                key.Setup(LiveConfig.SpawnPosition, LiveConfig.JudgmentPositions, 1f);
                key.Spawn(bar, 0f);
                key.Move(1.4f);
                Assert.That(key.transform.localPosition.x,
                    Is.EqualTo((vertices[2].x + vertices[4].x) * 0.5f).Within(1e-4f));
                Assert.That(key.transform.localPosition.y, Is.EqualTo(vertices[2].y).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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
