using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Settings;
using OpenSekai.Shared.Live.Simulation;
using OpenSekai.Tests.EditMode.Live;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Charts
{
    public sealed class NextRushChartLoaderTests
    {
        private LiveBundleBuildData _config;
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _config = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
            Assert.That(_config, Is.Not.Null);
            _directory = Path.Combine(Path.GetTempPath(), "OpenSekai-NextRush-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }
        [TearDown]
        public void TearDown() { Directory.Delete(_directory, true); }

        private static JObject V(string name, double value) => new JObject { ["name"] = name, ["value"] = value };
        private static JObject R(string name, string value) => new JObject { ["name"] = name, ["ref"] = value };
        private static JObject E(string archetype, string name, params JObject[] data)
        {
            var e = new JObject { ["archetype"] = archetype, ["data"] = new JArray(data) };
            if (name != null) e["name"] = name;
            return e;
        }
        private static JObject N(string type, string name, double beat, double lane = 0, double size = 1)
            => E(type, name, V("#BEAT", beat), V("lane", lane), V("size", size));
        private static void Add(JObject entity, params JObject[] fields) { foreach (var field in fields) ((JArray)entity["data"]).Add(field); }
        private static JObject Chart(params JObject[] entities)
        {
            var data = new JArray { E("Initialization", null, V("revision", 2)), E("#BPM_CHANGE", null, V("#BEAT", 0), V("#BPM", 120)) };
            foreach (var entity in entities) data.Add(entity);
            return new JObject { ["bgmOffset"] = 0, ["entities"] = data };
        }
        private RuntimeChartDefinition Load(JObject json, bool pairs = true) => NextRushChartLoader.Load(NextRushChart.Parse(json.ToString()), _config, pairs);

        private static JObject Hold()
        {
            var head = N("NormalHeadTapNote", "head", 1, -2, 1);
            Add(head, R("next", "middle"));
            var middle = N("NormalTickNote", "middle", 2, 0, 1);
            Add(middle, R("next", "tail"), R("activeHead", "head"));
            var tail = N("NormalTailReleaseNote", "tail", 3, 2, 1);
            Add(tail, R("activeHead", "head"));
            var connector1 = E("Connector", null, R("head", "head"), R("tail", "tail"), R("segmentHead", "head"), R("segmentTail", "middle"), R("activeHead", "head"), R("activeTail", "tail"));
            var connector2 = E("Connector", null, R("head", "head"), R("tail", "tail"), R("segmentHead", "middle"), R("segmentTail", "tail"), R("activeHead", "head"), R("activeTail", "tail"));
            var hidden = E("TransientHiddenTickNote", null, V("#BEAT", 1.5), V("isAttached", 1), R("attachHead", "head"), R("attachTail", "middle"));
            // Deliberately shuffled: names are references, never array indices or hexadecimal IDs.
            return Chart(tail, hidden, connector2, head, connector1, middle);
        }

        [Test]
        public void NativeRuntimeHasNoLegacyDocumentAndKeepsTheSource()
        {
            var json = Chart(N("NormalTapNote", "tap", 4));
            var source = NextRushChart.Parse(json.ToString());
            var result = NextRushChartLoader.Load(source, _config);
            Assert.That(result.Document, Is.Null);
            Assert.That(result.NextRush, Is.SameAs(source));
            Assert.That(result.HasPlayableNotes, Is.True);
            Assert.That(result.TotalNoteCount, Is.EqualTo(1));
            Assert.That(result.TicksMax, Is.EqualTo(1920));
            Assert.That(result.Score.NoteArray[0].MusicScoreInfo.time, Is.EqualTo(2).Within(0.0001));
        }

        [TestCase(-5.5, 0.5, 0, 0)]
        [TestCase(5.5, 0.5, 11, 11)]
        [TestCase(0, 1, 5, 6)]
        [TestCase(0.25, 0.75, 5.5, 6)]
        public void GeometryPreservesNativeFractionalBoundaries(double lane, double size, double left, double right)
        {
            var note = Load(Chart(N("NormalTapNote", null, 1, lane, size))).Score.NoteArray.Single();
            Assert.That(note.LaneStartF, Is.EqualTo(left));
            Assert.That(note.LaneEndF, Is.EqualTo(right));
        }

        [Test]
        public void BpmChangesDriveNativeBeatTiming()
        {
            var result = Load(Chart(E("#BPM_CHANGE", null, V("#BEAT", 4), V("#BPM", 240)), N("CriticalFlickNote", null, 8)));
            Assert.That(result.Score.NoteArray.Single().MusicScoreInfo.time, Is.EqualTo(3).Within(0.0001));
            Assert.That(result.Score.NoteArray.Single(), Is.TypeOf<FlickNote>());
            Assert.That(result.Score.NoteArray.Single().Type, Is.EqualTo(NoteType.Critical));
        }

        [Test]
        public void NamedHoldConnectionsAndExplicitTicksAreLoadedExactlyOnce()
        {
            var result = Load(Hold());
            var head = (LongNote)result.Score.NoteArray.Single();
            Assert.That(head.ChildNote, Is.TypeOf<NormalNote>());
            Assert.That(head.NoteList.Count, Is.EqualTo(4));
            Assert.That(result.TotalNoteCount, Is.EqualTo(4));
            var hidden = head.NoteList.OfType<LongHoldCombo>().Single();
            Assert.That(hidden.LaneStartF, Is.EqualTo(4));
            Assert.That(hidden.LaneEndF, Is.EqualTo(5));
        }

        [Test]
        public void NativeTailKeepsItsOwnCriticalTypeAfterParenting()
        {
            var json = Hold();
            var head = ((JArray)json["entities"]).OfType<JObject>().Single(e => (string)e["name"] == "head");
            head["archetype"] = "CriticalHeadTapNote";
            var result = Load(json);
            var root = (LongNote)result.Score.NoteArray.Single();
            Assert.That(root.Type, Is.EqualTo(NoteType.Critical));
            Assert.That(root.ChildNote.Type, Is.EqualTo(NoteType.Default));
        }

        [Test]
        public void NativeHoldAutoplaySettlesEveryJudgedPoint()
        {
            var result = Load(Hold());
            using var harness = new LiveHarness();
            harness.Arrange(result.Score);
            for (float time = 0; time < 5; time += 1f / 120)
            {
                harness.OnUpdate(time, 10d + time);
                harness.Logic.OnAutoInput();
                if (harness.Logic.IsNotesAllFinished) break;
            }
            Assert.That(harness.Logic.IsNotesAllFinished, Is.True);
            Assert.That(result.Score.NoteArray.SelectMany(n => n.NoteList).All(n => n.State == NoteState.Done), Is.True);
        }

        [Test]
        public void PairSettingAppliesToNativeNotes()
        {
            var json = Chart(N("NormalTapNote", "left", 1, -3), N("NormalTapNote", "right", 1, 3));
            Assert.That(Load(json).Score.NoteArray[0].PairNote, Is.Not.Null);
            Assert.That(Load(json, false).Score.NoteArray[0].PairNote, Is.Null);
        }

        [TestCase(null, NextRushRevision.Sonolus110)]
        [TestCase(0, NextRushRevision.Base)]
        [TestCase(1, NextRushRevision.Sonolus110)]
        [TestCase(2, NextRushRevision.ApparentElevation)]
        public void InitializationRevisionIsRetained(int? revision, NextRushRevision expected)
        {
            var json = Chart(N("NormalTapNote", null, 1));
            json["entities"][0]["data"] = revision.HasValue ? new JArray(V("revision", revision.Value)) : new JArray();

            var result = Load(json);

            Assert.That(result.NextRushCompatibility.Revision, Is.EqualTo(expected));
            Assert.That(result.TotalNoteCount, Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(1.5)]
        [TestCase(3)]
        public void UnknownInitializationRevisionBlocksPlayback(double revision)
        {
            var json = Chart(N("NormalTapNote", null, 1));
            json["entities"][0]["data"] = new JArray(V("revision", revision));

            Assert.That(() => Load(json), Throws.TypeOf<InvalidDataException>().With.Message.Contains("Initialization.revision"));
        }

        [Test]
        public void SkillAndFeverAreReportedWithoutCreatingNotesOrEvents()
        {
            var tap = N("NormalTapNote", "tap", 1);
            var baseline = Load(Chart(tap));
            var result = Load(Chart((JObject)tap.DeepClone(),
                E("Skill", "skill", V("unknownBusinessField", 1000), R("unusedReference", "missing")),
                E("FeverChance", null, V("#BEAT", 0)), E("FeverStart", null, V("#BEAT", 0.5))));

            Assert.That(result.NextRushCompatibility.Archetypes
                .Where(a => a.Status == NextRushEntityStatus.IgnoredByProject)
                .Select(a => (a.Archetype, a.Count)), Is.EquivalentTo(new[] { ("Skill", 1), ("FeverChance", 1), ("FeverStart", 1) }));
            Assert.That(result.NextRush.NamedEntities["skill"].Reference("unusedReference"), Is.EqualTo("missing"));
            Assert.That(result.TotalNoteCount, Is.EqualTo(baseline.TotalNoteCount));
            Assert.That(result.TicksMax, Is.EqualTo(baseline.TicksMax));
            Assert.That(result.Score.NoteArray.Single().MusicScoreInfo.time, Is.EqualTo(baseline.Score.NoteArray.Single().MusicScoreInfo.time));
            Assert.That(result.Score.EventArray, Is.Empty);
        }

        [Test]
        public void IgnoredSkillCannotResolveAsANoteOrCamera()
        {
            var json = Hold();
            ((JArray)json["entities"]).Add(E("Skill", "skill"));
            var head = ((JArray)json["entities"]).OfType<JObject>().Single(e => (string)e["name"] == "head");
            head["data"].Single(f => (string)f["name"] == "next")["ref"] = "skill";
            Assert.That(() => Load(json), Throws.TypeOf<InvalidDataException>().With.Message.Contains("next"));

            ((JArray)json["entities"][0]["data"]).Add(R("firstCamera", "skill"));
            Assert.That(() => Load(json), Throws.TypeOf<InvalidDataException>().With.Message.Contains("firstCamera"));
        }

        [Test]
        public void CompatibilityReportDistinguishesDeferredRuntimeOnlyAndUnknownEntities()
        {
            var json = Chart(E("StaticStage", null), E("FeverStartV2", null), E("DamageNote", null));
            var report = NextRushCompatibility.Analyze(NextRushChart.Parse(json.ToString()));

            Assert.That(report.PassesPreflight, Is.False);
            Assert.That(report.Archetypes.Single(a => a.Archetype == "StaticStage").Status, Is.EqualTo(NextRushEntityStatus.RuntimeOnly));
            Assert.That(report.Archetypes.Single(a => a.Archetype == "FeverStartV2").Status, Is.EqualTo(NextRushEntityStatus.Unknown));
            Assert.That(report.Archetypes.Single(a => a.Archetype == "DamageNote").Status, Is.EqualTo(NextRushEntityStatus.Unsupported));
            Assert.That(() => Load(Chart(E("FeverStartV2", null))), Throws.TypeOf<InvalidDataException>().With.Message.Contains("FeverStartV2"));
        }

        [TestCase("NormalHeadTraceNote")]
        [TestCase("NormalTailTapNote")]
        [TestCase("DamageNote")]
        public void UnsupportedArchetypesAreRefused(string archetype)
        {
            Assert.Throws<InvalidDataException>(() => Load(Chart(N(archetype, null, 1))));
        }

        [Test]
        public void GroupsUseIndependentTrajectoriesWithoutMovingJudgmentTimes()
        {
            var a = N("NormalTapNote", "a", 6, -2);
            var b = N("NormalTapNote", "b", 6, 2);
            Add(a, R("#TIMESCALE_GROUP", "fast"));
            Add(b, R("#TIMESCALE_GROUP", "slow"));
            var result = Load(Chart(a, b,
                E("#TIMESCALE_GROUP", "fast", R("first", "fast1"), R("editorName", "highlight"), V("editorHidden", 1)),
                E("#TIMESCALE_GROUP", "slow", R("first", "slow1"), V("forceNoteSpeed", 12)),
                E("#TIMESCALE_CHANGE", "fast1", V("#BEAT", 0), V("#TIMESCALE", 2), R("next", "fast2")),
                E("#TIMESCALE_CHANGE", "fast2", V("#BEAT", 4), V("#TIMESCALE", 2), V("#TIMESCALE_SKIP", 1)),
                E("#TIMESCALE_CHANGE", "slow1", V("#BEAT", 0), V("#TIMESCALE", 0), V("hideNotes", 1))));
            var notes = result.Score.NoteArray.OrderBy(n => n.LaneStartF).ToArray();
            foreach (var note in notes) Assert.That(note.MusicScoreInfo.time, Is.EqualTo(3));
            Assert.That(notes[0].NativeScroll.Distance(notes[0].NativeScroll.Sample(1), notes[0].NativeTarget),
                Is.EqualTo(4.5).Within(1e-9));
            Assert.That(notes[1].NativeScroll.Sample(1).Hidden, Is.True);
            Assert.That(notes[1].NativeScroll.Preempt(2), Is.EqualTo(0.35).Within(1e-9));

            using var harness = new LiveHarness();
            harness.Arrange(result.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            harness.Step(2.6f, 12.6);
            harness.Step(2.9f, 12.9);
            Assert.That(LiveConfig.ViewProgressOf(notes[1], 2.9f), Is.Zero);
            var left = LiveConfig.JudgmentPositions[3];
            var right = LiveConfig.JudgmentPositions[7];
            harness.Step(3f, 13, LiveHarness.Touch(1, left, 13), LiveHarness.Touch(2, right, 13));
            harness.Step(3.2f, 13.2);
            harness.Step(3.3f, 13.3);
            Assert.That(harness.Logic.IsNotesAllFinished, Is.True);
            Assert.That(notes.Select(n => n.Result), Is.All.EqualTo(NoteResult.JustPerfect));
        }

        [TestCase("cycle")]
        [TestCase("owner")]
        [TestCase("order")]
        [TestCase("orphan")]
        public void InvalidTimescaleGraphsAreRejected(string problem)
        {
            var group = E("#TIMESCALE_GROUP", "group", R("first", "first"));
            var first = E("#TIMESCALE_CHANGE", "first", V("#BEAT", 2), V("#TIMESCALE", 1));
            var second = E("#TIMESCALE_CHANGE", "second", V("#BEAT", problem == "order" ? 1 : 2), V("#TIMESCALE", 1));
            if (problem != "orphan") Add(first, R("next", "second"));
            if (problem == "cycle") Add(second, R("next", "first"));
            if (problem == "owner") Add(second, R("#TIMESCALE_GROUP", "elsewhere"));
            Assert.Throws<InvalidDataException>(() =>
                Load(Chart(group, first, second, E("#TIMESCALE_GROUP", "elsewhere"))));
        }

        [Test]
        public void TimelineConservativelyActivatesBeforeStopAndReverseReentry()
        {
            var timeline = new NextRushScrollTimeline(new[]
            {
                new NextRushScrollTimeline.Change(2, 0, 0, 0, false, true),
                new NextRushScrollTimeline.Change(4, -1, 0, 0, false, false),
                new NextRushScrollTimeline.Change(6, 1, 0, 0, false, false)
            }, 0);
            var hit = timeline.Sample(9);
            Assert.That(timeline.ActivationTime(hit, 1.25), Is.EqualTo(1.75).Within(1e-9));
            Assert.That(timeline.Distance(timeline.Sample(2), hit), Is.EqualTo(1).Within(1e-9));
            Assert.That(timeline.Sample(3).Hidden, Is.True);
            Assert.That(timeline.Distance(timeline.Sample(5), hit), Is.EqualTo(2).Within(1e-9));
            Assert.That(timeline.Distance(timeline.Sample(8), hit), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void VisualWalkerKeepsAnUnjudgedNoteAvailableForReentryAndRestart()
        {
            var tap = N("NormalTapNote", "tap", 18);
            Add(tap, R("#TIMESCALE_GROUP", "group"));
            var note = Load(Chart(tap, E("#TIMESCALE_GROUP", "group", R("first", "reverse")),
                E("#TIMESCALE_CHANGE", "reverse", V("#BEAT", 4), V("#TIMESCALE", -1), R("next", "forward")),
                E("#TIMESCALE_CHANGE", "forward", V("#BEAT", 12), V("#TIMESCALE", 1)))).Score.NoteArray.Single();
            var session = new LiveNoteSession();
            session.Bind(new[] { note });
            session.BindNativeVisuals(0.5);
            double progress = double.NaN;
            double now = 0.8;
            void Visit(NoteBase n) => progress = n.NativeViewProgress(now, out _);
            session.WalkNativeVisuals(now, Visit);
            Assert.That(progress, Is.GreaterThan(0));
            now = 6;
            session.WalkNativeVisuals(now, Visit);
            Assert.That(progress, Is.LessThan(0));
            now = 8.75;
            session.WalkNativeVisuals(now, Visit);
            Assert.That(progress, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(note.State, Is.EqualTo(NoteState.Wait));
            session.Reset();
            progress = double.NaN;
            now = 0.8;
            session.WalkNativeVisuals(now, Visit);
            Assert.That(progress, Is.GreaterThan(0));
        }

        [Test]
        public void MixedModesComposeScrollWidthsAndTimescaleDistancesInBothDirections()
        {
            var timeline = new NextRushScrollTimeline(new[]
            {
                new NextRushScrollTimeline.Change(0, 2, 0, 1, true, false),
                new NextRushScrollTimeline.Change(2, 4, 2, 0, false, false),
                new NextRushScrollTimeline.Change(4, 1, 0, 0, true, false)
            }, 0);
            var a = timeline.Sample(1);
            var b = timeline.Sample(5);
            // Width to the first boundary is 1 + 2/4. Its initial speed is 3.
            // The remaining run distances are 8 and 1, scaled by the first run's 3/4.
            Assert.That(timeline.Distance(a, b), Is.EqualTo(11.25).Within(1e-9));
            Assert.That(timeline.Distance(b, a), Is.EqualTo(-15).Within(1e-9));
            Assert.That(timeline.Distance(timeline.Sample(0), a), Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void LinearEaseAndSameTimeSkipsUseLinkedMarkerOrder()
        {
            var timeline = new NextRushScrollTimeline(new[]
            {
                new NextRushScrollTimeline.Change(0, 1, 0, 1, false, false),
                new NextRushScrollTimeline.Change(2, 3, 1, 0, false, true),
                new NextRushScrollTimeline.Change(2, -1, 2, 0, false, false)
            }, 0);
            Assert.That(timeline.Distance(timeline.Sample(0), timeline.Sample(1)), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(timeline.Distance(timeline.Sample(0), timeline.Sample(2)), Is.EqualTo(7).Within(1e-9));
            Assert.That(timeline.Distance(timeline.Sample(2), timeline.Sample(3)), Is.EqualTo(-1).Within(1e-9));
            Assert.That(timeline.Sample(2).Hidden, Is.False);
        }

        [TestCase("style", 1)]
        [TestCase("direction", 3)]
        [TestCase("segmentAlpha", -0.5)]
        [TestCase("connectorEase", 99)]
        [TestCase("futureField", 1)]
        public void UnsupportedFieldsAreNeverSilentlyDropped(string field, double value)
        {
            var note = N("NormalTapNote", null, 1);
            Add(note, V(field, value));
            Assert.Throws<InvalidDataException>(() => Load(Chart(note)));
        }

        private static JObject Segment(string head, string tail, string start, string end, string activeHead = null, string activeTail = null)
        {
            var segment = E("Connector", null, R("head", head), R("tail", tail), R("segmentHead", start), R("segmentTail", end));
            if (activeHead != null) Add(segment, R("activeHead", activeHead), R("activeTail", activeTail));
            return segment;
        }

        [Test]
        public void AnchorsShapeSameBeatEdgesGuidesAndHiddenTicksWithoutAddingJudgments()
        {
            var head = N("NormalHeadTapNote", "head", 2, -2);
            var middle = N("AnchorNote", "middle", 2, -1);
            var tail = N("AnchorNote", "tail", 4, 3);
            Add(head, R("next", "middle"));
            Add(middle, R("next", "tail"), V("connectorEase", 2));
            Add(tail, R("activeHead", "head"));
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "middle"), R("attachTail", "tail"));
            var guide = N("AnchorNote", "guide", 0);
            var guideMiddle = N("AnchorNote", "guideMiddle", 1);
            var guideEnd = N("AnchorNote", "guideEnd", 2);
            Add(guide, R("next", "guideMiddle"), V("segmentKind", 103), V("segmentAlpha", 0));
            Add(guideMiddle, R("next", "guideEnd"));
            var chart = Load(Chart(guideEnd, tail, tick, middle, head, guideMiddle, guide,
                Segment("head", "middle", "head", "middle", "head", "tail"),
                Segment("middle", "tail", "middle", "tail", "head", "tail"),
                Segment("guide", "guideMiddle", "guide", "guideEnd"),
                Segment("guideMiddle", "guideEnd", "guide", "guideEnd")));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single(n => n.HasJudgment);
            Assert.That(bar.ChildNote, Is.TypeOf<HiddenConnectionNote>());
            Assert.That(bar.LineNoteList.Count, Is.EqualTo(3));
            Assert.That(bar.LineNoteList[0].MusicScoreInfo.time, Is.EqualTo(bar.LineNoteList[1].MusicScoreInfo.time));
            Assert.That(bar.NoteList.OfType<LongHoldCombo>().Single().LaneStartF, Is.EqualTo(5).Within(0.00001));
            var body = chart.Score.NoteArray.OfType<GuideNote>().Single();
            Assert.That(body.NativeConnector.Kind, Is.EqualTo(103));
            Assert.That(body.NativeConnector.EndAlpha, Is.EqualTo(0.5).Within(0.00001));
            Assert.That(body.NoteList.All(note => !note.HasJudgment), Is.True);
            AssertCompletesAutoplay(chart);
        }

        [Test]
        public void NativeHiddenTickAtAVisibleTicksBeatKeepsItsOwnJudgment()
        {
            var json = Hold();
            var hidden = ((JArray)json["entities"]).OfType<JObject>().Single(e => (string)e["archetype"] == "TransientHiddenTickNote");
            hidden["data"] = new JArray(V("#BEAT", 2), V("isAttached", 1), R("attachHead", "middle"), R("attachTail", "tail"));
            var chart = Load(json);
            var hold = chart.Score.NoteArray.OfType<LongNote>().Single();
            Assert.That(hold.JudgedPoints.Count, Is.EqualTo(2));
            Assert.That(hold.JudgedPoints[0].MusicScoreInfo.time, Is.EqualTo(hold.JudgedPoints[1].MusicScoreInfo.time));
            AssertCompletesAutoplay(chart);
            Assert.That(hold.JudgedPoints.Select(n => n.Result), Is.All.EqualTo(NoteResult.Auto));
        }

        [TestCase(-1d, -1.25, 3.25, -4.25, 7)]
        [TestCase(-0.51, -0.76, 3.74, -3.76, 7)]
        [TestCase(0d, -0.25, 4.25, -3.25, 7.25)]
        [TestCase(0.51, 0.26, 4.76, -3, 7.76)]
        [TestCase(1d, 0.75, 5.25, -3, 8.25)]
        public void NativeOffsetPreservesTheThreeSecondIntroAndResultRules(
            double offset, double first, double last, double start, double result)
        {
            var json = Chart(N("NormalTapNote", null, -0.5), N("NormalTapNote", null, 8.5));
            json["bgmOffset"] = offset;
            var boot = Boot(json);
            var root = new GameObject("NextRUSH timeline test");
            try
            {
                var controller = root.AddComponent<TimelineProbe>();
                controller.Arrange(boot, 4000L);

                Assert.That(boot.MusicData.NativeBgmOffsetSeconds, Is.EqualTo(offset));
                Assert.That(boot.MusicData.AudioToChartTime(0), Is.EqualTo(-offset));
                Assert.That(boot.MusicData.Music.fillerSec, Is.EqualTo(2f), "the original manifest value is preserved");
                Assert.That(controller.Span.FirstNoteSeconds, Is.EqualTo(first).Within(1e-6));
                Assert.That(controller.Span.LastNoteSeconds, Is.EqualTo(last).Within(1e-6));
                Assert.That(controller.Span.StartSeconds, Is.EqualTo(start).Within(1e-6));
                Assert.That(controller.Span.ResultEffectSeconds, Is.EqualTo(result).Within(1e-6));
                controller.CallPreExit();
                Assert.That(controller.ResultAtMs, Is.EqualTo(result * 1000).Within(1), "normal results use an absolute audio deadline");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(0.51, 1.98)]
        [TestCase(-0.51, 4.02)]
        public void SyntheticAudioClickAlignsWithTheNoteAndInputCompensations(double offset, double beat)
        {
            var json = Chart(N("NormalTapNote", "click", beat));
            json["bgmOffset"] = offset;
            var boot = Boot(json);
            var root = new GameObject("NextRUSH click test");
            const int sampleRate = 8000;
            var click = AudioClip.Create("click", 2 * sampleRate, 1, sampleRate, false);
            try
            {
                Assert.That(click.SetData(new[] { 0.8f }, sampleRate * 3 / 2), Is.True);
                Assert.That(AudibleRange.TryFind(click, out float audioSeconds, out _), Is.True);
                Assert.That(audioSeconds, Is.EqualTo(1.5f));

                var controller = root.AddComponent<TimelineProbe>();
                controller.Arrange(boot, 2000L, new TimingCompensation(40f, 10f, 25f));
                var score = boot.MusicData.MusicScore;
                var note = score.NoteArray.Single();
                Assert.That(controller.SampleViewAt(1530L), Is.EqualTo(note.MusicScoreInfo.time).Within(1e-6f));
                float logicTime = controller.SampleLogicAt(1565L);
                Assert.That(logicTime, Is.EqualTo(note.MusicScoreInfo.time).Within(1e-6f));

                using var harness = new LiveHarness();
                harness.Arrange(score);
                harness.OnUpdate(logicTime, 10d);
                harness.AddTouch(1, LiveConfig.JudgmentPositions[5], 10d);
                harness.OnInput();
                Assert.That(note.Result, Is.EqualTo(NoteResult.JustPerfect));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(click);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NegativeBeatUsesTheInitialBpmAcrossZeroAndLaterChanges()
        {
            var chart = Load(Chart(N("NormalTapNote", null, -1.5),
                E("#BPM_CHANGE", null, V("#BEAT", 2), V("#BPM", 60)),
                N("NormalTapNote", null, 2.5)));
            Assert.That(chart.Score.NoteArray.Select(n => n.MusicScoreInfo.time), Is.EqualTo(new[] { -0.75f, 1.5f }));
            chart.Score.GetMusicScoreInfoFromTime(-0.75f, out var frame);
            Assert.That(frame.bar, Is.EqualTo(-1));
            Assert.That(frame.barProgress, Is.EqualTo(0.625f));
        }

        [Test]
        public void OffsetOutsidePlaybackRangeIsRefused()
        {
            var json = Chart(N("NormalTapNote", null, 1));
            json["bgmOffset"] = 1e30;
            Assert.That(() => Load(json), Throws.TypeOf<InvalidDataException>().With.Message.Contains("bgmOffset"));
        }

        private FreeLiveBootData Boot(JObject json)
        {
            var package = new SongPackage(_directory, new SongPackageManifest
            {
                id = "timing-test",
                scoreFileName = "level-data.json",
                fillerSec = 2f
            });
            var request = new PlayRequest(_directory, false, SceneManager.Scene.Library);
            var session = PlaySession.FromChart(request, package, Load(json), _config,
                new PlaybackSettingsSnapshot(new LiveSettingData()));
            var boot = LegacyLiveBootAdapter.Create(session, 0);
            // The probe supplies the audio file duration directly and uses no shared audio cue.
            boot.CustomMusicScoreId = null;
            boot.MusicData.Music.assetbundleName = null;
            boot.MusicData.Vocal.assetbundleName = null;
            return boot;
        }

        private sealed class TimelineProbe : BaseLiveController
        {
            internal LiveTimelineSpan Span => timelineSpan;
            internal long? ResultAtMs { get; private set; }

            internal void Arrange(FreeLiveBootData boot, long durationMs, TimingCompensation compensation = default)
            {
                BootData = boot;
                musicLength = durationMs;
                timingCompensation = compensation;
                BuildTimelineSpan(boot.MusicData.MusicScore);
                state = LiveControllerState.Playing;
            }

            internal float SampleLogicAt(long audioMs)
            {
                currentMusicTimeMs = audioMs;
                return CurrentLogicChartTime;
            }

            internal double SampleViewAt(long audioMs)
            {
                currentMusicTimeMs = audioMs;
                return BootData.MusicData.AudioToChartTime(currentAudioLatencyMusicTimeMs);
            }

            protected override void PreExit(float delay, float waitTime, long? resultEffectAtMs = null)
                => ResultAtMs = resultEffectAtMs;
            protected override void OnAwake() { }
            protected override void OnExit() { }
        }

        [Test]
        public void BrokenAndCyclicHoldReferencesAreRefused()
        {
            var head = N("NormalHeadTapNote", "h", 1);
            Add(head, R("next", "missing"));
            Assert.Throws<InvalidDataException>(() => Load(Chart(head)));
            ((JArray)head["data"]).Last["ref"] = "h";
            Assert.Throws<InvalidDataException>(() => Load(Chart(head)));
        }

        [Test]
        public void MissingAndDuplicateConnectorSegmentsAreRefused()
        {
            var json = Hold();
            var entities = (JArray)json["entities"];
            var connector = entities.OfType<JObject>().First(e => (string)e["archetype"] == "Connector");
            connector.Remove();
            Assert.Throws<InvalidDataException>(() => Load(json));
            json = Hold();
            entities = (JArray)json["entities"];
            entities.Add(entities.OfType<JObject>().First(e => (string)e["archetype"] == "Connector").DeepClone());
            Assert.Throws<InvalidDataException>(() => Load(json));
        }

        [Test]
        public void FailedNativeReplacementKeepsTheInstalledChart()
        {
            var manager = new SongPackageManager(Path.Combine(_directory, "library"), Path.Combine(_directory, "temp"));
            var package = manager.CreateNewEntry();
            byte[] previous = File.ReadAllBytes(package.ScorePath);
            string invalid = Path.Combine(_directory, "broken.next-rush.json");
            File.WriteAllText(invalid, "{}");
            Assert.Throws<InvalidDataException>(() => manager.ReplaceScoreFile(package, invalid));
            Assert.That(File.ReadAllBytes(package.ScorePath), Is.EqualTo(previous));
            Assert.That(SongPackageStorage.LoadEntry(package.RootDirectory).Manifest.chartFormat, Is.Null);
        }

        [Test]
        public void ParserRejectsDuplicateNamesFieldsAndNonfiniteNumbers()
        {
            Assert.Throws<InvalidDataException>(() => NextRushChart.Parse(Chart(N("NormalTapNote", "x", 1), N("NormalTapNote", "x", 2)).ToString()));
            var note = N("NormalTapNote", null, 1);
            Add(note, V("#BEAT", 2));
            Assert.Throws<InvalidDataException>(() => NextRushChart.Parse(Chart(note).ToString()));
            Assert.Throws<InvalidDataException>(() => NextRushChart.Parse("{\"bgmOffset\":NaN,\"entities\":[]}"));
        }

        [Test]
        public void GzipPackageSurvivesImportExportAndLoadsThroughTheSharedBoundary()
        {
            string path = Path.Combine(_directory, "original.next-rush.json.gz");
            byte[] json = Encoding.UTF8.GetBytes(Hold().ToString());
            using (var output = File.Create(path))
            using (var gzip = new GZipStream(output, CompressionMode.Compress)) gzip.Write(json, 0, json.Length);
            byte[] original = File.ReadAllBytes(path);
            var manager = new SongPackageManager(Path.Combine(_directory, "library"), Path.Combine(_directory, "temp"));
            var package = manager.ReplaceScoreFile(manager.CreateNewEntry(), path);
            Assert.That(package.Manifest.chartFormat, Is.EqualTo("next-rush"));
            Assert.That(File.ReadAllBytes(package.ScorePath), Is.EqualTo(original));
            string archive = manager.ExportZip(package, Path.Combine(_directory, "export.oskp"));
            var imported = manager.ImportZip(archive);
            Assert.That(File.ReadAllBytes(imported.ScorePath), Is.EqualTo(original));
            var runtime = RuntimeChartLoader.Load(imported, _config);
            var session = PlaySession.FromChart(new PlayRequest(imported.RootDirectory, true, SceneManager.Scene.Library), imported, runtime,
                _config, new PlaybackSettingsSnapshot(new LiveSettingData()));
            Assert.That(session.Chart.NextRush, Is.Not.Null);
            Assert.That(session.Chart.TotalNoteCount, Is.EqualTo(4));
            // Replacing a native chart by a legacy one must clear the format discriminator.
            string legacy = Path.Combine(_directory, "legacy.json");
            File.WriteAllText(legacy, OpenSekai.Charts.Serialization.ChartJson.Write(new ChartDocument()));
            Assert.That(manager.ReplaceScoreFile(imported, legacy).Manifest.chartFormat, Is.Null);
        }

        [Test]
        public void CorruptGzipAndUnknownPackageFormatsAreRefused()
        {
            string path = Path.Combine(_directory, "bad.next-rush.json.gz");
            File.WriteAllBytes(path, new byte[] { 0x1f, 0x8b, 0, 0, 0, 0, 0, 0, 0, 0, 0xff });
            Assert.That(() => NextRushChartLoader.ReadFile(path), Throws.Exception);
            var package = new SongPackage(_directory, new SongPackageManifest { chartFormat = "unknown", scoreFileName = "anything.json" });
            Assert.Throws<InvalidDataException>(() => RuntimeChartLoader.Load(package, _config));
        }

        [Test]
        public void DownloadedServerSampleLoadsAndAutoplaysWhenProvided()
        {
            string directory = Environment.GetEnvironmentVariable("OPENSEKAI_SONOLUS_SAMPLE_DIR");
            if (string.IsNullOrEmpty(directory)) Assert.Ignore("Optional integration test: provide OPENSEKAI_SONOLUS_SAMPLE_DIR from probe-sonolus.ps1.");
            var source = NextRushChartLoader.ReadFile(Path.Combine(directory, "level-data.json.gz"));
            var result = NextRushChartLoader.Load(source, _config);
            string archive = Environment.GetEnvironmentVariable("OPENSEKAI_SONOLUS_PACKAGE");
            if (!string.IsNullOrEmpty(archive))
            {
                var manager = new SongPackageManager(Path.Combine(_directory, "library"), Path.Combine(_directory, "temp"));
                var imported = manager.ImportZip(archive);
                Assert.That(imported.Manifest.chartFormat, Is.EqualTo("next-rush"));
                Assert.That(File.Exists(imported.AudioPath), Is.True);
                Assert.That(File.Exists(imported.JacketPath), Is.True);
                Assert.That(File.Exists(Path.Combine(imported.RootDirectory, "sonolus-source.json")), Is.True);
                Assert.That(File.ReadAllBytes(imported.ScorePath), Is.EqualTo(File.ReadAllBytes(Path.Combine(directory, "level-data.json.gz"))));
                result = RuntimeChartLoader.Load(imported, _config);
            }
            int authored = source.Entities.Count(e => e.Archetype.EndsWith("Note", StringComparison.Ordinal) && e.Archetype != "AnchorNote");
            Assert.That(result.TotalNoteCount, Is.EqualTo(authored), "native scored entities must appear exactly once");
            AssertCompletesAutoplay(result);
            TestContext.WriteLine($"Native server sample: {source.Entities.Count} entities, {result.TotalNoteCount} judged points, autoplay completed.");
        }

        private static void AssertCompletesAutoplay(RuntimeChartDefinition result)
        {
            using var harness = new LiveHarness();
            harness.Arrange(result.Score, auto: true);
            harness.Logic.Notes.BindNativeVisuals(1);
            float end = result.Score.NoteArray.SelectMany(n => n.NoteList).Max(n => n.MusicScoreInfo.time) + 3;
            for (float time = -2; time < end; time += 1f / 120)
            {
                harness.OnUpdate(time, 10d + time);
                harness.Logic.OnAutoInput();
                if (harness.Logic.IsNotesAllFinished) break;
            }
            Assert.That(harness.Logic.IsNotesAllFinished, Is.True);
        }
    }
}
