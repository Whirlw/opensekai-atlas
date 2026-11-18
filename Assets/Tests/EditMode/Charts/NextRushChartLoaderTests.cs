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
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

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
        [TestCase("segmentLayer", 4)]
        [TestCase("segmentLayer", 1.5)]
        [TestCase("segmentPresentation", 2)]
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
            Assert.That(body.NativeConnector.StartAlpha, Is.Zero);
            Assert.That(body.NativeConnector.EndAlpha, Is.EqualTo(0.5).Within(0.00001));
            Assert.That(body.NoteList.All(note => !note.HasJudgment), Is.True);
            AssertCompletesAutoplay(chart);
        }

        [TestCase(1, 1)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(2, 0)]
        [TestCase(13, 0)]
        [TestCase(24, 0)]
        public void PlayableConnectorStaysOpaqueAcrossAnAuthoredFade(int kind, double initialAlpha)
        {
            var head = N("NormalHeadTapNote", "head", 1);
            var middle = N("AnchorNote", "middle", 2);
            var tail = N("AnchorNote", "tail", 3);
            Add(head, R("next", "middle"), V("segmentKind", kind), V("segmentAlpha", initialAlpha));
            Add(middle, R("next", "tail"));
            Add(tail, R("activeHead", "head"), V("segmentAlpha", 0));
            var chart = Load(Chart(head, middle, tail,
                Segment("head", "middle", "head", "tail", "head", "tail"),
                Segment("middle", "tail", "head", "tail", "head", "tail")));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            foreach (var segment in bar.LineNoteList.OfType<NoteBase>().Take(2).Select(n => n.NativeConnector))
            {
                Assert.That(segment.Kind, Is.EqualTo(kind));
                Assert.That(segment.StartAlpha, Is.EqualTo(1f));
                Assert.That(segment.EndAlpha, Is.EqualTo(1f));
            }
        }

        [Test]
        public void AttachedVisibleTickSharesTheCurveAndBothScrollGroups()
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            var tick = E("CriticalTickNote", "tick", V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            Add(head, R("next", "tail"), V("isSeparator", 1), V("connectorEase", 10));
            Add(tail, R("activeHead", "head"), R("#TIMESCALE_GROUP", "fast"));
            var chart = Load(Chart(tick, tail, head,
                Segment("head", "tail", "head", "tail", "head", "tail"),
                E("#TIMESCALE_GROUP", "fast", R("first", "fastChange")),
                E("#TIMESCALE_CHANGE", "fastChange", V("#BEAT", 0), V("#TIMESCALE", 2))));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var attached = bar.JudgedPoints.Single();
            Assert.That(attached.Type, Is.EqualTo(NoteType.Critical));
            Assert.That(attached.IsVisible, Is.True);
            Assert.That(attached.LaneStartF, Is.EqualTo(2).Within(1e-6));
            Assert.That(attached.JudgeLaneStart + attached.JudgeLaneEnd, Is.EqualTo(5).Within(1e-6));
            Assert.That(bar.LineNoteList, Has.Count.EqualTo(2));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Logic.Notes.BindNativeVisuals(1);
                Assert.That(attached.NativeViewProgress(1.25, out _), Is.EqualTo(0.5).Within(1e-6));
            }
            AssertCompletesAutoplay(chart);
            Assert.That(attached.Result, Is.EqualTo(NoteResult.Auto));
        }

        [Test]
        public void AttachedSeparatorSplitsTheOriginalCurveWithoutRestartingItsEase()
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            var middle = E("AnchorNote", "middle", V("#BEAT", 3), V("isAttached", 1), V("isSeparator", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            Add(head, R("next", "tail"), V("connectorEase", 2));
            Add(tail, R("prev", "head"), R("activeHead", "head"));
            var json = Chart(middle, tail, head,
                Segment("middle", "tail", "middle", "tail", "head", "tail"),
                Segment("head", "middle", "head", "middle", "head", "tail"));
            var chart = Load(json);
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var separator = (NoteBase)bar.LineNoteList[1];
            Assert.That(bar.LineNoteList, Has.Count.EqualTo(3));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            Assert.That(separator.HasNativeAttachment, Is.True);
            Assert.That(separator.NativeLeft, Is.EqualTo(3f).Within(1e-6));
            Assert.That(bar.NativeConnector.Interpolate(0.5f), Is.EqualTo(0.25f).Within(1e-6));
            Assert.That(separator.NativeConnector.Interpolate(0.5f), Is.EqualTo(5f / 12f).Within(1e-6));

            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Step(0.5f, 10.5, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[1], 10.5));
                harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[6], 12, InputTouchPhase.Moved));
                Assert.That(bar.LaneStartF, Is.EqualTo(5.5f).Within(1e-5));
                harness.Step(2.5f, 12.5, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[9], 12.5, InputTouchPhase.Ended));
                Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.JustPerfect));
                Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.JustPerfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void AttachedGuideEndpointKeepsBothSourceScrollGroupsAndItsOwnReleaseJudgment()
        {
            var head = N("NormalHeadTapNote", "source", 1, -4);
            var tail = N("AnchorNote", "tail", 5, 4);
            var guide = N("AnchorNote", "guide", 2, 4);
            var release = E("CriticalTailReleaseNote", "release", V("#BEAT", 3), V("isAttached", 1), V("isSeparator", 1),
                R("attachHead", "source"), R("attachTail", "tail"));
            Add(head, R("next", "tail"), V("connectorEase", 10));
            Add(tail, R("#TIMESCALE_GROUP", "fast"));
            var json = Chart(guide, tail, release, head,
                Segment("guide", "release", "guide", "release"),
                Segment("source", "tail", "source", "tail", "source", "tail"),
                E("#TIMESCALE_GROUP", "fast", R("first", "fastChange")),
                E("#TIMESCALE_CHANGE", "fastChange", V("#BEAT", 0), V("#TIMESCALE", 2)));
            var chart = Load(json);
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            var key = chart.Score.NoteArray.OfType<NormalNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            Assert.That(bar.TailNote.HasJudgment, Is.False);
            Assert.That(key.ParentNote, Is.Null);
            Assert.That(bar.TailNote.NativeLeft, Is.EqualTo(2f).Within(1e-6));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Logic.Notes.BindNativeVisuals(1);
                Assert.That(key.NativeViewProgress(1.25, out _), Is.EqualTo(0.5).Within(1e-6));
                Assert.That(bar.TailNote.NativeViewProgress(1.25, out _), Is.EqualTo(0.5).Within(1e-6));
                var lane = LiveConfig.JudgmentPositions[2];
                harness.Step(1.3f, 11.3, LiveHarness.Touch(1, lane, 11.3));
                harness.Step(1.5f, 11.5, LiveHarness.Touch(1, lane, 11.5, InputTouchPhase.Ended));
                Assert.That(key.Result, Is.EqualTo(NoteResult.JustPerfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void AttachedSeparatorCanStartAnActiveSectionAndOwnItsHiddenTick()
        {
            var head = N("AnchorNote", "head", 1, -4);
            var tail = N("AnchorNote", "tail", 5, 4);
            var separator = E("AnchorNote", "separator", V("#BEAT", 3), V("isAttached", 1), V("isSeparator", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "separator"));
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 4), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "separator"));
            Add(head, R("next", "tail"));
            Add(tail, R("activeHead", "separator"));
            var chart = Load(Chart(head, tail, separator, tick, Segment("head", "separator", "head", "separator"),
                Segment("separator", "tail", "separator", "tail", "separator", "tail")));
            var guide = chart.Score.NoteArray.OfType<GuideNote>().Single();
            var hold = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(hold.HasNativeAttachment, Is.True);
            Assert.That(guide.TailNote.NativeLeft, Is.EqualTo(hold.NativeLeft));
            Assert.That(hold.JudgedPoints.Single().ParentNote, Is.SameAs(hold));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            AssertCompletesAutoplay(chart);
            Assert.That(hold.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Auto));
        }

        [Test]
        public void AttachmentCannotPointBackToAnAttachedSeparator()
        {
            var a = E("AnchorNote", "a", V("#BEAT", 2), V("isAttached", 1), R("attachHead", "b"), R("attachTail", "b"));
            var b = E("AnchorNote", "b", V("#BEAT", 2), V("isAttached", 1), R("attachHead", "a"), R("attachTail", "a"));
            Assert.Throws<InvalidDataException>(() => Load(Chart(a, b, Segment("a", "b", "a", "b"))));
        }

        [Test]
        public void GuideUsesAnExternalStyleAndLeavesItsEndKeyPlayable()
        {
            var guide = N("AnchorNote", "guide", 2);
            var tail = N("CriticalTapNote", "tail", 4);
            var styleHead = N("AnchorNote", "styleHead", 0);
            var styleTail = N("AnchorNote", "styleTail", 8);
            Add(guide, R("next", "tail"));
            Add(tail, R("prev", "guide"));
            Add(styleHead, V("segmentKind", 104), V("segmentAlpha", 0));
            var chart = Load(Chart(guide, tail, styleHead, styleTail,
                Segment("guide", "tail", "styleHead", "styleTail")));
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            var key = chart.Score.NoteArray.OfType<NormalNote>().Single();
            Assert.That(bar.NativeConnector.Kind, Is.EqualTo(104));
            Assert.That(bar.NativeConnector.StartAlpha, Is.EqualTo(0.25));
            Assert.That(bar.NativeConnector.EndAlpha, Is.EqualTo(0.5));
            Assert.That(bar.TailNote.HasJudgment, Is.False);
            Assert.That(key.ParentNote, Is.Null);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Step(1.9f, 11.9);
                harness.Step(2, 12, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[5], 12));
                Assert.That(key.Result, Is.EqualTo(NoteResult.JustPerfect));
            }
            AssertCompletesAutoplay(chart);
        }

        [Test]
        public void GuideReleaseTailUsesItsOwnLaneAndReleaseWindow()
        {
            var guide = N("AnchorNote", "guide", 2, -3);
            var tail = N("CriticalTailReleaseNote", "tail", 4, 3);
            Add(guide, R("next", "tail"));
            Add(tail, R("prev", "guide"));
            var chart = Load(Chart(tail, guide, Segment("guide", "tail", "guide", "tail")));
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            var release = chart.Score.NoteArray.OfType<NormalNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            Assert.That(bar.TailNote.HasJudgment, Is.False);
            Assert.That(release.ParentNote, Is.Null);
            Assert.That(release.Type, Is.EqualTo(NoteType.Critical));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var wrongLane = LiveConfig.JudgmentPositions[2];
                var lane = LiveConfig.JudgmentPositions[8];
                harness.Step(1.95f, 11.95, LiveHarness.Touch(1, wrongLane, 11.95), LiveHarness.Touch(2, lane, 11.95));
                harness.Step(2, 12, LiveHarness.Touch(1, wrongLane, 12, InputTouchPhase.Ended),
                    LiveHarness.Touch(2, lane, 12, InputTouchPhase.Canceled));
                Assert.That(release.Result, Is.EqualTo(NoteResult.None));
                harness.Step(2.04f, 12.04, LiveHarness.Touch(3, lane, 12.04));
                Assert.That(release.Result, Is.EqualTo(NoteResult.None));
                harness.Step(2.16f, 12.16, LiveHarness.Touch(3, lane, 12.10, InputTouchPhase.Ended));
                Assert.That(release.Result, Is.EqualTo(NoteResult.Great));
                Assert.That(bar.Result, Is.EqualTo(NoteResult.Pass));
            }
            AssertCompletesAutoplay(Load(Chart(tail, guide, Segment("guide", "tail", "guide", "tail"))));
        }

        [TestCase(2.04f, NoteResult.JustPerfect)]
        [TestCase(2.09f, NoteResult.Miss)]
        [TestCase(2.12f, NoteResult.Great)]
        public void GuideReleaseTailChecksRecentTapAndTimesOut(float liftTime, NoteResult expected)
        {
            var guide = N("AnchorNote", "guide", 2);
            var tail = N("NormalTailReleaseNote", "tail", 4);
            Add(guide, R("next", "tail"));
            Add(tail, R("prev", "guide"));
            var chart = Load(Chart(guide, tail, N("NormalTapNote", "tap", 3.88),
                Segment("guide", "tail", "guide", "tail")));
            var release = chart.Score.NoteArray.OfType<NormalNote>().Single(n => n.IsIndependentRelease);
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.JudgmentPositions[5];
            harness.Step(1.94f, 11.94, LiveHarness.Touch(1, lane, 11.94));
            Assert.That(chart.Score.NoteArray.OfType<NormalNote>().Single(n => n != release).Result,
                Is.EqualTo(NoteResult.JustPerfect));
            harness.Step(liftTime, 10d + liftTime, LiveHarness.Touch(1, lane, 10d + liftTime, InputTouchPhase.Ended));
            harness.Step(2.16f, 12.16);
            Assert.That(release.Result, Is.EqualTo(expected));
        }

        [Test]
        public void GuideBetweenTwoHoldSectionsKeepsEachTailAndTickInItsOwnSection()
        {
            var first = N("NormalHeadTapNote", "first", 2, -3);
            var release = N("NormalTailReleaseNote", "release", 4, -3);
            var second = N("CriticalHeadTapNote", "second", 6, 3);
            var end = N("AnchorNote", "end", 8, 3);
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 7), V("isAttached", 1),
                R("attachHead", "second"), R("attachTail", "end"), R("activeHead", "second"));
            Add(first, R("next", "release"), R("activeHead", "first"));
            Add(release, R("next", "second"), R("activeHead", "first"), V("segmentKind", 103));
            Add(second, R("next", "end"), R("activeHead", "second"));
            Add(end, R("activeHead", "second"));
            var json = Chart(second, release, end, first, tick,
                Segment("release", "second", "release", "second"),
                Segment("second", "end", "second", "end", "second", "end"),
                Segment("first", "release", "first", "release", "first", "release"));
            var chart = Load(json);
            var bars = chart.Score.NoteArray.OfType<LongNote>().ToArray();
            Assert.That(bars.Length, Is.EqualTo(3));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(4));
            var guide = (GuideNote)bars[1];
            Assert.That(guide.NativeConnector.Kind, Is.EqualTo(103));
            Assert.That(guide.NativeLeft, Is.EqualTo(bars[0].TailNote.NativeLeft));
            Assert.That(guide.TailNote.NativeLeft, Is.EqualTo(bars[2].NativeLeft));
            Assert.That(guide.NoteList.All(note => !note.HasJudgment), Is.True);
            Assert.That(bars[2].JudgedPoints.Single().ParentNote, Is.SameAs(bars[2]));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var left = LiveConfig.JudgmentPositions[2];
                var right = LiveConfig.JudgmentPositions[8];
                harness.Step(1, 11, LiveHarness.Touch(1, left, 11));
                harness.Step(2, 12, LiveHarness.Touch(1, left, 12, InputTouchPhase.Ended));
                Assert.That(bars[0].Head.Result, Is.EqualTo(NoteResult.JustPerfect));
                Assert.That(bars[0].TailNote.Result, Is.EqualTo(NoteResult.JustPerfect));
                harness.Step(2.5f, 12.5, LiveHarness.Touch(2, right, 12.5));
                Assert.That(bars[2].Head.Result, Is.EqualTo(NoteResult.None));
                harness.Step(3, 13, LiveHarness.Touch(3, right, 13));
                harness.Step(3.5f, 13.5, LiveHarness.Touch(3, right, 13.5, InputTouchPhase.Stationary));
                Assert.That(bars[2].Head.Result, Is.EqualTo(NoteResult.JustPerfect));
                Assert.That(bars[2].JudgedPoints.Single().Result, Is.EqualTo(NoteResult.JustPerfect));
                harness.Step(4.02f, 14.02);
                Assert.That(bars[2].State, Is.EqualTo(NoteState.Done));
                bars[2].ResetNote();
                Assert.That(bars[2].EndsWithoutJudgement, Is.True);
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void ActiveSectionStartingAtAnAnchorAcceptsHoldWithoutAddingAHeadJudgment()
        {
            var guide = N("AnchorNote", "guide", 2, -3);
            var anchor = N("AnchorNote", "anchor", 4, 3);
            var end = N("NormalTailReleaseNote", "end", 6, -3);
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 5), V("isAttached", 1),
                R("attachHead", "anchor"), R("attachTail", "end"));
            Add(guide, R("next", "anchor"));
            Add(anchor, R("next", "end"), R("activeHead", "anchor"));
            Add(end, R("activeHead", "anchor"));
            var json = Chart(guide, end, anchor, tick, Segment("guide", "anchor", "guide", "anchor"),
                Segment("anchor", "end", "anchor", "end", "anchor", "end"));
            var chart = Load(json);
            var hold = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(hold.HasJudgment, Is.False);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var startLane = LiveConfig.JudgmentPositions[8];
                var middleLane = LiveConfig.JudgmentPositions[5];
                var endLane = LiveConfig.JudgmentPositions[2];
                harness.Step(2.05f, 12.05);
                harness.Step(2.4f, 12.4, LiveHarness.Touch(1, startLane, 12.4));
                harness.Step(2.5f, 12.5, LiveHarness.Touch(1, middleLane, 12.5, InputTouchPhase.Moved));
                Assert.That(hold.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.JustPerfect));
                harness.Step(3, 13, LiveHarness.Touch(1, endLane, 13, InputTouchPhase.Ended));
                Assert.That(hold.TailNote.Result, Is.EqualTo(NoteResult.JustPerfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void ClaimedActiveIntervalCannotCrossAGuideGap()
        {
            var head = N("NormalHeadTapNote", "head", 1);
            var anchor = N("AnchorNote", "anchor", 2);
            var tail = N("AnchorNote", "tail", 3);
            Add(head, R("next", "anchor"));
            Add(anchor, R("next", "tail"));
            Assert.Throws<InvalidDataException>(() => Load(Chart(head, anchor, tail,
                Segment("head", "anchor", "head", "anchor", "head", "tail"),
                Segment("anchor", "tail", "anchor", "tail"))));
        }

        [Test]
        public void StepConnectorKeepsEachSideAtItsOwnLane()
        {
            var head = N("NormalHeadTapNote", "head", 1, -3);
            var tail = N("NormalTailReleaseNote", "tail", 3, 3);
            Add(head, R("next", "tail"), V("connectorEase", 40));
            Add(tail, R("activeHead", "head"));
            var chart = Load(Chart(head, tail, Segment("head", "tail", "head", "tail", "head", "tail")));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            harness.Step(0.99f, 10.99);
            Assert.That(bar.LaneStartF, Is.EqualTo(2f));
            harness.Step(1f, 11);
            Assert.That(bar.LaneStartF, Is.EqualTo(8f));
        }

        [TestCase(30, 0.4, -0.09935168)]
        [TestCase(31, 0.6, 1.09935168)]
        [TestCase(32, 0.25, -0.09968184)]
        [TestCase(33, 0.25, 0.54384875)]
        [TestCase(34, 0.85, -0.35355339)]
        [TestCase(35, 0.15, 1.35355339)]
        [TestCase(36, 0.45, 0.04341204)]
        [TestCase(37, 0.075, 0.67677670)]
        public void OvershootingConnectorFunctionsKeepTheirSignedOutput(int ease, double fraction, double expected)
        {
            Assert.That(NextRushConnectorSegment.Interpolate(ease, 0), Is.Zero);
            Assert.That(NextRushConnectorSegment.Interpolate(ease, 1), Is.EqualTo(1));
            Assert.That(NextRushConnectorSegment.Interpolate(ease, (float)fraction), Is.EqualTo(expected).Within(0.00001));
        }

        [Test]
        public void OvershootingConnectorMovesBothTheHoldAndItsAttachedTickPastTheEndpoint()
        {
            var head = N("NormalHeadTapNote", "head", 1, -3);
            var tail = N("NormalTailReleaseNote", "tail", 3, 3);
            var tick = E("NormalTickNote", "tick", V("#BEAT", 1.8), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            Add(head, R("next", "tail"), V("connectorEase", 30));
            Add(tail, R("activeHead", "head"));
            var chart = Load(Chart(head, tail, tick, Segment("head", "tail", "head", "tail", "head", "tail")));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var attached = bar.NoteList.OfType<ConnectionNote>().Single();
            float expected = 2 - 0.09935168f * 6;
            Assert.That(attached.LaneStartF, Is.EqualTo(expected).Within(0.00001));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            harness.Step(0.9f, 10.9);
            Assert.That(bar.LaneStartF, Is.EqualTo(expected).Within(0.00001));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThroughJudgeLineGuideCanDrawAfterItsGeometryAndIndependentKeySettle(bool through)
        {
            var guide = N("AnchorNote", "guide", 1.8, -2);
            var tail = N("NormalTapNote", "tail", 2, 2);
            var styleHead = N("AnchorNote", "styleHead", 1.5);
            Add(styleHead, V("segmentKind", 103), V("segmentThroughJudgeLine", through ? 1 : 0));
            Add(guide, R("next", "tail"));
            var chart = Load(Chart(guide, tail, styleHead, Segment("guide", "tail", "styleHead", "tail")));
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("through-judge-line guide");
            var view = host.AddComponent<NoteLineView>();
            float originalViewLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                view.Setup(_config, "traceLine_eff.png", 1, guide: true);
                view.Add(bar);
                view.Excute(0.95f);
                Mesh mesh = host.GetComponent<MeshFilter>().sharedMesh;
                float judgeY = LiveConfig.JudgmentPositions[0].y;
                Assert.That(mesh.vertices.Any(v => v.y < judgeY - 0.01f), Is.EqualTo(through));

                Assert.That(bar.AutoJudgment(new MusicScoreInfo(0, 0.5f, 1.01f, 120, 4, 1, 1)), Is.True);
                Assert.That(bar.State, Is.EqualTo(NoteState.Done));
                view.Remove(bar);
                view.Excute(1.01f);
                Assert.That(view.BarCount, Is.EqualTo(through ? 1 : 0));
                Assert.That(mesh.vertices.Any(v => v.y < judgeY - 0.01f), Is.EqualTo(through));

                view.Excute(6f);
                Assert.That(view.BarCount, Is.Zero);
                Assert.That(mesh.vertices.All(v => v == Vector3.zero), Is.True);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = originalViewLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NativeConnectorEdgesUseTheirOwnSortingBatchAndStyleKind()
        {
            var head = N("AnchorNote", "head", 1);
            var a = N("AnchorNote", "a", 1.2);
            var b = N("AnchorNote", "b", 1.4);
            var c = N("AnchorNote", "c", 1.6);
            var end = N("AnchorNote", "end", 1.8);
            Add(head, R("next", "a"), V("segmentLayer", 2), V("segmentKind", 1));
            Add(a, R("next", "b"), V("segmentLayer", 1), V("segmentKind", 101));
            Add(b, R("next", "c"), V("segmentLayer", 0), V("segmentKind", 2));
            Add(c, R("next", "end"), V("segmentLayer", 3), V("segmentKind", 102));
            var chart = Load(Chart(head, a, b, c, end, Segment("head", "a", "head", "a"),
                Segment("a", "b", "a", "b"), Segment("b", "c", "b", "c"), Segment("c", "end", "c", "end")));
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var holdHost = new GameObject("holds");
            var guideHost = new GameObject("guides");
            float originalViewLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var holdView = holdHost.AddComponent<NoteLineView>();
                var guideView = guideHost.AddComponent<NoteLineView>();
                holdView.Setup(_config, "longNoteLine.png", 1);
                guideView.Setup(_config, "traceLine_eff.png", 1, guide: true);
                holdView.Add(bar);
                guideView.Add(bar);
                holdView.Excute(0);
                guideView.Excute(0);
                var holdMeshes = holdHost.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                var guideMeshes = guideHost.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                Assert.That(holdMeshes.Select(r => r.name), Is.EquivalentTo(new[] { "holds", "Under" }));
                Assert.That(guideMeshes.Select(r => r.name), Is.EquivalentTo(new[] { "Bottom", "Over" }));
                Assert.That(holdMeshes.Single(r => r.name == "Under").sortingOrder, Is.LessThan(-101));
                Assert.That(guideMeshes.Single(r => r.name == "Bottom").sortingOrder, Is.GreaterThan(-99)
                    .And.LessThan(holdHost.GetComponent<MeshRenderer>().sortingOrder));
                Assert.That(holdHost.GetComponent<MeshRenderer>().sortingOrder, Is.LessThan(0));
                Assert.That(guideMeshes.Single(r => r.name == "Over").sortingOrder, Is.GreaterThan(31));
                holdView.Clear();
                guideView.Clear();
                Assert.That(holdMeshes.Concat(guideMeshes).All(r => !r.enabled), Is.True);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = originalViewLead;
                UnityEngine.Object.DestroyImmediate(holdHost);
                UnityEngine.Object.DestroyImmediate(guideHost);
            }
        }

        [Test]
        public void FullScreenConnectorUsesCameraViewportChartTimeAndStyleGroupVisibility()
        {
            var head = N("AnchorNote", "head", 2);
            var end = N("AnchorNote", "end", 4);
            var style = N("AnchorNote", "style", 1);
            var styleEnd = N("AnchorNote", "styleEnd", 5);
            Add(head, R("next", "end"));
            Add(style, V("segmentKind", 103), V("segmentLayer", 3), V("segmentPresentation", 1),
                V("segmentThroughJudgeLine", 1), V("segmentAlpha", 0), R("#TIMESCALE_GROUP", "styleGroup"));
            var chart = Load(Chart(head, end, style, styleEnd, Segment("head", "end", "style", "styleEnd"),
                E("#TIMESCALE_GROUP", "styleGroup", R("first", "hide")),
                E("#TIMESCALE_CHANGE", "hide", V("#BEAT", 3.25), V("#TIMESCALE", 1), V("hideNotes", 1), R("next", "show")),
                E("#TIMESCALE_CHANGE", "show", V("#BEAT", 3.75), V("#TIMESCALE", 1))));
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            Assert.That(bar.NativeConnector.FullScreen, Is.True);
            Assert.That(bar.NativeConnector.Layer, Is.EqualTo(NextRushConnectorLayer.Over));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("full-screen connector");
            var cameraHost = new GameObject("full-screen test camera");
            float originalViewLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                cameraHost.transform.position = new Vector3(2, 3, -5);
                var camera = cameraHost.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 2.1f;
                var view = host.AddComponent<NoteLineView>();
                view.Setup(_config, "traceLine_eff.png", 1, viewCamera: camera, guide: true);
                view.Add(bar);
                view.Excute(0.99f);
                Assert.That(host.transform.Find("Over"), Is.Null);
                view.Excute(1.5f);
                var overlay = host.transform.Find("Over");
                Assert.That(overlay, Is.Not.Null);
                var mesh = overlay.GetComponent<MeshFilter>().sharedMesh;
                var renderer = overlay.GetComponent<MeshRenderer>();
                Assert.That(mesh.vertexCount, Is.EqualTo(8));
                var points = mesh.vertices.Select(v => camera.WorldToViewportPoint(overlay.TransformPoint(v))).ToArray();
                Assert.That(points.Min(v => v.x), Is.Zero.Within(0.00001));
                Assert.That(points.Max(v => v.x), Is.EqualTo(1).Within(0.00001));
                Assert.That(points.Min(v => v.y), Is.Zero.Within(0.00001));
                Assert.That(points.Max(v => v.y), Is.EqualTo(1).Within(0.00001));
                Assert.That(mesh.colors.All(color => Mathf.Abs(color.a - 0.5f) < 0.00001f), Is.True);
                Assert.That(mesh.uv.All(uv => uv.x == 1), Is.True);
                view.Excute(1.75f);
                Assert.That(renderer.enabled, Is.False);
                view.Excute(1.9f);
                Assert.That(renderer.enabled, Is.True);
                view.Excute(2f);
                Assert.That(renderer.enabled, Is.False);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = originalViewLead;
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(cameraHost);
            }
        }

        [Test]
        public void OrdinaryHeadStaysOnTheLineAfterPassingAnAnchor()
        {
            var head = N("NormalHeadTapNote", "head", 1);
            var anchor = N("AnchorNote", "anchor", 2);
            var tail = N("NormalTailReleaseNote", "tail", 4);
            Add(head, R("next", "anchor"));
            Add(anchor, R("next", "tail"), R("#TIMESCALE_GROUP", "anchorGroup"));
            Add(tail, R("activeHead", "head"));
            var chart = Load(Chart(head, anchor, tail,
                Segment("head", "anchor", "head", "tail", "head", "tail"),
                Segment("anchor", "tail", "anchor", "tail", "head", "tail"),
                E("#TIMESCALE_GROUP", "anchorGroup", R("first", "hide")),
                E("#TIMESCALE_CHANGE", "hide", V("#BEAT", 2.5), V("#TIMESCALE", 1), V("hideNotes", 1), R("next", "show")),
                E("#TIMESCALE_CHANGE", "show", V("#BEAT", 3), V("#TIMESCALE", 1))));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);

            var host = new GameObject("native long head view");
            float originalViewLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0f;
                var view = host.AddComponent<LongNoteView>();
                view.Setup(new Vector2(0, 6), LiveConfig.JudgmentPositions, 1);
                view.Spawn(bar, 0);

                view.Move(0.8f);
                Assert.That(host.transform.localScale.x, Is.EqualTo(1f));
                view.Move(1.1f);
                Assert.That(host.transform.localScale.x, Is.EqualTo(1f));
                view.Move(1.3f);
                Assert.That(host.transform.localScale.x, Is.Zero);
                view.Move(1.6f);
                Assert.That(host.transform.localScale.x, Is.EqualTo(1f));
                view.Move(2f);
                Assert.That(host.transform.localScale.x, Is.Zero);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = originalViewLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
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
