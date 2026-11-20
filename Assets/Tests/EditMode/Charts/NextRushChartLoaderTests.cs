using System;
using System.Collections.Generic;
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
using UnityEngine.Rendering;
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

        [TestCase(false)]
        [TestCase(true)]
        public void ConnectorLoopOwnershipFollowsTheLatestActivationOnEachChannel(bool auto)
        {
            var entities = new System.Collections.Generic.List<JObject>();
            void HoldAt(string name, int start, int end, int lane, int kind)
            {
                string tail = name + "End";
                var head = N("NormalHeadTapNote", name, start, lane);
                Add(head, V("segmentKind", kind), R("next", tail));
                entities.Add(head);
                entities.Add(N("AnchorNote", tail, end, lane));
                entities.Add(Segment(name, tail, name, tail, name, tail));
            }
            HoldAt("first", 2, 10, -4, 11);
            HoldAt("otherChannel", 3, 9, 0, 22);
            HoldAt("replacement", 4, 6, 4, 1);
            entities.Add(N("NormalTapNote", null, 12));
            using var h = new LiveHarness();
            h.Arrange(Load(Chart(entities.ToArray())).Score, auto: auto);
            h.BindViews();
            var events = new System.Collections.Generic.List<string>();
            uint next = 0;
            h.Logic.ConnectorAudio.Play = cue => { events.Add(cue); return ++next; };
            h.Logic.ConnectorAudio.Stop = id => events.Add("stop" + id);
            void Step(float time, params LiveTouchSample[] touches)
            {
                h.Step(time, 10d + time, touches);
                if (auto) h.Logic.OnAutoInput();
            }
            LiveTouchSample Finger(int id, int lane, float time, InputTouchPhase phase = InputTouchPhase.Began)
                => LiveHarness.Touch(id, LiveConfig.NativeLanePosition(lane + 5.5f), 10d + time, phase);
            float first = auto ? 1f : .96f;
            Step(first, Finger(1, -4, first));
            Step(1.5f, Finger(2, 0, 1.5f), Finger(1, -4, 1.5f, InputTouchPhase.Stationary));
            Step(2, Finger(3, 4, 2), Finger(2, 0, 2, InputTouchPhase.Stationary), Finger(1, -4, 2, InputTouchPhase.Stationary));
            Step(3, Finger(1, -4, 3, InputTouchPhase.Stationary), Finger(2, 0, 3, InputTouchPhase.Stationary));
            Assert.That(events, Is.EqualTo(new[] { LiveSoundDefine.SE_LIVE_LONG, LiveSoundDefine.SE_LIVE_LONG_CRITICAL,
                "stop1", LiveSoundDefine.SE_LIVE_LONG, "stop3" }));
            if (!auto)
            {
                Step(3.1f, Finger(1, -4, 3.1f, InputTouchPhase.Ended), Finger(2, 0, 3.1f, InputTouchPhase.Stationary));
                Step(3.2f, Finger(4, -4, 3.2f), Finger(2, 0, 3.2f, InputTouchPhase.Stationary));
                Assert.That(events.Last(), Is.EqualTo(LiveSoundDefine.SE_LIVE_LONG));
            }
            Step(5.1f);
            Assert.That(events.Count(e => e.StartsWith("stop")), Is.EqualTo(next));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FakeHoldSoundStopsWhileHiddenAndRestartsWhenVisible(bool auto)
        {
            var head = N("FakeNormalHeadTapNote", "head", 2);
            Add(head, R("next", "end"), V("segmentKind", 52), R("#TIMESCALE_GROUP", "group"));
            var score = Load(Chart(head, N("AnchorNote", "end", 6), N("NormalTapNote", null, 8),
                Segment("head", "end", "head", "end", "head", "end"),
                E("#TIMESCALE_GROUP", "group", R("first", "hide")),
                E("#TIMESCALE_CHANGE", "hide", V("#BEAT", 3), V("#TIMESCALE", 1), V("hideNotes", 1), R("next", "show")),
                E("#TIMESCALE_CHANGE", "show", V("#BEAT", 4), V("#TIMESCALE", 1)))).Score;
            using var h = new LiveHarness();
            h.Arrange(score, auto: auto);
            var events = new System.Collections.Generic.List<string>();
            h.Logic.ConnectorAudio.Play = cue => { events.Add(cue); return 42; };
            h.Logic.ConnectorAudio.Stop = id => events.Add("stop" + id);
            foreach (float now in new[] { .95f, 1f, 1.5f, 1.9f, 2f, 3f })
            {
                h.Step(now, 10d + now);
                if (auto) h.Logic.OnAutoInput();
            }
            Assert.That(events, Is.EqualTo(new[] { LiveSoundDefine.SE_LIVE_LONG_CRITICAL, "stop42",
                LiveSoundDefine.SE_LIVE_LONG_CRITICAL, "stop42" }));
        }

        [TestCase(false, LiveSoundDefine.SE_LIVE_LONG, "NormalHeadTraceNote")]
        [TestCase(true, LiveSoundDefine.SE_LIVE_LONG_CRITICAL, "NormalHeadTraceNote")]
        [TestCase(false, LiveSoundDefine.SE_LIVE_LONG, "NormalTraceNote")]
        [TestCase(true, LiveSoundDefine.SE_LIVE_LONG_CRITICAL, "CriticalTraceNote")]
        public void TraceLoopUsesTheAuthoredHeadAndAutoUsesTheFirstSegment(bool auto, string expectedCue, string archetype)
        {
            var head = N(archetype, "head", 2, -3);
            Add(head, V("segmentKind", 1), R("next", "end"));
            var style = N("AnchorNote", "style", 1);
            Add(style, V("segmentKind", 26));
            var score = Load(Chart(head, N("AnchorNote", "end", 4, 3), style, N("AnchorNote", "styleEnd", 5),
                Segment("head", "end", "style", "styleEnd", "head", "end"), N("NormalTapNote", null, 6))).Score;
            using var h = new LiveHarness();
            h.Arrange(score, auto: auto);
            h.BindViews();
            var events = new System.Collections.Generic.List<string>();
            h.Logic.ConnectorAudio.Play = cue => { events.Add(cue); return 1; };
            h.Logic.ConnectorAudio.Stop = id => events.Add("stop");
            h.Step(.96f, 10.96, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(2.5f), 10.96));
            if (auto) h.Logic.OnAutoInput();
            Assert.That(events, Is.Empty);
            h.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(2.5f), 11, InputTouchPhase.Ended));
            if (auto) h.Logic.OnAutoInput();
            Assert.That(events, Is.EqualTo(new[] { expectedCue }));
            h.Step(1.02f, 11.02);
            if (auto) h.Logic.OnAutoInput();
            Assert.That(events.Count, Is.EqualTo(auto ? 1 : 2));
            h.Step(2f, 12);
            if (auto) h.Logic.OnAutoInput();
            Assert.That(events, Is.EqualTo(new[] { expectedCue, "stop" }));
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

        [TestCase(null, 1000)]
        [TestCase(0, 0)]
        [TestCase(65, 65)]
        [TestCase(2500, 2500)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public void InitialLifeComesFromTheNativeChart(int? authored, int expected)
        {
            var chart = Chart(N("NormalTapNote", "tap", 4));
            if (authored.HasValue) Add((JObject)chart["entities"][0], V("initialLife", authored.Value));
            var definition = Load(chart);
            var board = new ScoreLogic(_config);
            board.Setup(null, definition.Score);
            Assert.That(definition.NextRushCompatibility.InitialLife, Is.EqualTo(expected));
            Assert.That(board.Score.life, Is.EqualTo(expected));

            board.Setup(null, new MusicScore());
            Assert.That(board.Score.life, Is.EqualTo(_config.Life));
        }

        [TestCase(-1d)]
        [TestCase(10.5d)]
        [TestCase(2147483648d)]
        public void InvalidInitialLifeStopsAtPreflight(double authored)
        {
            var chart = Chart(N("NormalTapNote", "tap", 4));
            Add((JObject)chart["entities"][0], V("initialLife", authored));
            Assert.That(() => Load(chart), Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("Initialization.initialLife"));
        }

        [Test]
        public void AuthoredLifeDrainsOnContactAndRestoresAtSetup()
        {
            var chart = Chart(N("DamageNote", "first", 4), N("DamageNote", "second", 5),
                N("NormalTapNote", "later", 8));
            Add((JObject)chart["entities"][0], V("initialLife", 65));
            var score = Load(chart).Score;
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.BindViews();
            var board = new ScoreLogic(_config);
            harness.Logic.SetScoreLogic(board);
            var center = LiveConfig.NativeLanePosition(5.5f);
            harness.Step(2f, 12, LiveHarness.Touch(1, center, 12));
            Assert.That(board.Score.life, Is.EqualTo(25));
            harness.Step(2.5f, 12.5, LiveHarness.Touch(2, center, 12.5));
            Assert.That(board.Score.life, Is.Zero);
            Assert.That(harness.Logic.Result, Is.EqualTo(2));

            harness.Logic.Session.Reset(new LivePresentationBridge());
            harness.Logic.SetScoreLogic(board);
            Assert.That(board.Score.life, Is.EqualTo(65));
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
        public void MeterChangesPreserveAbsoluteBeatTiming()
        {
            var chart = Chart(
                E("#BPM_CHANGE", null, V("#BEAT", 3), V("#BPM", 240), V("meter", 5)),
                N("NormalTapNote", null, 1.5), N("NormalTapNote", null, 3), N("NormalTapNote", null, 8));
            Add((JObject)chart["entities"][1], V("meter", 3));

            var times = Load(chart).Score.NoteArray.Select(note => note.MusicScoreInfo.time).ToArray();
            Assert.That(times, Is.EqualTo(new[] { 0.75f, 1.5f, 2.75f }));
        }

        [Test]
        public void MeterMustBeNumericWhenPresent()
        {
            var chart = Chart(N("NormalTapNote", "tap", 1));
            Add((JObject)chart["entities"][1], R("meter", "tap"));
            Assert.That(() => Load(chart), Throws.TypeOf<InvalidDataException>().With.Message.Contains("meter"));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void MeterChangesPreserveGroupedScrollDistance(int transitionStyle)
        {
            var tap = N("NormalTapNote", "tap", 8);
            Add(tap, R("#TIMESCALE_GROUP", "group"));
            var chart = Chart(tap,
                E("#BPM_CHANGE", null, V("#BEAT", 2), V("#BPM", 120), V("meter", 4)),
                E("#BPM_CHANGE", null, V("#BEAT", 4), V("#BPM", 240), V("meter", 4)),
                E("#TIMESCALE_GROUP", "group", R("first", "fast")),
                E("#TIMESCALE_CHANGE", "fast", V("#BEAT", 0), V("#TIMESCALE", 2), R("next", "stop"), V("transitionStyle", transitionStyle)),
                E("#TIMESCALE_CHANGE", "stop", V("#BEAT", 2), V("#TIMESCALE", 0), R("next", "reverse"), V("transitionStyle", transitionStyle)),
                E("#TIMESCALE_CHANGE", "reverse", V("#BEAT", 4), V("#TIMESCALE", -1), V("#TIMESCALE_SKIP", 1), R("next", "resume"), V("transitionStyle", transitionStyle)),
                E("#TIMESCALE_CHANGE", "resume", V("#BEAT", 6), V("#TIMESCALE", 1), V("transitionStyle", transitionStyle)));
            var baseline = Load(chart).Score.NoteArray.Single();
            var meters = chart.SelectTokens("$.entities[*].data[?(@.name == 'meter')].value").ToArray();
            meters[0].Replace(new JValue(3));
            meters[1].Replace(new JValue(5));
            var changed = Load(chart).Score.NoteArray.Single();

            double Distance(NoteBase note, double time) =>
                note.NativeScroll.Distance(note.NativeScroll.Sample(time), note.NativeTarget);
            Assert.That(changed.NativeTarget.Time, Is.EqualTo(3));
            foreach (double time in new[] { 0.5, 0.999, 1, 1.5, 1.999, 2, 2.25, 2.499, 2.5, 2.75, 3, 3.25 })
                Assert.That(Distance(changed, time), Is.EqualTo(Distance(baseline, time)).Within(1e-9), $"t={time}");

            if (transitionStyle == 0)
            {
                // Stop at 1 s. At 2 s, skip one beat (0.25 s at 240 BPM), then reverse for 0.5 s.
                Assert.That(Distance(changed, 1.5), Is.EqualTo(0.25).Within(1e-9));
                Assert.That(Distance(changed, 2), Is.Zero.Within(1e-9));
            }
            Assert.That(Distance(changed, 2.25), Is.EqualTo(transitionStyle == 0 ? 0.25 : -0.75).Within(1e-9));
            Assert.That(Distance(changed, 2.75), Is.EqualTo(0.25).Within(1e-9));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void MeterChangesPreserveCurvedHoldMeshDuringGroupSpeedChanges(int transitionStyle)
        {
            var head = N("NormalHeadTapNote", "head", 1, -4, 0.75);
            var bend = N("AnchorNote", "bend", 5, 3, 2);
            var tail = N("NormalTailReleaseNote", "tail", 8, -2, 1);
            Add(head, R("next", "bend"), V("connectorEase", 2));
            Add(bend, R("next", "tail"), V("connectorEase", 10), R("#TIMESCALE_GROUP", "group"));
            Add(tail, R("activeHead", "head"));

            JObject MakeChart(int stopMeter, int reverseMeter) => Chart(head, bend, tail,
                Segment("head", "bend", "head", "bend", "head", "tail"),
                Segment("bend", "tail", "bend", "tail", "head", "tail"),
                E("#BPM_CHANGE", null, V("#BEAT", 2), V("#BPM", 120), V("meter", stopMeter)),
                E("#BPM_CHANGE", null, V("#BEAT", 4), V("#BPM", 240), V("meter", reverseMeter)),
                E("#TIMESCALE_GROUP", "group", R("first", "stop")),
                E("#TIMESCALE_CHANGE", "stop", V("#BEAT", 2), V("#TIMESCALE", 0), V("transitionStyle", transitionStyle), R("next", "reverse")),
                E("#TIMESCALE_CHANGE", "reverse", V("#BEAT", 4), V("#TIMESCALE", -1), V("transitionStyle", transitionStyle), R("next", "resume")),
                E("#TIMESCALE_CHANGE", "resume", V("#BEAT", 6), V("#TIMESCALE", 1), V("transitionStyle", transitionStyle)));

            var times = new[] { 0.375f, 0.999f, 1f, 1.5f, 1.999f, 2f, 2.125f, 2.25f, 2.5f, 2.75f };
            Vector3[][] Capture(JObject chart)
            {
                var score = Load(chart).Score;
                using var harness = new LiveHarness();
                harness.Arrange(score, auto: true);
                harness.Logic.Notes.BindNativeVisuals(2);
                var host = new GameObject("curved hold meter comparison");
                float previousLead = LiveConfig.ViewLeadSeconds;
                try
                {
                    LiveConfig.ViewLeadSeconds = 0;
                    var view = host.AddComponent<NoteLineView>();
                    view.Setup(_config, "longNoteLine.png", 1);
                    view.Add(score.NoteArray.OfType<LongNote>().Single());
                    return times.Select(time =>
                    {
                        harness.OnUpdate(time, 10d + time);
                        view.Excute(time);
                        return host.GetComponent<MeshFilter>().sharedMesh.vertices;
                    }).ToArray();
                }
                finally
                {
                    LiveConfig.ViewLeadSeconds = previousLead;
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }

            var baseline = Capture(MakeChart(4, 4));
            var changed = Capture(MakeChart(3, 5));
            for (int i = 0; i < times.Length; i++)
            {
                Assert.That(baseline[i].Distinct().Count(), Is.GreaterThan(4), $"Empty or collapsed ribbon at {times[i]}");
                Assert.That(changed[i], Is.EqualTo(baseline[i]), $"Hold mesh at {times[i]}");
            }
        }

        [TestCase("NormalFlickNote", NoteDirection.Down, 0, 60, false, NoteResult.Perfect)]
        [TestCase("NormalFlickNote", NoteDirection.DownLeft, -60, -60, false, NoteResult.Perfect)]
        [TestCase("NormalFlickNote", NoteDirection.DownLeft, 60, 60, false, NoteResult.Great)]
        [TestCase("CriticalTraceFlickNote", NoteDirection.DownRight, 60, -60, false, NoteResult.Perfect)]
        [TestCase("CriticalTraceFlickNote", NoteDirection.DownRight, -60, 60, false, NoteResult.Great)]
        [TestCase("NormalTailFlickNote", NoteDirection.DownRight, 60, -60, false, NoteResult.Perfect)]
        [TestCase("CriticalTailTraceFlickNote", NoteDirection.DownLeft, -60, -60, false, NoteResult.Perfect)]
        [TestCase("NormalFlickNote", NoteDirection.DownLeft, 60, -60, true, NoteResult.Perfect)]
        public void DownFlickDirectionsReachTheLiveJudge(string archetype, NoteDirection direction,
            int dx, int dy, bool mirror, NoteResult expected)
        {
            var flick = N(archetype, "flick", 4, -2);
            Add(flick, V("direction", (int)direction));
            bool isTail = archetype.Contains("Tail");
            var chart = Chart(flick);
            if (isTail)
            {
                var head = N("NormalHeadTapNote", "head", 2, -2);
                Add(head, R("next", "flick"));
                Add(flick, R("activeHead", "head"));
                chart = Chart(head, flick, E("Connector", null,
                    R("head", "head"), R("tail", "flick"), R("segmentHead", "head"), R("segmentTail", "flick"),
                    R("activeHead", "head"), R("activeTail", "flick")));
            }

            var source = NextRushChart.Parse(chart.ToString());
            var score = NextRushChartLoader.Load(source, _config, isMirror: mirror).Score;
            var root = score.NoteArray.Single();
            var key = isTail ? ((LongNote)root).ChildNote : root;
            Assert.That(key.Direction, Is.EqualTo(mirror ? NoteDirection.DownRight : direction));
            Assert.That(key.LaneStartF, Is.EqualTo(mirror ? 7f : 3f));

            using var harness = new LiveHarness();
            harness.Arrange(score);
            var lane = LiveConfig.JudgmentPositions[mirror ? 7 : 4];
            float originalFrameDelta = LiveUtility.FrameDeltaTimeOverride;
            try
            {
                LiveUtility.FrameDeltaTimeOverride = 1f / 60f;
                float press = isTail ? 1f : 1.95f;
                harness.Step(press, 10d + press, LiveHarness.Touch(1, lane, 10d + press));
                harness.Step(1.98f, 11.98, LiveHarness.Touch(1, lane, 11.98, InputTouchPhase.Stationary));
                float swipe = archetype.Contains("Trace") ? 1.99f : 2f;
                var delta = new Vector2(dx, dy);
                var screen = new Vector2(800, 600) + delta;
                harness.Step(swipe, 10d + swipe,
                    new LiveTouchSample(1, screen, lane, 10d + swipe, InputTouchPhase.Moved, delta));
                if (swipe < 2f)
                    harness.Step(2f, 12d, new LiveTouchSample(1, screen, lane, 12d, InputTouchPhase.Stationary, Vector2.zero));
                if (key is FrictionFlickNote && expected == NoteResult.Great)
                {
                    Assert.That(key.Result, Is.EqualTo(NoteResult.None));
                    harness.Step(2.02f, 12.02);
                    harness.Step(2.04f, 12.04);
                }
                Assert.That(key.Result, Is.EqualTo(expected));
            }
            finally { LiveUtility.FrameDeltaTimeOverride = originalFrameDelta; }
        }

        [TestCase("NormalTraceNote", 4f, 0f, InputTouchPhase.Moved, NoteResult.Perfect)]
        [TestCase("NormalTraceNote", 4f, 3.5f, InputTouchPhase.Moved, NoteResult.Perfect)]
        [TestCase("NormalTraceNote", 0f, 3.51f, InputTouchPhase.Moved, NoteResult.Miss)]
        [TestCase("NormalTraceNote", 4f, 0f, InputTouchPhase.Canceled, NoteResult.Miss)]
        [TestCase("NormalTapNote", 4f, 0f, InputTouchPhase.Moved, NoteResult.Miss)]
        [TestCase("NormalReleaseNote", 4f, 0f, InputTouchPhase.Ended, NoteResult.Perfect)]
        public void NativeHitboxChecksCurrentTouchHeight(string kind, float startY, float endY,
            InputTouchPhase phase, NoteResult expected)
        {
            var score = Load(Chart(N(kind, "key", 4))).Score;
            using var harness = new LiveHarness(_config);
            harness.Arrange(score);
            var lane = LiveConfig.NativeLanePosition(5.5f);
            harness.Step(1.8f, 11.8, LiveHarness.Touch(1, lane + Vector2.up * startY, 11.8));
            harness.Step(2f, 12, LiveHarness.Touch(1, lane + Vector2.up * endY, 12, phase));
            harness.Step(2.2f, 12.2);
            Assert.That(score.NoteArray.Single().Result, Is.EqualTo(expected));
        }

        [TestCase("NormalTraceFlickNote", -6, NoteResult.Perfect)]
        [TestCase("NormalTraceFlickNote", -7, NoteResult.Miss)]
        [TestCase("CriticalTraceFlickNote", 7, NoteResult.Perfect)]
        [TestCase("CriticalTraceFlickNote", 8, NoteResult.Miss)]
        public void StandaloneTraceFlickUsesItsAuthoredInputWindow(string kind, int frames, NoteResult expected)
        {
            var score = Load(Chart(N(kind, "key", 4))).Score;
            using var harness = new LiveHarness();
            harness.Arrange(score);
            var lane = LiveConfig.NativeLanePosition(5.5f);
            float swipe = 2f + frames / 60f;
            harness.Step(swipe - 0.02f, 10d + swipe - 0.02, LiveHarness.Touch(1, lane, 10d + swipe - 0.02));
            var delta = new Vector2(0, 200);
            harness.Step(swipe, 10d + swipe, new LiveTouchSample(1, new Vector2(800, 800),
                lane, 10d + swipe, InputTouchPhase.Moved, delta));
            if (frames < 0) harness.Step(2f, 12d);
            harness.Step(2.2f, 12.2);
            harness.Step(2.21f, 12.21);
            Assert.That(score.NoteArray.Single().Result, Is.EqualTo(expected));
        }

        [TestCase("custom01", "FlickNote", "NormalFlickNote")]
        [TestCase("custom01", "FrictionFlickCrtNote", "CriticalTraceFlickNote")]
        [TestCase("custom02", "FlickCrtNote", "CriticalFlickNote")]
        [TestCase("custom02", "FrictionFlickNote", "NormalTraceFlickNote")]
        public void DownFlickArrowsApproachTheKeyFromAboveAndResetWhenReused(string skin, string prefab, string archetype)
        {
            var view = UnityEngine.Object.Instantiate(Resources.Load<FlickNoteView>("live/note/" + skin + "/" + prefab));
            try
            {
                using var harness = new LiveHarness();
                var omni = view.transform.Find("DefaultArrow").GetComponent<SpriteRenderer>();
                var slanted = view.transform.Find("RotateRoot/MoveRoot");
                foreach (var direction in new[] { NoteDirection.Down, NoteDirection.DownLeft, NoteDirection.DownRight,
                    NoteDirection.Default, NoteDirection.Left, NoteDirection.Right })
                {
                    var entity = N(archetype, "flick", 4);
                    Add(entity, V("direction", (int)direction));
                    var score = Load(Chart(entity)).Score;
                    harness.Arrange(score);
                    harness.Step(1.8f, 11.8);
                    view.Spawn(score.NoteArray.Single(), 0);
                    view.Move(1.8f);
                    bool down = direction >= NoteDirection.Down;
                    bool isOmni = direction is NoteDirection.Default or NoteDirection.Down;
                    Vector3 Offset() => isOmni ? omni.transform.localPosition
                        : slanted.parent.localRotation * Vector3.Scale(slanted.localPosition, slanted.parent.localScale);
                    if (isOmni) Assert.That(omni.flipY, Is.EqualTo(down));
                    var first = Offset();
                    Assert.That(first.y, Is.GreaterThan(0f));
                    if (!isOmni)
                    {
                        bool left = direction is NoteDirection.Left or NoteDirection.DownLeft;
                        Assert.That(Mathf.Sign(first.x), Is.EqualTo(left != down ? -1f : 1f));
                    }
                    view.Move(1.9f);
                    Assert.That(Offset().y, down ? Is.LessThan(first.y) : Is.GreaterThan(first.y));
                    view.Unspawn();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
        }

        [TestCase(-1)]
        [TestCase(3.5)]
        [TestCase(6)]
        public void InvalidFlickDirectionIsRejected(double direction)
        {
            var flick = N("NormalFlickNote", "flick", 4);
            Add(flick, V("direction", direction));
            Assert.That(() => Load(Chart(flick)), Throws.TypeOf<InvalidDataException>().With.Message.Contains("direction="));
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
            var json = Chart(E("StaticStage", null), E("FeverStartV2", null), E("CameraChange", null));
            var report = NextRushCompatibility.Analyze(NextRushChart.Parse(json.ToString()));

            Assert.That(report.PassesPreflight, Is.False);
            Assert.That(report.Archetypes.Single(a => a.Archetype == "StaticStage").Status, Is.EqualTo(NextRushEntityStatus.RuntimeOnly));
            Assert.That(report.Archetypes.Single(a => a.Archetype == "FeverStartV2").Status, Is.EqualTo(NextRushEntityStatus.Unknown));
            Assert.That(report.Archetypes.Single(a => a.Archetype == "CameraChange").Status, Is.EqualTo(NextRushEntityStatus.Unsupported));
            Assert.That(() => Load(Chart(E("FeverStartV2", null))), Throws.TypeOf<InvalidDataException>().With.Message.Contains("FeverStartV2"));
        }

        [TestCase("Stage")]
        public void UnsupportedArchetypesAreRefused(string archetype)
        {
            Assert.Throws<InvalidDataException>(() => Load(Chart(N(archetype, null, 1))));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AllFakeKeysRetireSilentlyWithoutTakingTheRealTap(bool auto)
        {
            string[] kinds = { "Tap", "Flick", "Trace", "TraceFlick", "Release", "HeadTap", "HeadFlick",
                "HeadTrace", "HeadTraceFlick", "HeadRelease", "TailTap", "TailFlick", "TailTrace",
                "TailTraceFlick", "TailRelease", "Tick" };
            var entities = (from prefix in new[] { "FakeNormal", "FakeCritical" }
                            from kind in kinds
                            select N(prefix + kind + "Note", null, 4)).ToList();
            entities.AddRange(new[] { N("FakeAnchorNote", null, 4), N("FakeDamageNote", null, 4),
                N("FakeTransientHiddenTickNote", null, 4), N("FakeTransientHiddenDamageTickNote", null, 4),
                N("NormalTapNote", null, 4) });
            var definition = Load(Chart(entities.ToArray()));
            var score = definition.Score;
            var fakes = score.NoteArray.Where(n => !n.HasJudgment).ToArray();
            var tap = score.NoteArray.Single(n => n.HasJudgment);
            Assert.That(definition.TotalNoteCount, Is.EqualTo(1));
            Assert.That(fakes.Length, Is.EqualTo(36));
            Assert.That(fakes.Count(n => n.IsVisible), Is.EqualTo(33));
            Assert.That(fakes.Count(n => n.Type == NoteType.Critical), Is.EqualTo(16));

            using var harness = new LiveHarness();
            harness.Arrange(score, auto: auto);
            harness.BindViews();
            int fakeFeedback = 0;
            foreach (var fake in fakes) fake.OnJudgment = _ => fakeFeedback++;
            var board = new ScoreLogic(_config);
            board.Setup(null, score);
            harness.Logic.Session.ScoreLogic = board;
            int life = board.Score.life;
            harness.Step(1.96f, 11.96);
            if (auto)
            {
                harness.Step(2f, 12);
                harness.Logic.OnAutoInput();
            }
            else harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(5.5f), 12));
            harness.Step(2.2f, 12.2);
            Assert.That(tap.Result, Is.EqualTo(auto ? NoteResult.Auto : NoteResult.Perfect));
            Assert.That(fakes.All(n => n.State == NoteState.Done && n.Result == NoteResult.Pass), Is.True);
            Assert.That(board.Score.totalScore, Is.EqualTo(1000000));
            Assert.That(board.Score.missCount, Is.Zero);
            Assert.That(board.Score.life, Is.EqualTo(life));
            Assert.That(fakeFeedback, Is.Zero);

            harness.Logic.Session.Reset(new LivePresentationBridge());
            Assert.That(fakes.All(n => n.State == NoteState.Wait && n.Result == NoteResult.None), Is.True);
        }

        [TestCase(51)]
        [TestCase(52)]
        [TestCase(53)]
        [TestCase(64)]
        [TestCase(77)]
        [TestCase(88)]
        public void FakeConnectorsFollowThePathWithoutRegisteringDamage(int kind)
        {
            bool damage = kind is 53 or 88;
            var head = N(damage ? "FakeDamageNote" : "FakeNormalHeadTapNote", "head", 4, -3);
            var joint = N("FakeNormalTickNote", "joint", 5);
            var tail = N(damage ? "FakeDamageNote" : "FakeCriticalTailFlickNote", "tail", 6, 3);
            Add(head, V("segmentKind", kind), R("next", "joint"));
            Add(joint, R("next", "tail"));
            Add(tail, V("segmentKind", 0));
            if (!damage)
                foreach (var node in new[] { head, joint, tail }) Add(node, R("activeHead", "head"));
            string activeHead = damage ? null : "head", activeTail = damage ? null : "tail";
            var tick = E("FakeTransientHiddenDamageTickNote", "tick", V("#BEAT", 4.5), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "joint"));
            var definition = Load(Chart(head, joint, tail, tick,
                N("NormalTapNote", null, 5),
                Segment("head", "joint", "head", "tail", activeHead, activeTail),
                Segment("joint", "tail", "head", "tail", activeHead, activeTail)));
            var score = definition.Score;
            var bar = score.NoteArray.OfType<GuideNote>().Single();
            Assert.That(definition.TotalNoteCount, Is.EqualTo(1));
            Assert.That(bar.LineNoteList.Count, Is.EqualTo(3));
            Assert.That(bar.NativeConnector.IsFake, Is.True);
            Assert.That(bar.NativeConnector.IsDamage, Is.EqualTo(damage));
            Assert.That(bar.NativeConnector.IsCritical, Is.EqualTo(kind is 52 or 77));
            Assert.That(bar.TracksDamageContact, Is.False);
            Assert.That(bar.UpdateConnectorHeadView(2.25), Is.True);
            Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(3.5f).Within(1e-6f));
            Assert.That(bar.UpdateConnectorHeadView(2.75), Is.True);
            Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(6.5f).Within(1e-6f));
            Assert.That(bar.UpdateConnectorHeadView(3), Is.False);

            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.BindViews();
            var board = new ScoreLogic(_config);
            board.Setup(null, score);
            harness.Logic.Session.ScoreLogic = board;
            int life = board.Score.life;
            harness.Step(2.25f, 12.25, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(4f), 12.25));
            harness.Step(2.5f, 12.5, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(5.5f), 12.5));
            harness.Step(3.05f, 13.05);
            Assert.That(board.Score.life, Is.EqualTo(life));
            Assert.That(board.Score.missCount, Is.Zero);
            Assert.That(board.Score.totalScore, Is.EqualTo(1000000));
            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
        }

        [Test]
        public void FakeTickCanShapeARealHoldWithoutBecomingAPoint()
        {
            var chart = Hold();
            var entities = (JArray)chart["entities"];
            var joint = entities.OfType<JObject>().Single(e => (string)e["name"] == "middle");
            joint["archetype"] = "FakeNormalTickNote";
            var definition = Load(chart);
            var score = definition.Score;
            var bar = score.NoteArray.OfType<LongNote>().Single();
            var fake = score.NoteArray.OfType<ConnectionNote>().Single();
            Assert.That(definition.TotalNoteCount, Is.EqualTo(3));
            Assert.That(bar.LineNoteList.Count, Is.EqualTo(3));
            Assert.That(bar.LineNoteList[1].LaneStartF, Is.EqualTo(fake.LaneStartF));
            Assert.That(bar.NoteList.Contains(fake), Is.False);

            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(0.5f, 10.5, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(3.5f), 10.5));
            harness.Step(0.75f, 10.75, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(4.5f), 10.75, InputTouchPhase.Stationary));
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(5.5f), 11, InputTouchPhase.Stationary));
            harness.Step(1.5f, 11.5, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(7.5f), 11.5, InputTouchPhase.Ended));
            Assert.That(bar.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(bar.NoteList.OfType<LongHoldCombo>().Single().Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(bar.ChildNote.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(fake.Result, Is.EqualTo(NoteResult.Pass));
        }

        [Test]
        public void RealHeadTapOnAFakeRailKeepsItsOwnJudgment()
        {
            var head = N("NormalHeadTapNote", "head", 4);
            var tail = N("FakeAnchorNote", "tail", 6);
            Add(head, V("segmentKind", 51), R("next", "tail"), R("activeHead", "head"));
            Add(tail, R("activeHead", "head"));
            var definition = Load(Chart(head, tail, Segment("head", "tail", "head", "tail", "head", "tail")));
            var score = definition.Score;
            var key = score.NoteArray.Single(note => note.HasJudgment);
            Assert.That(key, Is.TypeOf<NormalNote>());
            Assert.That(definition.TotalNoteCount, Is.EqualTo(1));

            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(5.5f), 12));
            Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [Test]
        public void RealTicksOnAFakeHoldUseItsAlwaysActiveState()
        {
            var head = N("FakeNormalHeadTapNote", "head", 4, -2);
            var middle = N("NormalTickNote", "middle", 5, 0);
            var tail = N("FakeNormalTailReleaseNote", "tail", 6, 2);
            Add(head, V("segmentKind", 51), R("next", "middle"), R("activeHead", "head"));
            Add(middle, R("next", "tail"), R("activeHead", "head"));
            Add(tail, R("activeHead", "head"));
            var definition = Load(Chart(head, middle, tail,
                Segment("head", "middle", "head", "tail", "head", "tail"),
                Segment("middle", "tail", "head", "tail", "head", "tail"),
                Attached("NormalTickNote", 4.5), Attached("TransientHiddenTickNote", 4.75)));
            var score = definition.Score;
            var ticks = score.NoteArray.Where(note => note.HasJudgment).ToArray();
            Assert.That(definition.TotalNoteCount, Is.EqualTo(3));
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(2.25f, 12.25);
            harness.Step(2.375f, 12.375);
            harness.Step(2.5f, 12.5);
            Assert.That(ticks.Select(tick => tick.Result), Is.All.EqualTo(NoteResult.Perfect));

            JObject Attached(string kind, double beat) => E(kind, null, V("#BEAT", beat), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "middle"), R("activeHead", "head"));
        }

        [TestCase("NormalTapNote")]
        [TestCase("AnchorNote")]
        public void SelfReferencedSimLineReservesItsBeatWithoutDrawing(string endpoint)
        {
            var chart = Chart(N(endpoint, "point", 4), N("NormalTapNote", "left", 4, -3),
                N("NormalTapNote", "right", 4, 3), E("SimLine", null, R("left", "point"), R("right", "point")));
            var definition = Load(chart);
            Assert.That(definition.Score.HiddenSimLines, Is.Empty);
            Assert.That(definition.Score.NoteArray.All(n => n.PairNote == null && n.NativeSimLineTargets == null), Is.True);
            Assert.That(definition.TotalNoteCount, Is.EqualTo(endpoint == "AnchorNote" ? 2 : 3));
            AssertCompletesAutoplay(definition);

            ((JArray)chart["entities"]).Add(E("SimLine", null, R("left", "left"), R("right", "right")));
            var keys = Load(chart).Score.NoteArray.OfType<NormalNote>().OrderBy(n => n.LaneStartF).ToArray();
            Assert.That(keys[0].NativeSimLineTargets, Is.EqualTo(new[] { keys.Last() }));
            Assert.That(Load(chart, pairs: false).Score.NoteArray.All(n => n.NativeSimLineTargets == null), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RealChordUsesOnlyAuthoredEndpointsAndFallsBackOnTheNextBeat(bool mirror)
        {
            var chart = Chart(N("NormalTapNote", "a", 4, -4), N("NormalTapNote", "b", 4, -2),
                N("CriticalFlickNote", "c", 4, 3), N("NormalTapNote", "d", 8, -3),
                N("NormalTapNote", "e", 8, 3), E("SimLine", null, R("left", "a"), R("right", "b")));
            var score = NextRushChartLoader.Load(NextRushChart.Parse(chart.ToString()), _config, isMirror: mirror).Score;
            var notes = score.NoteArray;
            Assert.That(notes[0].NativeSimLineTargets, Is.EqualTo(new[] { notes[1] }));
            Assert.That(notes.Skip(1).All(n => n.NativeSimLineTargets == null), Is.True);
            Assert.That(notes.Take(3).All(n => n.PairNote == null), Is.True);
            Assert.That(notes[3].PairNote, Is.SameAs(notes[4]));
            Assert.That(Load(chart, pairs: false).Score.NoteArray.All(n => n.PairNote == null && n.NativeSimLineTargets == null), Is.True);

            using var harness = new LiveHarness();
            harness.Arrange(score);
            var host = new GameObject("authored real chord");
            try
            {
                var view = host.AddComponent<PairNoteLineView>();
                view.Setup();
                foreach (var note in notes.Take(3)) view.Add(note);
                harness.Step(1.75f, 11.75);
                view.Execute(1.75f);
                var vertices = host.GetComponent<MeshFilter>().sharedMesh.vertices;
                Assert.That(vertices.Count(v => v != Vector3.zero), Is.EqualTo(4));
                Assert.That(vertices[0].x < vertices[1].x, Is.EqualTo(!mirror));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase(2, 1, 1.4f, -.2f, .4f, .8f)]   // Left endpoint clipped at entry.
        [TestCase(-1, -2, 1.75f, 1.25f, 1.5f, 1f)] // Right endpoint clipped beyond the judge line.
        [TestCase(3, 1, 1.4f, -.8f, .4f, 0f)]     // Progress difference exceeds one.
        [TestCase(-1, 1, 1.75f, 1.25f, .75f, 0f)] // Opposite sides despite a full fade alpha.
        public void SameBeatSimLineUsesEachGroupsVisibleRange(int leftSpeed, int rightSpeed, float time,
            float leftProgress, float rightProgress, float expectedAlpha)
        {
            var left = N("NormalTapNote", "left", 4, -4);
            var right = N("NormalTapNote", "right", 4, 4);
            Add(left, R("#TIMESCALE_GROUP", "leftGroup"));
            Add(right, R("#TIMESCALE_GROUP", "rightGroup"));
            var score = Load(Chart(left, right, E("SimLine", null, R("left", "left"), R("right", "right")),
                E("#TIMESCALE_GROUP", "leftGroup", R("first", "l")),
                E("#TIMESCALE_GROUP", "rightGroup", R("first", "r")),
                E("#TIMESCALE_CHANGE", "l", V("#BEAT", 0), V("#TIMESCALE", leftSpeed)),
                E("#TIMESCALE_CHANGE", "r", V("#BEAT", 0), V("#TIMESCALE", rightSpeed)))).Score;
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Logic.Notes.BindNativeVisuals(1);
            float previousLead = LiveConfig.ViewLeadSeconds;
            var host = new GameObject("same beat independent trajectories");
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<PairNoteLineView>();
                view.Setup();
                view.Add(score.NoteArray[1]); // The right key may enter first.
                view.Execute(time);
                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                if (expectedAlpha == 0)
                {
                    Assert.That(vertices.All(v => v == Vector3.zero), Is.True);
                    return;
                }
                Assert.That(vertices.Count(v => v != Vector3.zero), Is.EqualTo(4));
                Assert.That(mesh.colors[0].a, Is.EqualTo(expectedAlpha).Within(.005f));
                for (int i = 0; i < 2; i++)
                {
                    float clipped = Mathf.Clamp(i == 0 ? leftProgress : rightProgress, 0, (float)LiveConfig.NativeKeyProgressEnd);
                    float fraction = Mathf.InverseLerp(leftProgress, rightProgress, clipped);
                    Vector2 lane = Vector2.Lerp(LiveConfig.NativeLanePosition(1.5f), LiveConfig.NativeLanePosition(9.5f), fraction);
                    Vector2 expected = LiveUtility.EarlyVec2Lerp(LiveConfig.SpawnPosition, lane, LiveConfig.GetNoteViewProgress(clipped));
                    Vector2 actual = (vertices[i] + vertices[i + 2]) * .5f;
                    Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.001f));
                }
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = previousLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CrossBeatSimLineKeepsItsEndpointOrderAndSamplesBothTrajectories(bool laterLeft)
        {
            var late = N("NormalTapNote", "late", 5.5, 4);
            var chart = Chart(N("NormalTapNote", "early", 4, -4), late,
                N("NormalTapNote", "chord", 5.5, 2),
                E("SimLine", null, R("left", laterLeft ? "late" : "early"), R("right", laterLeft ? "early" : "late")));
            if (laterLeft)
            {
                Add(late, R("#TIMESCALE_GROUP", "group"));
                var entities = (JArray)chart["entities"];
                entities.Add(E("#TIMESCALE_GROUP", "group", R("first", "hide")));
                entities.Add(E("#TIMESCALE_CHANGE", "hide", V("#BEAT", 3.82), V("#TIMESCALE", 1), V("hideNotes", 1), R("next", "show")));
                entities.Add(E("#TIMESCALE_CHANGE", "show", V("#BEAT", 3.88), V("#TIMESCALE", 1)));
            }
            var score = Load(chart).Score;
            var early = score.NoteArray[0];
            Assert.That(score.NoteArray.All(n => n.PairNote == null), Is.True);
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("cross beat line");
            float previousLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<PairNoteLineView>();
                view.Setup();
                view.Add(early);
                view.Execute(1.9f);
                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                Assert.That(vertices.Count(v => v != Vector3.zero), Is.EqualTo(4));
                Assert.That(vertices[0].y, Is.Not.EqualTo(vertices[1].y));
                Assert.That(vertices[0].x < vertices[1].x, Is.EqualTo(!laterLeft));
                Assert.That(mesh.colors[0].a, Is.EqualTo(.5f).Within(.01f));
                view.Execute(1.92f);
                Assert.That(mesh.vertices.All(v => v == Vector3.zero), Is.EqualTo(laterLeft));
                view.Add(score.NoteArray[1]);
                view.Execute(1.95f);
                Assert.That(mesh.vertices.Count(v => v != Vector3.zero), Is.EqualTo(4));
                view.Execute(2.01f);
                Assert.That(mesh.vertices.All(v => v == Vector3.zero), Is.True);
                Assert.That(Load(chart, pairs: false).Score.NoteArray.All(n => n.NativeSimLineTargets == null), Is.True);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = previousLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AutoPlaybackWaitsForALineBetweenTwoUnscoredAnchors()
        {
            var chart = Chart(N("AnchorNote", "left", 4, -3), N("AnchorNote", "right", 4, 3),
                E("SimLine", null, R("left", "left"), R("right", "right")));
            var definition = Load(chart);
            Assert.That(definition.TotalNoteCount, Is.Zero);
            Assert.That(definition.Score.HasVisualNotes, Is.True);
            Assert.That(Load(chart, pairs: false).Score.HasVisualNotes, Is.False);
            using var harness = new LiveHarness();
            harness.Arrange(definition.Score, auto: true);
            harness.Step(1.75f, 11.75);
            Assert.That(harness.Logic.result, Is.Zero);
            harness.Step(2.01f, 12.01);
            Assert.That(harness.Logic.result, Is.EqualTo(3));
        }

        [TestCase("AnchorNote", 1, true)]
        [TestCase("NormalTapNote", 1, true)]
        [TestCase("AnchorNote", 0, false)]
        public void HiddenSimLineEndpointsDrawWithoutSpawningKeyViews(string rightKind, int size, bool visible)
        {
            var chart = Chart(N("TransientHiddenTickNote", "left", 4, -3),
                N(rightKind, "right", 4, 3, size), E("SimLine", null, R("left", "left"), R("right", "right")));
            var definition = Load(chart);
            var score = definition.Score;
            Assert.That(definition.TotalNoteCount, Is.EqualTo(rightKind == "AnchorNote" ? 1 : 2));
            Assert.That(score.HiddenSimLines.Length, Is.EqualTo(1));
            Assert.That(Load(chart, pairs: false).Score.HiddenSimLines, Is.Empty);
            var host = new GameObject("line with hidden endpoints");
            float previousLead = LiveConfig.ViewLeadSeconds;
            try
            {
                using var harness = new LiveHarness();
                harness.Arrange(score);
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<PairNoteLineView>();
                view.Setup(score, 1);
                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                for (int play = 0; play < 2; play++)
                {
                    view.Execute(1.75f);
                    Assert.That(mesh.vertices.Count(v => v != Vector3.zero), Is.EqualTo(visible ? 4 : 0));
                    view.Execute(2.01f);
                    Assert.That(mesh.vertices.All(v => v == Vector3.zero), Is.True);
                    view.Clear();
                }
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = previousLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SimLineUsesTheAttachedHiddenTickThatActuallyReceivesJudgment()
        {
            var chart = Hold();
            var entities = (JArray)chart["entities"];
            entities.Single(e => (string)e["archetype"] == "TransientHiddenTickNote")["name"] = "hidden";
            entities.Add(E("SimLine", null, R("left", "middle"), R("right", "hidden")));
            var definition = Load(chart);
            var line = definition.Score.HiddenSimLines.Single();
            var bar = definition.Score.NoteArray.OfType<LongNote>().Single();
            Assert.That(line.right, Is.SameAs(bar.JudgedPoints.OfType<LongHoldCombo>().Single()));
            Assert.That(line.right.HasNativeAttachment, Is.True);
            Assert.That(line.right.LaneStartF, Is.EqualTo(4f));
            AssertCompletesAutoplay(definition);
        }

        [Test]
        public void DenseRealChordDrawsEveryAuthoredLine()
        {
            var entities = new System.Collections.Generic.List<JObject> { N("NormalTapNote", "left", 4, -4) };
            for (int index = 0; index < 51; index++)
            {
                string id = "right" + index;
                entities.Add(N("NormalTapNote", id, 4, 1 + index * 0.05));
                entities.Add(E("SimLine", null, R("left", "left"), R("right", id)));
            }
            var score = Load(Chart(entities.ToArray())).Score;
            using var harness = new LiveHarness();
            harness.Arrange(score);
            var host = new GameObject("dense real chord");
            try
            {
                var view = host.AddComponent<PairNoteLineView>();
                view.Setup();
                view.Add(score.NoteArray[0]);
                harness.Step(1.75f, 11.75);
                view.Execute(1.75f);
                Assert.That(host.GetComponent<MeshFilter>().sharedMesh.vertices.Count(v => v != Vector3.zero),
                    Is.EqualTo(51 * 4));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FakeSimultaneousLinesUseTheAuthoredLinks(bool mirror)
        {
            var chart = Chart(N("FakeNormalTapNote", "a", 4, -4),
                N("NormalTapNote", "b", 4, -2), N("FakeNormalFlickNote", "c", 4, 2),
                N("NormalTapNote", "d", 4, 4),
                E("SimLine", null, R("left", "a"), R("right", "b")),
                E("SimLine", null, R("left", "b"), R("right", "c")),
                E("SimLine", null, R("left", "c"), R("right", "d")));
            var score = NextRushChartLoader.Load(NextRushChart.Parse(chart.ToString()), _config, isMirror: mirror).Score;
            var notes = score.NoteArray;
            Assert.That(notes.All(note => note.PairNote == null), Is.True);
            Assert.That(notes.SelectMany(note => note.NativeSimLineTargets ?? Enumerable.Empty<NoteBase>()).Count(),
                Is.EqualTo(3));
            Assert.That(Load(chart, pairs: false).Score.NoteArray.All(n => n.NativeSimLineTargets == null), Is.True);

            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("authored simultaneous lines");
            float previousLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<PairNoteLineView>();
                view.Setup();
                foreach (var note in notes) view.Add(note);
                harness.Step(1.75f, 11.75);
                view.Execute(1.75f);
                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertices.Count(vertex => vertex != Vector3.zero), Is.EqualTo(12));
                view.Execute(2f);
                Assert.That(mesh.vertices.All(vertex => vertex == Vector3.zero), Is.True);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = previousLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase("NormalHeadFlickNote", false, false, NoteResult.Perfect)]
        [TestCase("NormalFlickNote", false, false, NoteResult.Perfect)]
        [TestCase("CriticalFlickNote", false, true, NoteResult.Great)]
        [TestCase("CriticalHeadFlickNote", false, true, NoteResult.Great)]
        [TestCase("NormalHeadTraceFlickNote", true, false, NoteResult.Perfect)]
        [TestCase("NormalTraceFlickNote", true, false, NoteResult.Perfect)]
        [TestCase("CriticalTraceFlickNote", true, true, NoteResult.Great)]
        [TestCase("CriticalHeadTraceFlickNote", true, true, NoteResult.Great)]
        [TestCase("NormalTailTraceFlickNote", true, false, NoteResult.Perfect)]
        [TestCase("CriticalTailTraceFlickNote", true, true, NoteResult.Great)]
        public void FlickHeadsScoreOnceAndContinueAlongTheMovingHold(string archetype, bool trace,
            bool mirror, NoteResult expected)
        {
            var json = FlickHeadHold(archetype);
            var definition = NextRushChartLoader.Load(NextRushChart.Parse(json.ToString()), _config, isMirror: mirror);
            var score = definition.Score;
            var key = score.NoteArray.Single(note => note is FlickNote or FrictionFlickNote);
            var bar = score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(definition.TotalNoteCount, Is.EqualTo(3));
            Assert.That(key.Type, Is.EqualTo(mirror ? NoteType.Critical : NoteType.Default));
            Assert.That(key.Direction, Is.EqualTo(mirror ? NoteDirection.DownRight : NoteDirection.DownLeft));
            Assert.That(key.ParentNote, Is.Null);
            if (archetype is "NormalFlickNote" or "CriticalFlickNote")
            {
                Assert.That(bar.UpdateConnectorHeadView(1.5), Is.True);
                Assert.That(bar.ConnectorHeadViewNote, Is.TypeOf<FlickNote>());
                Assert.That(bar.ConnectorHeadViewNote.IsNextRushConnectorHeadVisual, Is.True);
                Assert.That(key.IsNextRushConnectorHeadVisual, Is.False);
                Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(5f));
                Assert.That(bar.UpdateConnectorHeadView(2), Is.False);
            }
            if (archetype is "NormalTraceFlickNote" or "CriticalTraceFlickNote")
            {
                Assert.That(bar.UpdateConnectorHeadView(1.5), Is.True);
                var marker = (FrictionFlickNote)bar.ConnectorHeadViewNote;
                Assert.That(marker.IsNextRushConnectorHeadVisual, Is.True);
                Assert.That(marker.LaneStartF, Is.EqualTo(5f));
                Assert.That(bar.UpdateConnectorHeadView(2), Is.False);
            }
            using (var harness = new LiveHarness())
            {
                harness.Arrange(score);
                var start = LiveConfig.NativeLanePosition(mirror ? 8.5f : 2.5f);
                var outside = LiveConfig.NativeLanePosition(mirror ? 11f : 0f);
                var middle = LiveConfig.NativeLanePosition(5.5f);
                var end = LiveConfig.NativeLanePosition(mirror ? 2.5f : 8.5f);
                float originalFrameDelta = LiveUtility.FrameDeltaTimeOverride;
                try
                {
                    LiveUtility.FrameDeltaTimeOverride = 1f / 60f;
                    if (trace) harness.Step(0.8f, 10.8, LiveHarness.Touch(1, outside, 10.8));
                    harness.Step(0.95f, 10.95, LiveHarness.Touch(1, start, 10.95,
                        trace ? InputTouchPhase.Stationary : InputTouchPhase.Began));
                    Assert.That(key.Result, Is.EqualTo(NoteResult.None));
                    var delta = mirror ? new Vector2(-60, 60) : new Vector2(-60, -60);
                    float swipeTime = trace ? 0.99f : 1f;
                    harness.Step(swipeTime, 10d + swipeTime, new LiveTouchSample(1,
                        new Vector2(800, 600) + delta, outside, 10d + swipeTime, InputTouchPhase.Moved, delta));
                    if (trace)
                    {
                        Assert.That(key.Result, Is.EqualTo(NoteResult.None));
                        harness.Step(1f, 11);
                        harness.Step(1.02f, 11.02);
                    }
                    Assert.That(key.Result, Is.EqualTo(expected));
                    int holder = mirror ? 2 : 1;
                    harness.Step(1.42f, 11.42, LiveHarness.Touch(holder, middle, 11.42,
                        mirror ? InputTouchPhase.Began : InputTouchPhase.Moved));
                    harness.Step(1.5f, 11.5, LiveHarness.Touch(holder, middle, 11.5, InputTouchPhase.Stationary));
                    Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
                    harness.Step(2f, 12, LiveHarness.Touch(holder, end, 12, InputTouchPhase.Ended));
                    Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
                }
                finally { LiveUtility.FrameDeltaTimeOverride = originalFrameDelta; }
            }
            AssertCompletesAutoplay(Load(json));
        }

        [TestCase("NormalHeadFlickNote")]
        [TestCase("NormalFlickNote")]
        public void FlickHeadRejectsAnUnregisteredFingerAndAcceptsALateFrameSwipeAfterRetry(string archetype)
        {
            var score = Load(FlickHeadHold(archetype)).Score;
            var head = score.NoteArray.OfType<FlickNote>().Single();
            var bar = score.NoteArray.OfType<FrictionHideLongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score);
            var lane = LiveConfig.NativeLanePosition(2.5f);
            var delta = new Vector2(-60, -60);
            float originalFrameDelta = LiveUtility.FrameDeltaTimeOverride;
            try
            {
                LiveUtility.FrameDeltaTimeOverride = 1f / 60f;
                harness.Step(0.8f, 10.8, LiveHarness.Touch(1, lane, 10.8));
                harness.Step(1.02f, 11.02, Swipe(1, 11.02));
                Assert.That(head.Result, Is.EqualTo(NoteResult.None));
                Assert.That(bar.WasTouched, Is.False);
                harness.Step(1.16f, 11.16, LiveHarness.Touch(1, lane, 11.16, InputTouchPhase.Stationary));
                Assert.That(head.Result, Is.EqualTo(NoteResult.Miss));
                Assert.That(bar.WasTouched, Is.True);

                harness.Logic.Session.Reset(new LivePresentationBridge());
                harness.Step(0.95f, 20.95, LiveHarness.Touch(2, lane, 20.95));
                harness.Step(1.02f, 21.02, Swipe(1, 21.02));
                Assert.That(head.Result, Is.EqualTo(NoteResult.None));
                harness.Step(1.15f, 21.15, Swipe(2, 21.13));
                Assert.That(head.Result, Is.EqualTo(NoteResult.Good));
            }
            finally { LiveUtility.FrameDeltaTimeOverride = originalFrameDelta; }

            LiveTouchSample Swipe(int finger, double time) => new LiveTouchSample(finger,
                new Vector2(800, 600) + delta, lane, time, InputTouchPhase.Moved, delta);
        }

        [TestCase(false, "NormalHeadFlickNote")]
        [TestCase(true, "NormalHeadFlickNote")]
        [TestCase(false, "NormalFlickNote")]
        [TestCase(true, "NormalFlickNote")]
        public void VeryShortFlickHeadHoldRequiresCaptureBeforeAcceptingAnEarlyRelease(bool armHead, string archetype)
        {
            var head = N(archetype, "head", 2);
            var tail = N("NormalTailReleaseNote", "tail", 2.1);
            Add(head, R("next", "tail"));
            Add(tail, R("activeHead", "head"));
            var score = Load(Chart(head, tail, Segment("head", "tail", "head", "tail", "head", "tail"))).Score;
            var bar = score.NoteArray.OfType<FrictionHideLongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score);
            var lane = LiveConfig.NativeLanePosition(5.5f);
            float pressTime = armHead ? 0.95f : 0.82f;
            harness.Step(pressTime, 10d + pressTime, LiveHarness.Touch(1, lane, 10d + pressTime));
            harness.Step(1.05f, 11.05, LiveHarness.Touch(1, lane, 11.05, InputTouchPhase.Ended));
            Assert.That(bar.TailNote.Result, Is.EqualTo(armHead ? NoteResult.Perfect : NoteResult.None));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TraceFlickOwnerAllowsShortReleaseAfterCapturingAnAvailablePress(bool freshPress, bool overlappingTap)
        {
            var head = N("NormalTraceFlickNote", "head", 2, -3);
            Add(head, R("next", "tail"));
            var json = Chart(head, N("NormalTailReleaseNote", "tail", 2.1, 3),
                Segment("head", "tail", "head", "tail", "head", "tail"));
            if (overlappingTap) ((JArray)json["entities"]).Add(N("NormalTapNote", null, 2, -3));
            var chart = Load(json);
            var key = chart.Score.NoteArray.OfType<FrictionFlickNote>().Single();
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.NativeLanePosition(2.5f);
            var end = LiveConfig.NativeLanePosition(8.5f);
            harness.Step(.8f, 10.8, LiveHarness.Touch(1, lane, 10.8), LiveHarness.Touch(2, end, 10.8));
            harness.Step(1f, 11, LiveHarness.Touch(freshPress ? 3 : 1, lane, 11,
                freshPress ? InputTouchPhase.Began : InputTouchPhase.Stationary));
            Assert.That(key.Result, Is.EqualTo(NoteResult.None));
            if (overlappingTap) Assert.That(chart.Score.NoteArray.OfType<NormalNote>().Single().Result, Is.EqualTo(NoteResult.Perfect));
            harness.Step(1.05f, 11.05, LiveHarness.Touch(2, end, 11.05, InputTouchPhase.Ended));
            Assert.That(bar.TailNote.Result, Is.EqualTo(freshPress && !overlappingTap ? NoteResult.Perfect : NoteResult.None));
        }

        [Test]
        public void TraceFlickOwnerCanMissAndStillAllowTheFollowingTickAndRelease()
        {
            var chart = Load(FlickHeadHold("NormalTraceFlickNote"));
            var key = chart.Score.NoteArray.OfType<FrictionFlickNote>().Single();
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Step(.8f, 10.8, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[2], 10.8));
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[2], 11, InputTouchPhase.Stationary));
            harness.Step(1.15f, 11.15);
            harness.Step(1.16f, 11.16);
            Assert.That(key.Result, Is.EqualTo(NoteResult.Miss));
            harness.Step(1.45f, 11.45, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[5], 11.45));
            harness.Step(1.5f, 11.5, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[5], 11.5, InputTouchPhase.Stationary));
            Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
            harness.Step(2f, 12, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[8], 12, InputTouchPhase.Ended));
            Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase("NormalTailFlickNote", false, true)]
        [TestCase("CriticalTailFlickNote", true, true)]
        [TestCase("NormalTailFlickNote", false, false)]
        [TestCase("NormalTailTraceFlickNote", false, true)]
        [TestCase("CriticalTailTraceFlickNote", true, false)]
        public void SwipeTailOwnsTheNextSection(string archetype, bool freshPress, bool swipe)
        {
            var first = N("NormalHeadTapNote", "first", 1, -3);
            var joint = N(archetype, "joint", 2, -3);
            Add(first, R("next", "joint"));
            Add(joint, R("prev", "first"), R("activeHead", "first"), R("next", "tail"), V("style", 4));
            var json = Chart(first, joint, N("NormalTailReleaseNote", "tail", 4, 3),
                Segment("first", "joint", "first", "joint", "first", "joint"),
                Segment("joint", "tail", "joint", "tail", "joint", "tail"),
                E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                    R("attachHead", "joint"), R("attachTail", "tail"), R("activeHead", "joint")));
            var chart = Load(json);
            var key = chart.Score.NoteArray.OfType<FrictionFlickNote>().Single();
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(4));
            bool trace = archetype.EndsWith("TailTraceFlickNote", StringComparison.Ordinal);
            Assert.That(key.Category, Is.EqualTo(trace ? NoteCategory.FrictionFlick : NoteCategory.Flick));
            Assert.That(bar.UpdateConnectorHeadView(1.5), Is.True);
            Assert.That(bar.ConnectorHeadViewNote, trace ? Is.TypeOf<FrictionFlickNote>() : Is.TypeOf<FlickNote>());
            Assert.That(bar.ConnectorHeadViewNote.IsNextRushConnectorHeadVisual, Is.True);
            Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(5f));
            Assert.That(bar.ConnectorHeadViewNote.NativeStyle, Is.EqualTo(4));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var start = LiveConfig.NativeLanePosition(2.5f);
                harness.Step(0.5f, 10.5, LiveHarness.Touch(1, start, 10.5));
                var held = LiveHarness.Touch(1, start, 11, InputTouchPhase.Stationary);
                if (freshPress) harness.Step(1f, 11, held, LiveHarness.Touch(2, start, 11));
                else harness.Step(1f, 11, held);
                Assert.That(key.HasCapturedPress, Is.EqualTo(freshPress && !trace));
                Assert.That(bar.HasFinger, Is.EqualTo(freshPress || trace));
                if (swipe)
                {
                    harness.Step(1.02f, 11.02, new LiveTouchSample(1, new Vector2(800, 660), start,
                        11.02, InputTouchPhase.Moved, new Vector2(0, 60)));
                    Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
                }
                var middle = LiveConfig.NativeLanePosition(5.5f);
                harness.Step(1.43f, 11.43, LiveHarness.Touch(1, middle, 11.43, InputTouchPhase.Moved));
                harness.Step(1.5f, 11.5, LiveHarness.Touch(1, middle, 11.5, InputTouchPhase.Stationary));
                Assert.That(key.Result, Is.EqualTo(swipe ? NoteResult.Perfect : NoteResult.Miss));
                Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
                harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(8.5f), 12, InputTouchPhase.Ended));
                Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [TestCase("CriticalTailFlickNote", true)]
        [TestCase("CriticalTailFlickNote", false)]
        [TestCase("NormalTailTraceNote", true)]
        [TestCase("NormalTailTraceNote", false)]
        [TestCase("CriticalTailTraceFlickNote", true, false)]
        [TestCase("CriticalTailTraceFlickNote", true, true)]
        public void ForkFromScoredTailWaitsForOwnerBeforeRelease(string archetype, bool touchOwner, bool swipe = false)
        {
            var head = N(archetype, "head", 2, -3);
            Add(head, R("next", "tail"));
            var tail = N("NormalTailReleaseNote", "tail", 2.12, 3);
            Add(tail, R("activeHead", "head"));
            var chart = Load(Chart(head, tail, N("AnchorNote", "arm", 2.1),
                Segment("head", "tail", "head", "tail", "head", "tail"),
                Segment("head", "arm", "head", "arm", "head", "tail")));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var right = LiveConfig.JudgmentPositions[8];
            harness.Step(.8f, 10.8, LiveHarness.Touch(1, right, 10.8));
            if (touchOwner) harness.Step(1f, 11, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[2], 11));
            if (swipe) harness.Step(1.02f, 11.02, new LiveTouchSample(2, new Vector2(800, 660),
                LiveConfig.JudgmentPositions[2], 11.02, InputTouchPhase.Moved, new Vector2(0, 60)));
            harness.Step(1.06f, 11.06, LiveHarness.Touch(1, right, 11.06, InputTouchPhase.Ended));
            Assert.That(chart.Score.NoteArray.OfType<NormalNote>().Single().Result,
                Is.EqualTo((archetype == "CriticalTailTraceFlickNote" ? swipe : touchOwner) ? NoteResult.Perfect : NoteResult.None));
        }

        private static JObject FlickHeadHold(string archetype)
        {
            var head = N(archetype, "head", 2, -3);
            var tail = N("NormalTailReleaseNote", "tail", 4, 3);
            Add(head, V("direction", (int)NoteDirection.DownLeft), R("next", "tail"));
            Add(tail, R("activeHead", "head"));
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            return Chart(head, tail, tick, Segment("head", "tail", "head", "tail", "head", "tail"));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void DamageContactScoresOnceAndDoesNotSpendThePress(bool touchHazard, bool mirror)
        {
            var chart = Chart(N("NormalTapNote", "before", 3),
                N("NormalTapNote", "tap", 4, -2), N("DamageNote", "damage", 4, -2));
            var definition = NextRushChartLoader.Load(NextRushChart.Parse(chart.ToString()), _config, isMirror: mirror);
            var score = definition.Score;
            var hazard = score.NoteArray.OfType<NextRushDamageNote>().Single();
            var board = new ScoreLogic(_config);
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.BindViews();
            harness.Logic.Session.ScoreLogic = board;
            board.Setup(null, score);
            int initialLife = board.Score.life;
            Assert.That(definition.TotalNoteCount, Is.EqualTo(3));
            Assert.That(score.NoteArray.Single(n => n is NormalNote && n.MusicScoreInfo.time == 2f).PairNote, Is.Null);

            var target = LiveConfig.NativeLanePosition(mirror ? 7.5f : 3.5f);
            harness.Step(1.5f, 11.5, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[5], 11.5));
            harness.Step(1.98f, 11.98, LiveHarness.Touch(2, target, 11.98));
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.None));
            var contact = touchHazard ? target : LiveConfig.NativeLanePosition(mirror ? 8.6f : 2.4f);
            harness.Step(2f, 12, LiveHarness.Touch(2, contact, 12, InputTouchPhase.Stationary));
            Assert.That(hazard.Result, Is.EqualTo(touchHazard ? NoteResult.Miss : NoteResult.Perfect));
            Assert.That(board.Score.perfectCount, Is.EqualTo(touchHazard ? 2 : 3));
            Assert.That(board.Score.missCount, Is.EqualTo(touchHazard ? 1 : 0));
            Assert.That(board.Score.life, Is.EqualTo(initialLife - (touchHazard ? 40 : 0)));
            Assert.That(board.Score.combo, Is.EqualTo(touchHazard ? 0 : 3));
            harness.Step(2.05f, 12.05, LiveHarness.Touch(2, target, 12.05, InputTouchPhase.Stationary));
            Assert.That(board.Score.life, Is.EqualTo(initialLife - (touchHazard ? 40 : 0)));
            Assert.That(board.Score.totalScore, Is.EqualTo(touchHazard ? 666667 : 1000000));
        }

        [TestCase(true, InputTouchPhase.Moved, 12.04, NoteResult.Miss)]
        [TestCase(false, InputTouchPhase.Moved, 12.04, NoteResult.Perfect)]
        [TestCase(true, InputTouchPhase.Ended, 12.04, NoteResult.Miss)]
        [TestCase(true, InputTouchPhase.Ended, 11.99, NoteResult.Perfect)]
        public void VisibleDamageUsesTheContactAtItsTargetWhenTheFrameIsLate(bool startedOnHazard,
            InputTouchPhase phase, double eventTime, NoteResult expected)
        {
            var score = Load(Chart(N("DamageNote", "hazard", 4))).Score;
            var hazard = score.NoteArray.OfType<NextRushDamageNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score);
            var hazardLane = LiveConfig.NativeLanePosition(5.5f);
            var safeLane = LiveConfig.NativeLanePosition(1.5f);

            harness.Step(1.95f, 11.95, LiveHarness.Touch(1, startedOnHazard ? hazardLane : safeLane, 11.95));
            harness.Step(2.08f, 12.08,
                LiveHarness.Touch(1, startedOnHazard ? safeLane : hazardLane, eventTime, phase));

            Assert.That(hazard.Result, Is.EqualTo(expected));
        }

        [TestCase(11.97, NoteResult.Miss)]
        [TestCase(12.04, NoteResult.Perfect)]
        public void VisibleDamageDoesNotUseAPressStartedAfterItsTarget(double pressTime, NoteResult expected)
        {
            var score = Load(Chart(N("DamageNote", "hazard", 4))).Score;
            var hazard = score.NoteArray.OfType<NextRushDamageNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score);

            harness.Step(2.08f, 12.08, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(5.5f), pressTime));

            Assert.That(hazard.Result, Is.EqualTo(expected));
        }

        [Test]
        public void DamageAtTheSameTimeAsTapDoesNotPreventItsJudgmentAndRetryRestoresTheHazard()
        {
            var definition = Load(Chart(N("DamageNote", "hazard", 4), N("NormalTapNote", "tap", 4)));
            var score = definition.Score;
            var hazard = score.NoteArray.OfType<NextRushDamageNote>().Single();
            var tap = score.NoteArray.OfType<NormalNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(2.016f, 12.016, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[5], 12));
            Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.Miss));

            harness.Logic.Session.Reset(new LivePresentationBridge());
            harness.Step(2f, 22);
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase("DamageNote")]
        [TestCase("TransientHiddenDamageTickNote")]
        public void AutoplayAvoidsDamageAndCountsItsScorePoint(string archetype)
        {
            var score = Load(Chart(N(archetype, "hazard", 4))).Score;
            var board = new ScoreLogic(_config);
            board.Setup(null, score);
            using var harness = new LiveHarness();
            harness.Arrange(score, auto: true);
            harness.BindViews();
            harness.Logic.Session.ScoreLogic = board;
            int initialLife = board.Score.life;
            harness.Step(2.03f, 12.03);
            harness.Logic.OnAutoInput();
            Assert.That(board.Score.autoCount, Is.EqualTo(1));
            Assert.That(board.Score.missCount, Is.Zero);
            Assert.That(board.Score.life, Is.EqualTo(initialLife));
            Assert.That(board.Score.totalScore, Is.EqualTo(1000000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HiddenDamageChecksTheMovingPathBeforeItsAttachmentJoint(bool mirror)
        {
            var head = N("AnchorNote", "head", 4, -3, 0.5);
            var joint = N("AnchorNote", "joint", 4.25, 0, 0.5);
            var tail = N("AnchorNote", "tail", 5, 3, 0.5);
            Add(head, V("segmentKind", 3), R("next", "joint"), R("activeHead", "head"));
            Add(joint, R("next", "tail"), R("activeHead", "head"));
            Add(tail, V("segmentKind", 0), R("activeHead", "head"));
            var tick = E("TransientHiddenDamageTickNote", "tick", V("#BEAT", 4.5), V("isAttached", 1),
                V("segmentKind", 0), R("attachHead", "joint"), R("attachTail", "tail"), R("activeHead", "head"));
            var lastTick = E("TransientHiddenDamageTickNote", "lastTick", V("#BEAT", 5), V("isAttached", 1),
                R("attachHead", "joint"), R("attachTail", "tail"), R("activeHead", "head"));
            var chart = Chart(head, joint, tail, tick, lastTick, Edge("head", "joint"), Edge("joint", "tail"));
            var definition = NextRushChartLoader.Load(NextRushChart.Parse(chart.ToString()), _config, isMirror: mirror);
            var score = definition.Score;
            var bar = score.NoteArray.OfType<GuideNote>().Single();
            var hazards = score.NoteArray.OfType<NextRushDamageNote>().ToArray();
            Assert.That(definition.TotalNoteCount, Is.EqualTo(2));
            Assert.That(bar.NativeConnector.IsDamage, Is.True);
            Assert.That(bar.NativeConnector.StyleColor(), Is.EqualTo((Color)new Color32(178, 99, 255, 255)));
            Assert.That(bar.LineNoteList.Count, Is.EqualTo(3));
            Assert.That(hazards.All(hazard => !hazard.IsVisible), Is.True);

            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.BindViews();
            var board = new ScoreLogic(_config);
            harness.Logic.Session.ScoreLogic = board;
            board.Setup(null, score);
            int initialLife = board.Score.life;
            var contact = LiveConfig.NativeLanePosition(mirror ? 7f : 4f);
            harness.Step(1.99f, 11.99, LiveHarness.Touch(1, contact, 11.99));
            Assert.That(hazards[0].Result, Is.EqualTo(NoteResult.None));
            Assert.That(bar.DamageContact, Is.False);
            // An unchanged device sample still describes a finger that is down on this frame.
            harness.Step(2.0625f, 12.0625, LiveHarness.Touch(1, contact, 11.99, InputTouchPhase.Stationary));
            Assert.That(hazards[0].Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(bar.DamageContact, Is.True);
            Assert.That(board.Score.life, Is.EqualTo(initialLife - 40));
            harness.Step(2.25f, 12.25);
            Assert.That(bar.DamageContact, Is.False);
            harness.Step(2.50f, 12.50);
            Assert.That(hazards[1].Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(board.Score.life, Is.EqualTo(initialLife - 40));
            Assert.That(board.Score.missCount, Is.EqualTo(1));

            harness.Logic.Session.Reset(new LivePresentationBridge());
            harness.Step(2.26f, 22.26);
            Assert.That(hazards[0].Result, Is.EqualTo(NoteResult.Perfect));

            JObject Edge(string start, string end) => E("Connector", null, R("head", start), R("tail", end),
                R("segmentHead", "head"), R("segmentTail", "tail"), R("activeHead", "head"), R("activeTail", "tail"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HiddenDamageUsesAuthoredPredecessorAtMerge(bool reverseOrder)
        {
            var left = N("AnchorNote", "left", 4, -4, 0.5);
            var right = N("AnchorNote", "right", 4, 4, 0.5);
            var joint = N("AnchorNote", "joint", 4.25, 0, 0.5);
            var tail = N("AnchorNote", "tail", 5, 0, 0.5);
            Add(left, V("segmentKind", 3), R("next", "joint"));
            Add(right, V("segmentKind", 3), R("next", "joint"));
            Add(joint, V("segmentKind", 3), R("prev", "right"), R("next", "tail"));
            var tick = E("TransientHiddenDamageTickNote", null, V("#BEAT", 4.5), V("isAttached", 1),
                R("attachHead", "joint"), R("attachTail", "tail"), R("activeHead", "right"));
            var entities = new[] { left, right, joint, tail, tick,
                Segment("left", "joint", "left", "joint", "left", "tail"),
                Segment("right", "joint", "right", "joint", "right", "tail"),
                Segment("joint", "tail", "joint", "tail", "right", "tail") };
            var score = Load(Chart(reverseOrder ? entities.Reverse().ToArray() : entities)).Score;
            var hazard = score.NoteArray.OfType<NextRushDamageNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(2.0625f, 12.0625, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(3.5f), 12.0625));
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.None));
            harness.Step(2.063f, 12.063, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(7.5f), 12.063));
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void HiddenDamageClipsItsWindowToTheActiveHeadAndKeepsLateFrameSamples()
        {
            var tick = N("TransientHiddenDamageTickNote", "tick", 1.5);
            var head = N("AnchorNote", "head", 1.25);
            Add(tick, R("activeHead", "head"));
            var score = Load(Chart(head, tick, E("#BPM_CHANGE", null, V("#BEAT", 1), V("#BPM", 10)))).Score;
            var hazard = score.NoteArray.OfType<NextRushDamageNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score, displayTimeOffset: 0.1f);
            var contact = LiveConfig.NativeLanePosition(5.5f);
            harness.Step(1.9f, 11.9, LiveHarness.Touch(1, contact, 11.9));
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.None));
            // The song clock is past the tick. A press timestamped before it still counts.
            harness.Step(3.52f, 13.52, LiveHarness.Touch(2, contact, 13.48));
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.Miss));

            harness.Logic.Session.Reset(new LivePresentationBridge());
            harness.Step(2.05f, 22.05, LiveHarness.Touch(3, contact, 22.05));
            Assert.That(hazard.Result, Is.EqualTo(NoteResult.Miss));
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void TapHeadAndDamageTickOnOneConnectorGradeIndependently(bool touchDamage, bool auto, bool pressHead)
        {
            var head = N("NormalHeadTapNote", "head", 4, -3);
            Add(head, V("segmentKind", pressHead ? 34 : 3), V("style", 7), R("next", "tail"), R("activeHead", "head"));
            var tail = N("AnchorNote", "tail", 6, 3);
            var tick = E("TransientHiddenDamageTickNote", "tick", V("#BEAT", 5), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            var definition = Load(Chart(head, tail, tick, Segment("head", "tail", "head", "tail", "head", "tail")));
            var score = definition.Score;
            var bar = score.NoteArray.OfType<GuideNote>().Single();
            Assert.That(definition.TotalNoteCount, Is.EqualTo(2));
            Assert.That(bar.TracksDamageContact, Is.True);
            Assert.That(bar.UpdateConnectorHeadView(1.99), Is.False);
            Assert.That(bar.UpdateConnectorHeadView(2.5), Is.True);
            Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(5f).Within(1e-5f));
            Assert.That(bar.ConnectorHeadViewNote.NativeStyle, Is.EqualTo(pressHead ? 4 : 7));
            Assert.That(bar.UpdateConnectorHeadView(3), Is.False);

            using var harness = new LiveHarness();
            harness.Arrange(score, auto: auto);
            harness.BindViews();
            var board = new ScoreLogic(_config);
            board.Setup(null, score);
            harness.Logic.Session.ScoreLogic = board;
            int life = board.Score.life;
            if (auto)
            {
                harness.Step(2f, 12);
                harness.Logic.OnAutoInput();
            }
            else if (pressHead)
            {
                harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(2.5f), 12));
                Assert.That(score.NoteArray.OfType<NormalNote>().Single().Result, Is.EqualTo(NoteResult.Perfect));
            }
            if (touchDamage)
                harness.Step(2.5f, 12.5, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(5.5f), 12.5));
            else
                harness.Step(2.5f, 12.5);
            if (auto) harness.Logic.OnAutoInput();
            if (pressHead || auto) Assert.That(board.Score.life, Is.EqualTo(life - (touchDamage ? 40 : 0)));
            Assert.That(board.Score.missCount, Is.EqualTo(touchDamage || !auto && !pressHead ? 1 : 0));
            Assert.That(board.Score.autoCount + board.Score.perfectCount + board.Score.missCount, Is.EqualTo(2));
        }

        [TestCase("NormalTapNote", 3)]
        [TestCase("CriticalFlickNote", 34)]
        [TestCase("NormalTraceNote", 34)]
        [TestCase("CriticalTraceFlickNote", 3)]
        [TestCase("NormalReleaseNote", 3)]
        [TestCase("CriticalReleaseNote", 34)]
        public void OrdinaryKeySuppliesMovingHeadForDamageConnector(string archetype, int segmentKind)
        {
            var head = N(archetype, "head", 4, -3);
            Add(head, V("segmentKind", segmentKind), V("style", 7), R("next", "tail"), R("activeHead", "head"));
            var definition = Load(Chart(head, N("AnchorNote", "tail", 6, 3),
                Segment("head", "tail", "head", "tail", "head", "tail")));
            var bar = definition.Score.NoteArray.OfType<GuideNote>().Single();
            var key = definition.Score.NoteArray.Single(note => note != bar && !note.IsDecoration);
            var marker = bar.ConnectorHeadViewNote;
            Assert.That(definition.TotalNoteCount, Is.EqualTo(1));
            Assert.That(marker, Is.TypeOf(key.GetType()));
            Assert.That(marker.IsDecoration, Is.True);
            Assert.That(bar.UpdateConnectorHeadView(2.5), Is.True);
            Assert.That(marker.LaneStartF, Is.EqualTo(5f).Within(1e-5f));
            Assert.That(marker.NativeStyle, Is.EqualTo(segmentKind == 3 ? 7 : 4));
            Assert.That(marker.Type, Is.EqualTo(key.Type));
            Assert.That(marker.IsNextRushConnectorHeadVisual, Is.True);
            Assert.That(bar.UpdateConnectorHeadView(3), Is.False);
        }

        [TestCase(false, 1, false)]
        [TestCase(true, 2, false)]
        [TestCase(true, 1, true)]
        [TestCase(false, 2, true)]
        public void DamageHeadGradesOnceWhileThePlayableConnectorContinues(bool touchHazard, int segmentKind, bool hidden)
        {
            var head = N(hidden ? "TransientHiddenDamageTickNote" : "DamageNote", "head", 2, -3);
            Add(head, R("next", "tail"), V("segmentKind", segmentKind), V("style", 4));
            var json = Chart(N("NormalTailReleaseNote", "tail", 4, 3), head,
                Segment("head", "tail", "head", "tail", "head", "tail"),
                E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                    R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head")));
            var chart = Load(json);
            var hazard = chart.Score.NoteArray.OfType<NextRushDamageNote>().Single();
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            Assert.That(hazard.IsVisible, Is.EqualTo(!hidden));
            if (hidden)
            {
                Assert.That(hazard.InputStart, Is.EqualTo(.75f));
                Assert.That(bar.ConnectorHeadViewNote, Is.Null);
            }
            else
            {
                Assert.That(bar.UpdateConnectorHeadView(1.5), Is.True);
                Assert.That(bar.ConnectorHeadViewNote, Is.TypeOf<NextRushDamageNote>());
                Assert.That(bar.ConnectorHeadViewNote.IsDecoration, Is.True);
                Assert.That(bar.ConnectorHeadViewNote.Type, Is.EqualTo(hazard.Type));
                Assert.That(bar.ConnectorHeadViewNote.NativeStyle, Is.EqualTo(4));
                Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(5f));
            }
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                float touchTime = hidden ? .85f : 1f;
                if (touchHazard) harness.Step(touchTime, 10d + touchTime,
                    LiveHarness.Touch(1, LiveConfig.NativeLanePosition(2.5f), 10d + touchTime));
                else harness.Step(1f, 11);
                var middle = LiveConfig.NativeLanePosition(5.5f);
                harness.Step(1.43f, 11.43, LiveHarness.Touch(2, middle, 11.43));
                harness.Step(1.5f, 11.5, LiveHarness.Touch(2, middle, 11.5, InputTouchPhase.Stationary));
                Assert.That(hazard.Result, Is.EqualTo(touchHazard ? NoteResult.Miss : NoteResult.Perfect));
                Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
                harness.Step(2f, 12, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(8.5f), 12, InputTouchPhase.Ended));
                Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void DamageOwnedLoopStopsAtTheTailWhileTheFingerIsStillDown()
        {
            var head = N("DamageNote", "head", 2);
            Add(head, R("next", "tail"), V("segmentKind", 2));
            var chart = Load(Chart(head, N("NormalTailReleaseNote", "tail", 4),
                Segment("head", "tail", "head", "tail", "head", "tail")));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var events = new List<string>();
            harness.Logic.ConnectorAudio.Play = cue => { events.Add(cue); return 42; };
            harness.Logic.ConnectorAudio.Stop = id => events.Add("stop" + id);
            var lane = LiveConfig.NativeLanePosition(5.5f);
            harness.Step(1f, 11);
            harness.Step(1.3f, 11.3, LiveHarness.Touch(1, lane, 11.3));
            harness.Step(1.5f, 11.5, LiveHarness.Touch(1, lane, 11.5, InputTouchPhase.Stationary));
            Assert.That(events, Is.EqualTo(new[] { LiveSoundDefine.SE_LIVE_LONG_CRITICAL }));
            harness.Step(2f, 12, LiveHarness.Touch(1, lane, 12, InputTouchPhase.Stationary));
            Assert.That(events, Is.EqualTo(new[] { LiveSoundDefine.SE_LIVE_LONG_CRITICAL, "stop42" }));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ShortReleaseAfterDamageWaitsForTheHazard(bool fork, bool hidden)
        {
            var head = N(hidden ? "TransientHiddenDamageTickNote" : "DamageNote", "head", 2, -3);
            var tail = N("NormalTailReleaseNote", "tail", 2.12, 3);
            Add(head, R("next", "tail"));
            Add(tail, R("activeHead", "head"));
            var json = Chart(head, tail, Segment("head", "tail", "head", "tail", "head", "tail"));
            if (fork)
            {
                ((JArray)json["entities"]).Add(N("AnchorNote", "arm", 2.1));
                ((JArray)json["entities"]).Add(Segment("head", "arm", "head", "arm", "head", "tail"));
            }
            var chart = Load(json);
            var end = fork ? chart.Score.NoteArray.OfType<NormalNote>().Single()
                : chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single().TailNote;
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.JudgmentPositions[8];
            harness.Step(.8f, 10.8, LiveHarness.Touch(1, lane, 10.8), LiveHarness.Touch(2, lane, 10.8));
            harness.Step(.99f, 10.99, LiveHarness.Touch(1, lane, 10.99, InputTouchPhase.Ended));
            Assert.That(end.Result, Is.EqualTo(NoteResult.None));
            harness.Step(1f, 11);
            harness.Step(1.06f, 11.06, LiveHarness.Touch(2, lane, 11.06, InputTouchPhase.Ended));
            Assert.That(end.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase(1, 3)]
        [TestCase(3, 2)]
        [TestCase(1, 51)]
        [TestCase(52, 2)]
        public void ActiveIntervalCanChangeConnectorKindAtAnInteriorAnchor(int firstKind, int secondKind)
        {
            var head = N("NormalTapNote", "head", 2, -3);
            var joint = N("AnchorNote", "joint", 3);
            var tail = N("NormalTailTapNote", "tail", 4, 3);
            Add(head, R("next", "joint"), V("segmentKind", firstKind));
            Add(joint, R("next", "tail"), R("activeHead", "head"), V("segmentKind", secondKind));
            Add(tail, R("activeHead", "head"));
            var json = Chart(tail, joint, head,
                Segment("joint", "tail", "joint", "tail", "head", "tail"),
                Segment("head", "joint", "head", "joint", "head", "tail"));
            var chart = Load(json);
            var bars = chart.Score.NoteArray.OfType<LongNote>().OrderBy(bar => bar.MusicScoreInfo.time).ToArray();
            Assert.That(bars.Select(bar => bar.NativeConnector.Kind), Is.EqualTo(new[] { firstKind, secondKind }));
            Assert.That(bars.OfType<FrictionHideLongNote>().Count(), Is.EqualTo(1));
            Assert.That(bars.OfType<GuideNote>().Count(), Is.EqualTo(1));
            Assert.That(bars[0].ChildNote.MusicScoreInfo.time, Is.EqualTo(bars[1].MusicScoreInfo.time));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            var keys = chart.Score.NoteArray.OfType<NormalNote>().OrderBy(key => key.MusicScoreInfo.time).ToArray();
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(2.5f), 11));
                harness.Step(1.5f, 11.5);
                harness.Step(2f, 12, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(8.5f), 12));
                Assert.That(keys.Select(key => key.Result), Is.EqualTo(new[] { NoteResult.Perfect, NoteResult.Perfect }));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void DamageConnectorBrightensOnContactWithoutGradingTheBody()
        {
            var head = N("DamageNote", "head", 4);
            var tail = N("DamageNote", "tail", 6);
            Add(head, V("segmentKind", 3), R("next", "tail"), R("activeHead", "head"));
            Add(tail, V("segmentKind", 0), R("activeHead", "head"));
            var definition = Load(Chart(head, tail, Segment("head", "tail", "head", "tail", "head", "tail")));
            var score = definition.Score;
            var bar = score.NoteArray.OfType<GuideNote>().Single();
            var hazards = score.NoteArray.OfType<NextRushDamageNote>().ToArray();
            Assert.That(definition.TotalNoteCount, Is.EqualTo(2));
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("damage connector");
            float previousLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<NoteLineView>();
                view.Setup(_config, "longNoteLine.png", 1);
                view.Add(bar);
                harness.Step(2.1f, 12.1);
                view.Excute(2.1f);
                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                var waiting = mesh.colors[0];
                harness.Step(2.2f, 12.2, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(5.5f), 12.2));
                view.Excute(2.2f);
                var touched = mesh.colors[0];
                Assert.That(touched.r, Is.GreaterThan(waiting.r));
                Assert.That(touched.b, Is.GreaterThan(touched.r));
                Assert.That(hazards[0].Result, Is.EqualTo(NoteResult.Perfect));
                Assert.That(hazards[1].Result, Is.EqualTo(NoteResult.None));
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = previousLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase("custom01")]
        [TestCase("custom02")]
        public void DamageHasAPlumBodyWithoutTraceDiamondAndRestoresTraceArtWhenReused(string skin)
        {
            var view = UnityEngine.Object.Instantiate(Resources.Load<FrictionNoteView>("live/note/" + skin + "/FrictionNote"));
            try
            {
                var score = Load(Chart(N("DamageNote", "damage", 2), N("NormalTraceNote", "trace", 4))).Score;
                var renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(2));
                var frictionSprites = renderers.Select(r => r.sprite).ToArray();
                view.Spawn(score.NoteArray[0], 0);
                var body = renderers.Single(r => r.enabled);
                Assert.That(body.sprite.texture.name, Is.EqualTo("notes_damage"));
                Assert.That(body.sprite.border, Is.EqualTo(frictionSprites[Array.IndexOf(renderers, body)].border));
                view.Unspawn();
                view.Spawn(score.NoteArray[1], 0);
                Assert.That(renderers.Select(r => r.sprite), Is.EqualTo(frictionSprites));
                Assert.That(renderers.All(r => r.enabled), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
        }

        [TestCase(12.0001)]
        [TestCase(15)]
        [TestCase(1e100)]
        public void AboveRangeForcedSpeedUsesTheFastestPreemptInItsOwnGroup(double speed)
        {
            var forced = N("NormalTapNote", "forced", 4, -3);
            Add(forced, R("#TIMESCALE_GROUP", "fast"));
            var chart = Load(Chart(forced, N("NormalTapNote", "ordinary", 4, 3),
                E("#TIMESCALE_GROUP", "fast", V("forceNoteSpeed", speed))));
            var notes = chart.Score.NoteArray.OrderBy(note => note.LaneStartF).ToArray();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            Assert.That(notes[0].NativePreempt, Is.EqualTo(.35).Within(1e-9));
            Assert.That(notes[1].NativePreempt, Is.EqualTo(1).Within(1e-9));
            Assert.That(notes[0].NativeViewProgress(1.825, out _), Is.EqualTo(.5).Within(1e-8));
            Assert.That(notes[1].NativeViewProgress(1.825, out _), Is.EqualTo(.825).Within(1e-8));
            harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(2.5f), 12),
                LiveHarness.Touch(2, LiveConfig.NativeLanePosition(8.5f), 12));
            Assert.That(notes.Select(note => note.Result), Is.All.EqualTo(NoteResult.Perfect));
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
            Assert.That(notes.Select(n => n.Result), Is.All.EqualTo(NoteResult.Perfect));
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

        [TestCase("style", 9)]
        [TestCase("style", 2.5)]
        [TestCase("effectKind", 11)]
        [TestCase("effectKind", -1)]
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

        private sealed class SoundRecorder : LiveViewBase
        {
            internal readonly System.Collections.Generic.List<string> Cues = new();
            public override void NativeHitSound(NoteBase note, string cue) => Cues.Add(cue);
        }

        [Test]
        public void RealAndFakeKeyFamiliesUseTheirAuthoredSkinAndSoundFamilies()
        {
            var families = new[]
            {
                ("Tap", NoteCategory.Normal, NextRushEffectKind.Basic, NextRushEffectKind.CriticalBasic),
                ("Flick", NoteCategory.Flick, NextRushEffectKind.Flick, NextRushEffectKind.CriticalFlick),
                ("Trace", NoteCategory.Friction, NextRushEffectKind.Trace, NextRushEffectKind.CriticalTrace),
                ("TraceFlick", NoteCategory.FrictionFlick, NextRushEffectKind.Flick, NextRushEffectKind.CriticalFlick),
                ("Release", NoteCategory.Long, NextRushEffectKind.Basic, NextRushEffectKind.Basic),
                ("Tick", NoteCategory.Connection, NextRushEffectKind.Tick, NextRushEffectKind.CriticalTick)
            };
            foreach (var (name, family, normal, critical) in families)
            foreach (string position in name == "Tick" ? new[] { "" } : new[] { "", "Head", "Tail" })
            foreach (bool isCritical in new[] { false, true })
            foreach (bool fake in new[] { false, true })
            {
                string archetype = (fake ? "Fake" : "") + (isCritical ? "Critical" : "Normal") + position + name + "Note";
                var key = N(archetype, "key", 4);
                NoteCategory category = name == "Tap" && position.Length != 0 ? NoteCategory.Long : family;
                NextRushEffectKind effect = name == "Tap" && position.Length != 0 ? NextRushEffectKind.Basic
                    : isCritical ? critical : normal;
                var entities = new List<JObject> { key };
                if (!fake && position == "Head")
                {
                    Add(key, R("next", "end"));
                    entities.Add(N("AnchorNote", "end", 6));
                    entities.Add(Segment("key", "end", "key", "end", "key", "end"));
                }
                else if (!fake && position == "Tail")
                {
                    var head = N("NormalHeadTapNote", "start", 2);
                    Add(head, R("next", "key"));
                    Add(key, R("activeHead", "start"));
                    entities.Add(head);
                    entities.Add(Segment("start", "key", "start", "key", "start", "key"));
                }

                var score = Load(Chart(entities.ToArray())).Score;
                var authoredKey = score.NoteArray.SelectMany(note => note.NoteList).Distinct().Single(note =>
                    note.MusicScoreInfo.time == 2 && note.Category != NoteCategory.FrictionHideLong
                    && note.Category != NoteCategory.Hidden);
                Assert.That(authoredKey.IsVisible, Is.True, archetype);
                Assert.That(authoredKey.IsDecoration, Is.EqualTo(fake), archetype);
                Assert.That(authoredKey.Category, Is.EqualTo(category), archetype);
                Assert.That(authoredKey.Type, Is.EqualTo(isCritical ? NoteType.Critical : NoteType.Default), archetype);
                Assert.That(authoredKey.NativeEffectKind, Is.EqualTo(effect), archetype);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DamageSoundUsesTheJudgmentAndTheAuthoredOverride(bool overrideCue)
        {
            var hit = N("DamageNote", "hit", 2);
            var avoid = N("DamageNote", "avoid", 4);
            if (overrideCue)
            {
                Add(hit, V("effectKind", 3));
                Add(avoid, V("effectKind", 3));
            }
            var score = Load(Chart(hit, avoid)).Score;
            var recorder = new GameObject("Sound recorder").AddComponent<SoundRecorder>();
            try
            {
                using var harness = new LiveHarness();
                harness.Arrange(score);
                harness.BindViews(recorder);
                harness.Step(1f, 11d, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(5.5f), 11d));
                harness.Step(2f, 12d);
                Assert.That(score.NoteArray.Select(n => n.Result), Is.EqualTo(new[] { NoteResult.Miss, NoteResult.Perfect }));
                Assert.That(recorder.Cues, Is.EqualTo(overrideCue
                    ? new[] { LiveSoundDefine.SE_LIVE_FLICK, LiveSoundDefine.SE_LIVE_FLICK }
                    : new[] { LiveSoundDefine.SE_LIVE_GOOD }));
            }
            finally { UnityEngine.Object.DestroyImmediate(recorder.gameObject); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StyledTapsUseTheirAuthoredSoundsInManualAndAutoplay(bool auto)
        {
            var entities = Enumerable.Range(0, 11).Select(i =>
            {
                var tap = N("NormalTapNote", null, i + 1);
                Add(tap, V("style", 2), V("effectKind", i));
                return tap;
            }).ToArray();
            var score = Load(Chart(entities)).Score;
            var recorder = new GameObject("SoundRecorder").AddComponent<SoundRecorder>();
            try
            {
                using var harness = new LiveHarness();
                harness.Arrange(score, auto: auto);
                harness.BindViews(recorder);
                for (int i = 0; i < entities.Length; i++)
                {
                    float time = (i + 1) * 0.5f;
                    if (auto) harness.Step(time, 10 + time);
                    else harness.Step(time, 10 + time, LiveHarness.Touch(i + 1, LiveConfig.JudgmentPositions[5], 10 + time));
                }
                Assert.That(score.NoteArray.Select(n => n.Result), Is.All.EqualTo(auto ? NoteResult.Auto : NoteResult.Perfect));
                Assert.That(recorder.Cues, Is.EqualTo(new[] { LiveSoundDefine.SE_LIVE_PERFECT, LiveSoundDefine.SE_LIVE_PERFECT,
                    LiveSoundDefine.SE_LIVE_FLICK, LiveSoundDefine.SE_LIVE_TRACE, LiveSoundDefine.SE_LIVE_CONNECT,
                    LiveSoundDefine.SE_LIVE_CRITICAL, LiveSoundDefine.SE_LIVE_FLICK_CRITICAL,
                    LiveSoundDefine.SE_LIVE_FRICTION_CRITICAL, LiveSoundDefine.SE_LIVE_CONNECT_CRITICAL }));
            }
            finally { UnityEngine.Object.DestroyImmediate(recorder.gameObject); }
        }

        [TestCase("custom01")]
        [TestCase("custom02")]
        public void SkinVariantsRecolorTheExistingFlickBodyAndArrowsAndResetOnReuse(string skin)
        {
            var view = UnityEngine.Object.Instantiate(Resources.Load<FlickNoteView>("live/note/" + skin + "/FlickNote"));
            try
            {
                var renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
                var block = new MaterialPropertyBlock();
                foreach (int style in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 0 })
                {
                    var flick = N("NormalFlickNote", null, 2);
                    Add(flick, V("style", style));
                    view.Spawn(Load(Chart(flick)).Score.NoteArray.Single(), 0);
                    foreach (var renderer in renderers)
                    {
                        Assert.That(renderer.sharedMaterial.HasProperty("_NextRushPalette"), Is.True, renderer.name);
                        renderer.GetPropertyBlock(block);
                        Assert.That(block.GetColor("_NextRushPalette"), Is.EqualTo(style == 0 ? Color.clear :
                            style == 1 ? Color.gray : NextRushConnectorSegment.PaletteColor(style)));
                    }
                    if (style == 2 || style == 4)
                    {
                        var note = Load(Chart(flick)).Score.NoteArray.Single();
                        note.ResetNote();
                        Assert.That(note.NativeStyle, Is.EqualTo(style));
                        Assert.That(note.NativeEffectKind, Is.EqualTo(NextRushEffectKind.Flick));
                        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                        {
                            var camera = new GameObject("PaletteCamera").AddComponent<Camera>();
                            try
                            {
                                camera.orthographic = true;
                                camera.orthographicSize = 1.5f;
                                var body = renderers.First(r => r.drawMode != SpriteDrawMode.Simple);
                                view.transform.localScale = Vector3.one;
                                var center = body.bounds.center;
                                camera.transform.position = center - Vector3.forward * 10f;
                                var points = Enumerable.Range(-8, 17).Select(i => center + Vector3.up * (i / 16f)).ToArray();
                                var pixels = ReadGuidePixels(camera, body, points);
                                Assert.That(pixels.Any(c => style == 2 ? c.r > c.b + 0.05f : c.b > c.r + 0.05f), Is.True,
                                    "The rendered key must contain the selected palette color");
                            }
                            finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }
                        }
                    }
                    view.Unspawn();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
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

        [TestCase(0, 0, false)]
        [TestCase(-6.5, 0, false)]
        [TestCase(6.5, 0, false)]
        [TestCase(-6, 1, false)]
        [TestCase(6, 1, false)]
        [TestCase(-9, 0.5, false)]
        [TestCase(-9, 0.5, true)]
        [TestCase(4096.25, .5, false)]
        [TestCase(-65536.25, 0, false)]
        [TestCase(-65536.25, .25, true)]
        [TestCase(0, 8192.5, false)]
        public void BoundaryKeysKeepTheirAuthoredPositionAndReceiveTouches(double lane, double size, bool mirror)
        {
            var json = Chart(N("NormalTapNote", null, 2, lane, size), N("NormalTapNote", null, 4, lane, size));
            var chart = NextRushChartLoader.Load(NextRushChart.Parse(json.ToString()), _config, isMirror: mirror);
            var tap = chart.Score.NoteArray[0];
            float center = 5.5f + (float)(mirror ? -lane : lane);
            Assert.That(tap.NativeScroll, Is.Not.Null);
            Assert.That((tap.NativeLeft + tap.NativeRight) / 2, Is.EqualTo(center));
            Assert.That(tap.NativeRight - tap.NativeLeft + 1, Is.EqualTo(2 * size));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            Assert.That(LiveConfig.IsKeyVisible(tap, 0.9f), Is.EqualTo(size > 0));
            var position = LiveConfig.NativeLanePosition(center);
            harness.Step(1, 11, LiveHarness.Touch(1, position, 11));
            Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));
            harness.Step(2, 12, LiveHarness.Touch(2, position + Vector2.up * 20, 12));
            harness.Step(2.3f, 12.3);
            Assert.That(chart.Score.NoteArray[1].Result, Is.EqualTo(NoteResult.Miss));
        }

        [Test]
        public void AnOffstageTapAndHazardDistinguishTheNearestLaneFromTheirOwnPosition()
        {
            var chart = Load(Chart(N("NormalTapNote", "tap", 2, -9),
                N("DamageNote", "damage", 4, -9), N("DamageNote", "avoided", 6, -9)));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var edge = LiveConfig.JudgmentPositions[0];
            var outside = LiveConfig.NativeLanePosition(-3.5f);
            harness.Step(1, 11, LiveHarness.Touch(1, edge, 11));
            harness.Step(1.3f, 11.3);
            Assert.That(chart.Score.NoteArray[0].Result, Is.EqualTo(NoteResult.Miss));
            harness.Step(2, 12, LiveHarness.Touch(2, outside, 12));
            harness.Step(3, 13, LiveHarness.Touch(3, edge, 13));
            Assert.That(chart.Score.NoteArray[1].Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(chart.Score.NoteArray[2].Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase(-9)]
        [TestCase(8192)]
        public void OffstageHoldAcceptsItsZeroWidthHeadAndReleaseTail(int lane)
        {
            var head = N("NormalHeadTapNote", "h", 2, lane, 0);
            var tail = N("NormalTailReleaseNote", "t", 4, lane + 1, 0);
            Add(head, R("next", "t"));
            Add(tail, R("activeHead", "h"));
            var chart = Load(Chart(head, tail, Segment("h", "t", "h", "t", "h", "t")));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            for (int frame = 60; frame <= 120; frame++)
            {
                float t = frame / 60f;
                var pos = LiveConfig.NativeLanePosition(lane + 5.5f + t - 1f);
                var phase = frame == 60 ? InputTouchPhase.Began : frame == 120 ? InputTouchPhase.Ended : InputTouchPhase.Moved;
                harness.Step(t, 10d + t, LiveHarness.Touch(1, pos, 10d + t, phase));
            }
            Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase(1e100, 1)]
        [TestCase(0, 1.5e9)]
        [TestCase(1e9, 0)]
        public void UnrepresentableLaneGeometryFailsDuringLoad(double lane, double size)
        {
            Assert.Throws<InvalidDataException>(() => Load(Chart(N("NormalTapNote", null, 2, lane, size))));
        }

        [Test]
        public void SubLaneTapUsesAuthoredWidthWithoutAScrollGroup()
        {
            var source = NextRushChart.Parse(Chart(N("NormalTapNote", "tap", 2, 2.25, 0.25)).ToString());
            var score = NextRushChartLoader.Load(source, _config, isMirror: true).Score;
            var tap = score.NoteArray.Single();
            Assert.That(tap.NativeScroll, Is.Not.Null);
            Assert.That(tap.NativeLeft, Is.EqualTo(3.5f));
            Assert.That(tap.NativeRight - tap.NativeLeft + 1, Is.EqualTo(0.5f));
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(1, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(3.25f), 11));
            Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConnectorUsesAttachedHiddenTickEndpointsWithoutExtraJudgments(bool guide)
        {
            var head = N("NormalHeadTapNote", "head", 1, -4, 1);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4, 2);
            Add(head, R("next", "tail"), V("connectorEase", 2));
            JObject Tick(string name, int beat) => E("TransientHiddenTickNote", name, V("#BEAT", beat),
                V("isAttached", 1), R("attachHead", "head"), R("attachTail", "tail"));
            JObject Edge(string a, string b) => Segment(a, b, "head", "tail", "head", "tail");
            var edges = guide
                ? new[] { Segment("a", "b", "a", "b"), Edge("head", "tail") }
                : new[] { Edge("b", "tail"), Edge("a", "b"), Edge("head", "a") };
            var chart = Load(Chart(edges.Concat(new[] { Tick("b", 4), tail, Tick("a", 2), head }).ToArray()));
            var hold = chart.Score.NoteArray.OfType<LongNote>().Single(n => n is not GuideNote);
            var line = guide ? chart.Score.NoteArray.OfType<GuideNote>().Single() : hold;
            var endpoints = line.LineNoteList.Cast<NoteBase>().Where(n => n.HasNativeAttachment).ToArray();
            Assert.That(endpoints.Select(n => n.NativeLeft), Is.EqualTo(new[] { 1 + 7f / 16, 1 + 63f / 16 }));
            Assert.That(endpoints.Select(n => n.NativeRight), Is.EqualTo(new[] { 2 + 9f / 16, 2 + 81f / 16 }));
            // Halfway between fractions 1/4 and 3/4 on the original quadratic.
            Assert.That(endpoints[0].NativeConnector.Interpolate(0.5f), Is.EqualTo(0.375f));
            var ticks = hold.JudgedPoints.OfType<LongHoldCombo>().ToArray();
            Assert.That(ticks.Length, Is.EqualTo(2));
            Assert.That(ticks.All(n => !n.IsVisible), Is.True);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(4));
            AssertCompletesAutoplay(chart);
            Assert.That(ticks.Select(n => n.Result), Is.All.EqualTo(NoteResult.Auto));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HiddenTicksCanUseTwoAttachedHiddenEndpointsAndASameBeatAlias(bool parentsFirst)
        {
            var head = N("NormalHeadTapNote", "head", 1, -4, 1);
            Add(head, R("next", "tail"), V("connectorEase", 2));
            var tail = N("NormalTailReleaseNote", "tail", 5, 4, 2);
            Add(tail, R("activeHead", "head"));
            JObject Tick(string name, double beat, string from, string to) =>
                E("TransientHiddenTickNote", name, V("#BEAT", beat), V("isAttached", 1),
                    R("attachHead", from), R("attachTail", to));
            var ticks = new[] { Tick("alias", 3, "middle", "middle"), Tick("middle", 3, "a", "b"),
                Tick("b", 4, "head", "tail"), Tick("a", 2, "head", "tail") };
            var ordered = parentsFirst ? ticks.Reverse() : ticks;
            var json = Chart(ordered.Concat(new[] { tail, Segment("head", "tail", "head", "tail", "head", "tail"), head }).ToArray());
            var chart = Load(json);
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var points = bar.JudgedPoints.OfType<LongHoldCombo>().ToArray();
            Assert.That(points.Length, Is.EqualTo(4));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(6));
            Assert.That(bar.LineNoteList.Count, Is.EqualTo(2));
            var middle = points.Where(n => n.MusicScoreInfo.time == 1.5f).ToArray();
            Assert.That(middle.Length, Is.EqualTo(2));
            // The parent fractions are 1/16 and 9/16; quadratic interpolation halfway between
            // those points gives 3/16 of the full change in each lane boundary.
            Assert.That(middle.Select(n => n.NativeLeft), Is.All.EqualTo(1 + 7 * 3f / 16));
            Assert.That(middle.Select(n => n.NativeRight), Is.All.EqualTo(2 + 9 * 3f / 16));
            Assert.That(points.All(n => !n.IsVisible && n.ParentNote == bar), Is.True);
            AssertCompletesAutoplay(chart);
            Assert.That(points.Select(n => n.Result), Is.All.EqualTo(NoteResult.Auto));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NestedHiddenTickChecksItsOwnGuidePositionAfterTheParentMisses(bool touchChild)
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            Add(head, R("next", "tail"));
            var guide = N("AnchorNote", "guide", 1, 0);
            Add(guide, V("segmentKind", 101));
            JObject Tick(string name, int beat, string from) => E("TransientHiddenTickNote", name, V("#BEAT", beat),
                V("isAttached", 1), R("attachHead", from), R("attachTail", "end"), R("activeHead", "head"));
            var chart = Load(Chart(Tick("child", 4, "parent"), Tick("parent", 2, "guide"), head, guide,
                N("AnchorNote", "end", 5, 4), N("AnchorNote", "tail", 5, -4),
                Segment("guide", "end", "guide", "end"), Segment("head", "tail", "head", "tail", "head", "tail")));
            var hidden = chart.Score.NoteArray.OfType<NextRushHiddenTickNote>().ToArray();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.BindViews();
            harness.Step(.5f, 10.5, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(1.5f), 10.5));
            harness.Step(1, 11);
            harness.Step(1.03f, 11.03);
            harness.Step(2, 12, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(touchChild ? 8.5f : 1.5f), 12));
            harness.Step(2.03f, 12.03);
            Assert.That(hidden.Select(n => n.Result), Is.EqualTo(new[] { NoteResult.Miss,
                touchChild ? NoteResult.Perfect : NoteResult.Miss }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HiddenTickAttachmentCyclesAreRejected(bool selfReference)
        {
            JObject Tick(string name, string from) => E("TransientHiddenTickNote", name, V("#BEAT", 2),
                V("isAttached", 1), R("attachHead", from), R("attachTail", from));
            var chart = Chart(Tick("a", selfReference ? "a" : "b"), Tick("b", "a"));
            Assert.That(() => Load(chart), Throws.TypeOf<InvalidDataException>().With.Message.Contains("循环"));
        }

        [Test]
        public void SameBeatNestedAttachmentInheritsCurveAndJudgmentSection()
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            Add(head, R("next", "tail"), V("connectorEase", 2));
            Add(tail, R("activeHead", "head"));
            JObject Point(string kind, string name, string a, string b) =>
                E(kind, name, V("#BEAT", 3), V("isAttached", 1), R("attachHead", a), R("attachTail", b));
            var tick = Point("NormalTickNote", "tick", "second", "second");
            Add(tick, R("activeHead", "head"));
            var second = Point("AnchorNote", "second", "first", "first");
            Add(second, R("#TIMESCALE_GROUP", "fast"));
            var chart = Load(Chart(tick, second,
                Point("AnchorNote", "first", "head", "tail"), tail, head,
                E("#TIMESCALE_GROUP", "fast", R("first", "speed")),
                E("#TIMESCALE_CHANGE", "speed", V("#BEAT", 0), V("#TIMESCALE", 2)),
                Segment("head", "tail", "head", "tail", "head", "tail")));
            var point = chart.Score.NoteArray.OfType<LongNote>().Single().JudgedPoints.Single();
            Assert.That(point.NativeLeft, Is.EqualTo(3f));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Logic.Notes.BindNativeVisuals(1);
                Assert.That(point.NativeViewProgress(1.25, out _), Is.EqualTo(0.5).Within(1e-6));
            }
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            AssertCompletesAutoplay(chart);
        }

        [Test]
        public void TicksBetweenAttachedSeparatorsUseTheirGeometryAndScrollGroups()
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            Add(head, R("next", "tail"), V("connectorEase", 2));
            Add(tail, R("activeHead", "head"));
            JObject Separator(string name, int beat) => E("AnchorNote", name, V("#BEAT", beat),
                V("isAttached", 1), V("isSeparator", 1), R("attachHead", "head"), R("attachTail", "tail"));
            var first = Separator("a", 2);
            Add(first, R("#TIMESCALE_GROUP", "fast"));
            JObject Tick(string kind) => E(kind, null, V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "a"), R("attachTail", "b"), R("activeHead", "head"));
            var json = Chart(Tick("NormalTickNote"), Tick("TransientHiddenTickNote"), Separator("b", 4),
                first, tail, head, Segment("head", "a", "head", "tail", "head", "tail"),
                Segment("a", "b", "head", "tail", "head", "tail"),
                Segment("b", "tail", "head", "tail", "head", "tail"),
                E("#TIMESCALE_GROUP", "fast", R("first", "speed")),
                E("#TIMESCALE_CHANGE", "speed", V("#BEAT", 0), V("#TIMESCALE", 2)));
            var chart = Load(json);
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var visible = bar.JudgedPoints.OfType<ConnectionNote>().Single();
            var hidden = bar.JudgedPoints.OfType<LongHoldCombo>().Single();
            Assert.That(hidden.LaneStartF, Is.EqualTo(2.5f).Within(1e-6));
            Assert.That(visible.NativeLeft, Is.EqualTo(2.5f).Within(1e-6));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Logic.Notes.BindNativeVisuals(1);
                Assert.That(visible.NativeViewProgress(0.5, out _), Is.EqualTo(-0.25).Within(1e-6));
            }
            Assert.That(chart.TotalNoteCount, Is.EqualTo(4));
            AssertCompletesAutoplay(chart);
        }

        [Test]
        public void NestedSeparatorSplitsAConnectingEdgeAndTicksCanSpanSeveralEdges()
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            Add(head, R("next", "tail"), V("connectorEase", 2));
            Add(tail, R("activeHead", "head"));
            JObject Point(string kind, string name, double beat, string a, string b) =>
                E(kind, name, V("#BEAT", beat), V("isAttached", 1), R("attachHead", a), R("attachTail", b));
            var first = Point("AnchorNote", "a", 2, "head", "tail");
            var middle = Point("AnchorNote", "middle", 3, "a", "b");
            Add(middle, V("isSeparator", 1), R("#TIMESCALE_GROUP", "fast"));
            var last = Point("AnchorNote", "b", 4, "head", "tail");
            JObject Edge(string a, string b) => Segment(a, b, "head", "tail", "head", "tail");
            var data = new[] { Point("NormalTickNote", "visible", 4, "middle", "tail"),
                Point("TransientHiddenTickNote", null, 3, "head", "b"), middle, last, first, tail, head,
                Edge("head", "a"), Edge("a", "middle"), Edge("middle", "b"), Edge("b", "tail"),
                E("#TIMESCALE_GROUP", "fast", R("first", "speed")),
                E("#TIMESCALE_CHANGE", "speed", V("#BEAT", 0), V("#TIMESCALE", 2)) };
            foreach (var entities in new[] { data, data.Reverse().ToArray() })
            {
                var chart = Load(Chart(entities));
                var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
                Assert.That(bar.LineNoteList.Cast<NoteBase>().Select(n => n.NativeLeft),
                    Is.EqualTo(new[] { 1f, 1.5f, 2.5f, 5.5f, 9f }).Within(1e-5));
                var visible = bar.JudgedPoints.OfType<ConnectionNote>().Single();
                Assert.That(visible.NativeLeft, Is.EqualTo(4.125f).Within(1e-5));
                Assert.That(bar.JudgedPoints.OfType<LongHoldCombo>().Single().LaneStartF, Is.EqualTo(3f).Within(1e-5));
                using (var harness = new LiveHarness())
                {
                    harness.Arrange(chart.Score);
                    harness.Logic.Notes.BindNativeVisuals(1);
                    Assert.That(visible.NativeViewProgress(1, out _), Is.EqualTo(-0.25).Within(1e-6));
                }
                Assert.That(chart.TotalNoteCount, Is.EqualTo(4));
                AssertCompletesAutoplay(chart);
            }
        }

        [Test]
        public void FloatingAttachmentReferencesCanSupplyANewGuideEndpoint()
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            Add(head, R("next", "tail"), V("connectorEase", 2));
            JObject Attached(string kind, string name, double beat, string a, string b) =>
                E(kind, name, V("#BEAT", beat), V("isAttached", 1), R("attachHead", a), R("attachTail", b));
            var chart = Load(Chart(Attached("AnchorNote", "guideStart", 3, "a", "b"),
                Attached("NormalTickNote", "tick", 3, "a", "b"), Attached("AnchorNote", "a", 2, "head", "tail"),
                Attached("AnchorNote", "b", 4, "head", "tail"), head, tail, N("AnchorNote", "end", 6),
                Segment("head", "tail", "head", "tail", "head", "tail"),
                Segment("guideStart", "end", "guideStart", "end")));
            var guide = chart.Score.NoteArray.OfType<GuideNote>().Single();
            Assert.That(guide.NativeLeft, Is.EqualTo(2.5f));
            Assert.That(guide.HasNativeAttachment, Is.True);
            var hold = chart.Score.NoteArray.OfType<LongNote>().Single(n => n != guide);
            Assert.That(hold.LineNoteList.Count, Is.EqualTo(2));
            Assert.That(hold.JudgedPoints.Single().NativeLeft, Is.EqualTo(guide.NativeLeft));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            AssertCompletesAutoplay(chart);
        }

        [Test]
        public void CrossBranchTickRequiresAnUnambiguousActiveArm()
        {
            var fork = N("AnchorNote", "fork", 1);
            var left = N("AnchorNote", "left", 2, -3);
            var right = N("AnchorNote", "right", 2, 3);
            var join = N("AnchorNote", "join", 4);
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "fork"), R("attachTail", "join"));
            JObject Json() => Chart(fork, left, right, join, tick,
                Segment("fork", "left", "fork", "left", "fork", "left"),
                Segment("fork", "right", "fork", "right", "fork", "right"),
                Segment("left", "join", "left", "join", "left", "join"),
                Segment("right", "join", "right", "join", "right", "join"));
            Assert.That(() => Load(Json()), Throws.TypeOf<InvalidDataException>().With.Message.Contains("判定段"));
            Add(tick, R("activeHead", "right"));
            var chart = Load(Json());
            var arm = chart.Score.NoteArray.OfType<LongNote>().Single(n => n.JudgedPoints.Count > 0);
            Assert.That(arm.NativeLeft, Is.EqualTo(8));
            Assert.That(arm.JudgedPoints.Single().LaneStartF, Is.EqualTo(5));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            AssertCompletesAutoplay(chart);
        }

        [Test]
        public void CyclicSameBeatAttachmentsAreRejected()
        {
            JObject Alias(string name, string target) => E("AnchorNote", name, V("#BEAT", 1),
                V("isAttached", 1), R("attachHead", target), R("attachTail", target));
            Assert.That(() => Load(Chart(Alias("a", "b"), Alias("b", "a"))),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("循环"));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TicksUseGuideGeometryAndAnIndependentHoldHead(bool crossChain, bool touchGuide)
        {
            var guideStart = N("AnchorNote", "g", 2, 1);
            var guideEnd = N("AnchorNote", "ge", 6, 5);
            Add(guideStart, V("segmentKind", 103));
            if (!crossChain) Add(guideStart, R("next", "ge"));
            var head = N("NormalHeadTapNote", "h", 2, -4);
            Add(head, R("next", "end"));
            var tail = N("AnchorNote", "end", 6, -4);
            JObject Tick(string kind) => E(kind, null, V("#BEAT", 4), V("isAttached", 1),
                R("attachHead", "g"), R("attachTail", "ge"), R("activeHead", "h"));
            var json = Chart(Tick("TransientHiddenTickNote"), Tick("CriticalTickNote"), guideEnd, head, tail, guideStart,
                Segment("h", "end", "h", "end", "h", "end"), Segment("g", "ge", "g", "ge"));
            var chart = Load(json);
            var ticks = chart.Score.NoteArray.OfType<NextRushConnectorTickNote>().ToArray();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            Assert.That(ticks.Length, Is.EqualTo(2));
            Assert.That(ticks.Single(t => t.IsVisible).Type, Is.EqualTo(NoteType.Critical));
            Assert.That(ticks.Select(t => t.LaneStartF), Is.All.EqualTo(8f));
            Assert.That(chart.Score.NoteArray.OfType<LongNote>().Select(b => b.LineNoteList.Count), Is.All.EqualTo(2));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.BindViews();
            var ownerLane = LiveConfig.NativeLanePosition(1.5f);
            harness.Step(1, 11, LiveHarness.Touch(1, ownerLane, 11));
            harness.Step(2, 12, LiveHarness.Touch(1, ownerLane, 12, InputTouchPhase.Stationary),
                LiveHarness.Touch(2, touchGuide ? LiveConfig.NativeLanePosition(8.5f) : ownerLane, 12));
            harness.Step(2.02f, 12.02);
            Assert.That(ticks.Select(t => t.Result), Is.All.EqualTo(touchGuide ? NoteResult.Perfect : NoteResult.Miss));
            var replay = Load(json);
            AssertCompletesAutoplay(replay);
            Assert.That(replay.Score.NoteArray.OfType<NextRushConnectorTickNote>().Select(t => t.Result), Is.All.EqualTo(NoteResult.Auto));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(true, false, "NormalTapNote")]
        [TestCase(true, true, "NormalTapNote")]
        [TestCase(true, false, "NormalFlickNote")]
        [TestCase(true, true, "NormalFlickNote")]
        public void DecorativelyAttachedTickWaitsForItsAuthoredTapHeadOnTheCrossingFrame(bool fork, bool pressHead,
            string archetype = "NormalHeadTapNote")
        {
            var head = N(archetype, "h", 4, -4);
            var guide = N("AnchorNote", "g", 2, 3);
            Add(guide, V("segmentKind", 101));
            var entities = new System.Collections.Generic.List<JObject> {
                E("NormalTickNote", null, V("#BEAT", 4), V("isAttached", 1),
                    R("attachHead", "g"), R("attachTail", "ge"), R("activeHead", "h")),
                guide, N("AnchorNote", "ge", 6, 3), head, N("AnchorNote", "end", 6, -4),
                Segment("g", "ge", "g", "ge"), Segment("h", "end", "h", "end", "h", "end") };
            if (fork)
            {
                entities.Add(N("AnchorNote", "branch", 6, 0));
                entities.Add(Segment("h", "branch", "h", "branch", "h", "branch"));
            }
            var chart = Load(Chart(entities.ToArray()));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.BindViews();
            var position = LiveConfig.NativeLanePosition(8.5f);
            harness.Step(1.98f, 11.98, LiveHarness.Touch(1, position, 11.98));
            var contact = LiveHarness.Touch(1, position, 12, InputTouchPhase.Stationary);
            if (pressHead)
                harness.Step(2, 12, contact, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(1.5f), 12));
            else harness.Step(2, 12, contact);
            harness.Step(2.02f, 12.02);
            Assert.That(chart.Score.NoteArray.OfType<NextRushConnectorTickNote>().Single().Result,
                Is.EqualTo(pressHead ? NoteResult.Perfect : NoteResult.Miss));
        }

        [TestCase(true, true, NoteResult.Perfect, false)]
        [TestCase(false, true, NoteResult.Miss, false)]
        [TestCase(true, false, NoteResult.Miss, false)]
        [TestCase(true, true, NoteResult.Perfect, true)]
        [TestCase(false, true, NoteResult.Miss, true)]
        [TestCase(true, false, NoteResult.Miss, true)]
        public void TicksOnAnotherPlayableHoldUseItsGeometryAndTheirDeclaredHead(
            bool pressOwner, bool touchGeometry, NoteResult expected, bool damagePath)
        {
            var path = N("NormalHeadTapNote", "path", 2, 1);
            Add(path, R("next", "pathEnd"));
            if (damagePath) Add(path, V("segmentKind", 3));
            var owner = N("NormalHeadTapNote", "owner", 4, -4);
            Add(owner, R("next", "ownerEnd"));
            JObject Tick(string kind) => E(kind, null, V("#BEAT", 4), V("isAttached", 1),
                R("attachHead", "path"), R("attachTail", "pathEnd"), R("activeHead", "owner"));
            var json = Chart(Tick("NormalTickNote"), Tick("TransientHiddenTickNote"), path, owner,
                N("AnchorNote", "pathEnd", 6, 5), N("AnchorNote", "ownerEnd", 6, -4),
                Segment("path", "pathEnd", "path", "pathEnd", "path", "pathEnd"),
                Segment("owner", "ownerEnd", "owner", "ownerEnd", "owner", "ownerEnd"));
            var chart = Load(json);
            var ticks = chart.Score.NoteArray.OfType<NextRushConnectorTickNote>().ToArray();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(4));
            Assert.That(ticks.Length, Is.EqualTo(2));
            Assert.That(ticks.Count(t => t.IsVisible), Is.EqualTo(1));
            Assert.That(ticks.Select(t => t.LaneStartF), Is.All.EqualTo(8f));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.BindViews();
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(6.5f), 11));
            harness.Step(1.1f, 11.1, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(6.5f), 11.1, InputTouchPhase.Ended));
            var touches = new List<LiveTouchSample>();
            if (pressOwner) touches.Add(LiveHarness.Touch(2, LiveConfig.NativeLanePosition(1.5f), 12));
            if (touchGeometry) touches.Add(LiveHarness.Touch(3, LiveConfig.NativeLanePosition(8.5f), 12));
            harness.Step(2f, 12, touches.ToArray());
            harness.Step(2.02f, 12.02);
            Assert.That(ticks.Select(t => t.Result), Is.All.EqualTo(expected));

            var replay = Load(json);
            AssertCompletesAutoplay(replay);
            Assert.That(replay.Score.NoteArray.OfType<NextRushConnectorTickNote>().Select(t => t.Result),
                Is.All.EqualTo(NoteResult.Auto));
        }

        [TestCase(1.998f, 1.5f, NoteResult.Perfect, NoteResult.Perfect)]
        [TestCase(1.998f, 9.5f, NoteResult.Miss, NoteResult.Miss)]
        [TestCase(2f, 9.5f, NoteResult.Miss, NoteResult.Perfect)]
        [TestCase(2f, 1.5f, NoteResult.Miss, NoteResult.Miss)]
        public void ConnectedVisibleTicksUseTheFirstTickCrossedThisFrame(float previousFrame, float lane,
            NoteResult first, NoteResult second)
        {
            var head = N("NormalHeadTapNote", "head", 2, -4);
            var a = N("NormalTickNote", "a", 4, -4, 0.5);
            var turn = N("AnchorNote", "turn", 4.01, 3, 0.5);
            var b = N("CriticalTickNote", "b", 4.02, 4, 0.5);
            var end = N("AnchorNote", "end", 6, -4);
            Add(a, R("activeHead", "head"));
            Add(b, R("activeHead", "head"));
            Add(head, R("next", "a"));
            Add(a, R("next", "turn"));
            Add(turn, R("next", "b"));
            Add(b, R("next", "end"));
            JObject Edge(string from, string to) => Segment(from, to, "head", "end", "head", "end");
            var score = Load(Chart(b, end, turn, head, a, Edge("head", "a"), Edge("a", "turn"),
                Edge("turn", "b"), Edge("b", "end"))).Score;
            var ticks = score.NoteArray.SelectMany(n => n.NoteList).OfType<ConnectionNote>()
                .OrderBy(n => n.MusicScoreInfo.time).ToArray();

            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(1.5f), 11));
            harness.Step(previousFrame, 10d + previousFrame);
            harness.Step(2.014f, 12.014, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(lane), 12.014));
            harness.Step(2.03f, 12.03);
            Assert.That(ticks.Select(tick => tick.Result), Is.EqualTo(new[] { first, second }));
        }

        [TestCase(false, 6f, NoteResult.Perfect)]
        [TestCase(false, 1.5f, NoteResult.Miss)]
        [TestCase(false, 8f, NoteResult.Miss)]
        [TestCase(true, 9.5f, NoteResult.Perfect)]
        [TestCase(true, 6f, NoteResult.Miss)]
        [TestCase(false, 6f, NoteResult.Perfect, true)]
        [TestCase(true, 9.5f, NoteResult.Perfect, true)]
        [TestCase(false, 6f, NoteResult.Perfect, true, "TransientHiddenTickNote")]
        [TestCase(false, 1.5f, NoteResult.Miss, true, "TransientHiddenTickNote")]
        [TestCase(true, 9.5f, NoteResult.Perfect, true, "TransientHiddenTickNote")]
        [TestCase(true, 6f, NoteResult.Miss, true, "TransientHiddenTickNote")]
        public void AttachedConnectorTickFollowsCrossedNodesAndStopsAtTheFirstVisibleTick(bool visibleTurn,
            float lane, NoteResult expected, bool independentOwner = false, string archetype = "CriticalTickNote")
        {
            var head = N("NormalHeadTapNote", "head", 2, -4, 0.5);
            var attachTail = N("AnchorNote", "attachTail", 4.004, -4, 0.5);
            var turn = N(visibleTurn ? "NormalTickNote" : "AnchorNote", "turn", 4.008, 4, 0.5);
            var curveStart = N("AnchorNote", "curveStart", 4.012, -4, 0.5);
            var end = N("AnchorNote", "end", 4.052, 4, 0.5);
            var attached = E(archetype, "attached", V("#BEAT", 4.002), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "attachTail"),
                R("activeHead", independentOwner ? "owner" : "head"));
            Add(head, R("next", "attachTail"));
            Add(attachTail, R("next", "turn"));
            Add(turn, R("next", "curveStart"));
            Add(curveStart, R("next", "end"), V("connectorEase", 2));
            if (visibleTurn) Add(turn, R("activeHead", "head"));
            JObject Edge(string from, string to) => Segment(from, to, "head", "end", "head", "end");
            var entities = new List<JObject> { head, attached, attachTail, turn, curveStart, end,
                Edge("head", "attachTail"), Edge("attachTail", "turn"), Edge("turn", "curveStart"),
                Edge("curveStart", "end") };
            if (independentOwner)
            {
                var owner = N("NormalHeadTapNote", "owner", 2, 0, 0.5);
                Add(owner, R("next", "ownerEnd"));
                entities.Add(owner);
                entities.Add(N("AnchorNote", "ownerEnd", 6, 0, 0.5));
                entities.Add(Segment("owner", "ownerEnd", "owner", "ownerEnd", "owner", "ownerEnd"));
            }
            var score = Load(Chart(entities.ToArray())).Score;
            var tick = score.NoteArray.SelectMany(n => n.NoteList).OfType<NextRushConnectorTickNote>().Single();

            using var harness = new LiveHarness();
            harness.Arrange(score);
            var headTouch = LiveHarness.Touch(1, LiveConfig.NativeLanePosition(1.5f), 11);
            if (independentOwner)
                harness.Step(1f, 11, headTouch, LiveHarness.Touch(3, LiveConfig.NativeLanePosition(5.5f), 11));
            else harness.Step(1f, 11, headTouch);
            harness.Step(1.998f, 11.998);
            // At 2.021 the head-to-tail chord is long past. The last segment is 75% through its
            // quadratic ease (lane 6 instead of 7.5), unless the frame crosses the visible turn tick.
            harness.Step(2.021f, 12.021, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(lane), 12.021));
            harness.Step(2.024f, 12.024);
            Assert.That(tick.Result, Is.EqualTo(expected));
        }

        [TestCase(6f, false, NoteResult.Perfect)]
        [TestCase(1.5f, false, NoteResult.Miss)]
        [TestCase(6f, true, NoteResult.Miss)]
        public void UnattachedHiddenTickWithActiveHeadFollowsTheCurveOnItsCrossingFrame(float lane, bool late,
            NoteResult expected)
        {
            var head = N("NormalHeadTapNote", "head", 2, -4, 0.5);
            var tickNode = N("TransientHiddenTickNote", "tick", 4.002, -4, 0.5);
            var turn = N("AnchorNote", "turn", 4.008, 4, 0.5);
            var curveStart = N("AnchorNote", "curveStart", 4.012, -4, 0.5);
            var end = N("AnchorNote", "end", 4.052, 4, 0.5);
            Add(head, R("next", "tick"));
            Add(tickNode, R("next", "turn"), R("activeHead", "head"));
            Add(turn, R("next", "curveStart"));
            Add(curveStart, R("next", "end"), V("connectorEase", 2));
            JObject Edge(string from, string to) => Segment(from, to, "head", "end", "head", "end");
            var score = Load(Chart(head, tickNode, turn, curveStart, end, Edge("head", "tick"),
                Edge("tick", "turn"), Edge("turn", "curveStart"), Edge("curveStart", "end"))).Score;
            var tick = score.NoteArray.OfType<NextRushHiddenTickNote>().Single();

            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(1.5f), 11));
            harness.Step(1.998f, 11.998);
            if (late) harness.Step(2.021f, 12.021);
            float touchTime = late ? 2.024f : 2.021f;
            harness.Step(touchTime, 10d + touchTime,
                LiveHarness.Touch(2, LiveConfig.NativeLanePosition(lane), 10d + touchTime));
            harness.Step(2.025f, 12.025);
            Assert.That(tick.Result, Is.EqualTo(expected));
        }

        private static JObject IndependentKeyOnForeignGuide(string archetype)
        {
            var guide = N("AnchorNote", "guide", 2, 1);
            Add(guide, V("segmentKind", 101));
            return Chart(E(archetype, "key", V("#BEAT", 4), V("isAttached", 1),
                    R("attachHead", "guide"), R("attachTail", "guideEnd"), R("activeHead", "owner")),
                guide, N("AnchorNote", "guideEnd", 6, 5), N("NormalHeadTapNote", "owner", 4, -4),
                N("AnchorNote", "end", 6, -4), Segment("guide", "guideEnd", "guide", "guideEnd"),
                Segment("owner", "end", "owner", "end", "owner", "end"));
        }

        [TestCase("NormalTapNote")]
        [TestCase("CriticalFlickNote")]
        [TestCase("NormalTraceNote")]
        [TestCase("CriticalTraceFlickNote")]
        [TestCase("NormalReleaseNote")]
        [TestCase("DamageNote")]
        public void IndependentAttachedKeyUsesGuideGeometryAndScoresOnce(string archetype)
        {
            var chart = Load(IndependentKeyOnForeignGuide(archetype));
            var key = chart.Score.NoteArray.Single(n => n is not LongNote);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            Assert.That(key.ParentNote, Is.Null);
            Assert.That(key.LaneStartF, Is.EqualTo(8f));
            AssertCompletesAutoplay(chart);
            Assert.That(key.Result, Is.EqualTo(NoteResult.Auto));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AttachedTapUsesFreeAnchorsWithoutAConnector(bool sameAnchor)
        {
            var key = E("NormalTapNote", "key", V("#BEAT", 2), V("isAttached", 1),
                R("attachHead", "a"), R("attachTail", sameAnchor ? "a" : "b"));
            var first = N("AnchorNote", "a", sameAnchor ? 2 : 1, 2);
            if (!sameAnchor) Add(first, V("connectorEase", 1));
            var chart = Load(sameAnchor ? Chart(key, first) : Chart(key, first, N("AnchorNote", "b", 3, 4)));
            var tap = chart.Score.NoteArray.OfType<NormalNote>().Single();
            float left = sameAnchor ? 7f : 8f;
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            Assert.That(tap.LaneStartF, Is.EqualTo(left));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(left + 0.5f), 11));
            Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase(8.5f, NoteResult.Miss)]
        [TestCase(1.5f, NoteResult.Perfect)]
        public void AttachedDamageChecksTheGuideLaneEvenWhenAnotherHoldIsItsActiveHead(float lane, NoteResult expected)
        {
            var chart = Load(IndependentKeyOnForeignGuide("DamageNote"));
            var hazard = chart.Score.NoteArray.OfType<NextRushDamageNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(lane), 12));
            Assert.That(hazard.Result, Is.EqualTo(expected));
        }

        [TestCase("NormalTapNote", false, NoteResult.Perfect)]
        [TestCase("NormalReleaseNote", false, NoteResult.None)]
        [TestCase("NormalReleaseNote", true, NoteResult.Perfect)]
        public void IndependentAttachedKeyChecksItsOwnLaneAndReleaseOwner(string archetype, bool pressOwner, NoteResult expected)
        {
            var chart = Load(IndependentKeyOnForeignGuide(archetype));
            var key = chart.Score.NoteArray.OfType<NormalNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.NativeLanePosition(8.5f);
            bool release = archetype == "NormalReleaseNote";
            if (release) harness.Step(1.8f, 11.8, LiveHarness.Touch(1, lane, 11.8));
            if (pressOwner) harness.Step(1.98f, 11.98, LiveHarness.Touch(2, LiveConfig.NativeLanePosition(1.5f), 11.98));
            harness.Step(2f, 12, LiveHarness.Touch(1, lane, 12, release ? InputTouchPhase.Ended : InputTouchPhase.Began));
            Assert.That(key.Result, Is.EqualTo(expected));
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

        [TestCase("bypass")]
        [TestCase("through")]
        [TestCase("next")]
        [TestCase("prev")]
        public void AttachedSeparatorSplitsTheOriginalCurveWithoutRestartingItsEase(string links)
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            var middle = E("AnchorNote", "middle", V("#BEAT", 3), V("isAttached", 1), V("isSeparator", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            Add(head, V("connectorEase", 2));
            Add(tail, R("activeHead", "head"));
            if (links == "bypass" || links == "through")
            {
                Add(head, R("next", links == "through" ? "middle" : "tail"));
                Add(tail, R("prev", links == "through" ? "middle" : "head"));
            }
            if (links == "through" || links == "next") Add(middle, R("next", "tail"));
            if (links == "through" || links == "prev") Add(middle, R("prev", "head"));
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
                Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Perfect));
                Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [TestCase("NormalTickNote")]
        [TestCase("TransientHiddenTickNote")]
        public void FloatingLinkedTickKeepsItsJudgmentWithoutSplittingTheConnector(string kind)
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tail = N("NormalTailReleaseNote", "tail", 5, 4);
            Add(head, R("next", "tick"), V("connectorEase", 2));
            Add(tail, R("prev", "tick"), R("activeHead", "head"));
            var tick = E(kind, "tick", V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("prev", "head"), R("next", "tail"));
            var chart = Load(Chart(tick, tail, head, Segment("head", "tail", "head", "tail", "head", "tail")));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            Assert.That(bar.LineNoteList.Count, Is.EqualTo(2));
            Assert.That(bar.JudgedPoints.Single().LaneStartF, Is.EqualTo(3f));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            AssertCompletesAutoplay(chart);
        }

        [TestCase("next", "head")]
        [TestCase("prev", "tail")]
        [TestCase("prev", "missing")]
        [TestCase("next", "self")]
        public void InvalidAttachedNeighborIsRejected(string field, string reference)
        {
            var head = N("AnchorNote", "head", 1);
            var tail = N("AnchorNote", "tail", 5);
            Add(head, R("next", "tail"));
            var point = E("AnchorNote", "self", V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R(field, reference));
            Assert.Throws<InvalidDataException>(() => Load(Chart(head, tail, point, Segment("head", "tail", "head", "tail"))));
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
                Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
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
                Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(chart);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GuideReleaseTailUsesItsOwnLaneAndReleaseWindow(bool independentGeometry)
        {
            var guide = N("AnchorNote", "guide", 2, -3);
            var tail = N("CriticalTailReleaseNote", "tail", 4, 3);
            if (!independentGeometry)
            {
                Add(guide, R("next", "tail"));
                Add(tail, R("prev", "guide"));
            }
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

        [TestCase(2.04f, NoteResult.Perfect)]
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
                Is.EqualTo(NoteResult.Perfect));
            harness.Step(liftTime, 10d + liftTime, LiveHarness.Touch(1, lane, 10d + liftTime, InputTouchPhase.Ended));
            harness.Step(2.16f, 12.16);
            Assert.That(release.Result, Is.EqualTo(expected));
        }

        [TestCase(2.04f, NoteResult.Perfect)]
        [TestCase(2.09f, NoteResult.Miss)]
        [TestCase(2.12f, NoteResult.Great)]
        public void OrdinaryReleaseTailUsesNativeRecentPressLockout(float liftTime, NoteResult expected)
        {
            var head = N("NormalHeadTapNote", "head", 2);
            Add(head, R("next", "tail"));
            var score = Load(Chart(head, N("NormalTailReleaseNote", "tail", 4),
                Segment("head", "tail", "head", "tail", "head", "tail"),
                N("NormalTapNote", "tap", 3.88))).Score;
            using var harness = new LiveHarness();
            harness.Arrange(score);
            var lane = LiveConfig.NativeLanePosition(5.5f);
            harness.Step(1f, 11, LiveHarness.Touch(1, lane, 11));
            harness.Step(1.94f, 11.94, LiveHarness.Touch(2, lane, 11.94));
            harness.Step(liftTime, 10d + liftTime, LiveHarness.Touch(2, lane, 10d + liftTime, InputTouchPhase.Ended));
            harness.Step(2.2f, 12.2);
            Assert.That(score.NoteArray.OfType<LongNote>().Single().TailNote.Result, Is.EqualTo(expected));
        }

        [Test]
        public void NativeTapPublishesPerfectAndBadAsSeparateGrades()
        {
            var score = Load(Chart(N("NormalTapNote", "first", 2), N("NormalTapNote", "second", 4))).Score;
            using var harness = new LiveHarness();
            harness.Arrange(score);
            var board = new ScoreLogic(_config);
            board.Setup(null, score);
            harness.Logic.Session.ScoreLogic = board;
            harness.BindViews();
            var lane = LiveConfig.NativeLanePosition(5.5f);
            harness.Step(1f, 11, LiveHarness.Touch(1, lane, 11));
            harness.Step(2.117f, 12.117, LiveHarness.Touch(2, lane, 12.117));
            Assert.That(score.NoteArray.Select(n => n.Result), Is.EqualTo(new[] { NoteResult.Perfect, NoteResult.Bad }));
            Assert.That(board.Score.justPerfectCount, Is.Zero);
            Assert.That(board.Score.perfectCount, Is.EqualTo(1));
            Assert.That(board.Score.badCount, Is.EqualTo(1));
        }

        [TestCase("NormalReleaseNote", 1.94f, NoteResult.Great)]
        [TestCase("CriticalReleaseNote", 2.06f, NoteResult.Perfect)]
        [TestCase("CriticalReleaseNote", 2.15f, NoteResult.Miss)]
        public void StandaloneReleaseUsesTheLiftTimestampAndAsymmetricWindow(string archetype, float liftTime, NoteResult expected)
        {
            var json = Chart(N(archetype, "release", 4, 3));
            var chart = Load(json);
            var release = (NormalNote)chart.Score.NoteArray.Single();
            Assert.That(release.IsIndependentRelease, Is.True);
            Assert.That(release.Type, Is.EqualTo(archetype.StartsWith("Critical", StringComparison.Ordinal)
                ? NoteType.Critical : NoteType.Default));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var lane = LiveConfig.JudgmentPositions[8];
                harness.Step(1.85f, 11.85, LiveHarness.Touch(1, lane, 11.85));
                harness.Step(1.9f, 11.9, LiveHarness.Touch(1, lane, 11.85, InputTouchPhase.Stationary));
                Assert.That(release.Result, Is.EqualTo(NoteResult.None));
                harness.Step(2.17f, 12.17, LiveHarness.Touch(1, lane, 10d + liftTime, InputTouchPhase.Ended));
                Assert.That(release.Result, Is.EqualTo(expected));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void AHiddenTickCanUseItsAuthoredTapTailAfterTheBodyEnds()
        {
            var head = N("NormalHeadTapNote", "head", 2, -3);
            var tail = N("NormalTailTapNote", "tail", 4, 3);
            Add(head, R("next", "tail"));
            Add(tail, R("prev", "head"), R("activeHead", "head"));
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 3.94), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            var score = Load(Chart(head, tail, tick, Segment("head", "tail", "head", "tail", "head", "tail"))).Score;
            var bar = score.NoteArray.OfType<LongNote>().Single();
            var point = bar.NoteList.OfType<LongHoldCombo>().Single();
            var tailKey = score.NoteArray.OfType<NormalNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(1f, 11);
            harness.Step(1.2f, 11.2);
            harness.Step(2.002f, 12.002);

            Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            Assert.That(point.Result, Is.EqualTo(NoteResult.None));

            harness.Step(2.024f, 12.024, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[8], 12.024));

            Assert.That(tailKey.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(point.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase("NormalHeadReleaseNote", false)]
        [TestCase("CriticalHeadReleaseNote", true)]
        [TestCase("NormalReleaseNote", true)]
        [TestCase("CriticalReleaseNote", false)]
        [TestCase("NormalTailReleaseNote", true)]
        [TestCase("CriticalTailReleaseNote", false)]
        public void ReleaseHeadAndTapTailUseTheirOwnInputAcrossAMovingHold(string archetype, bool hitHead)
        {
            var head = N(archetype, "head", 2, -3);
            var tail = N("CriticalTailTapNote", "tail", 4, 3);
            Add(head, R("next", "tail"));
            Add(tail, R("prev", "head"), R("activeHead", "head"));
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                R("attachHead", "head"), R("attachTail", "tail"), R("activeHead", "head"));
            var json = Chart(tail, tick, head, Segment("head", "tail", "head", "tail", "head", "tail"));
            bool joint = archetype.EndsWith("TailReleaseNote", StringComparison.Ordinal);
            if (joint)
            {
                var first = N("NormalHeadTapNote", "first", 1, -3);
                Add(first, R("next", "head"));
                Add(head, R("prev", "first"), R("activeHead", "first"));
                ((JArray)json["entities"]).Add(first);
                ((JArray)json["entities"]).Add(Segment("first", "head", "first", "head", "first", "head"));
            }
            var chart = Load(json);
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            var release = chart.Score.NoteArray.OfType<NormalNote>().Single(note => note.IsIndependentRelease);
            var tap = chart.Score.NoteArray.OfType<NormalNote>().Single(note => !note.IsIndependentRelease);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(joint ? 4 : 3));
            Assert.That(bar.EndsWithoutJudgement, Is.True);
            Assert.That(tap.ParentNote, Is.Null);
            Assert.That(tap.Type, Is.EqualTo(NoteType.Critical));
            if (joint || archetype is "NormalReleaseNote" or "CriticalReleaseNote")
            {
                Assert.That(bar.UpdateConnectorHeadView(0.99), Is.False);
                Assert.That(bar.UpdateConnectorHeadView(1.5), Is.True);
                Assert.That(bar.ConnectorHeadViewNote, Is.TypeOf<NormalNote>());
                Assert.That(bar.ConnectorHeadViewNote.IsDecoration, Is.True);
                Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(5f));
                Assert.That(bar.UpdateConnectorHeadView(2), Is.False);
            }

            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var startLane = LiveConfig.JudgmentPositions[2];
                var middleLane = LiveConfig.JudgmentPositions[5];
                var endLane = LiveConfig.JudgmentPositions[8];
                if (joint) harness.Step(0.5f, 10.5, LiveHarness.Touch(1, startLane, 10.5));
                else harness.Step(0.95f, 10.95, LiveHarness.Touch(1, startLane, 10.95));
                Assert.That(release.Result, Is.EqualTo(NoteResult.None));
                harness.Step(1f, 11, LiveHarness.Touch(1, startLane, 11,
                    hitHead ? InputTouchPhase.Ended : InputTouchPhase.Canceled));
                harness.Step(1.4f, 11.4, LiveHarness.Touch(2, middleLane, 11.4));
                Assert.That(release.Result, Is.EqualTo(hitHead ? NoteResult.Perfect : NoteResult.Miss));
                harness.Step(1.5f, 11.5, LiveHarness.Touch(2, middleLane, 11.4, InputTouchPhase.Stationary));
                Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
                harness.Step(2f, 12, LiveHarness.Touch(2, endLane, 12, InputTouchPhase.Ended));
                Assert.That(tap.Result, Is.EqualTo(NoteResult.None));
                harness.Step(2.01f, 12.01, LiveHarness.Touch(3, endLane, 12.01));
                Assert.That(tap.Result, Is.EqualTo(NoteResult.Perfect));
                Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [TestCase("NormalTapNote", true)]
        [TestCase("CriticalTapNote", false)]
        [TestCase("NormalTailTapNote", true)]
        [TestCase("CriticalTailTapNote", false)]
        public void TapCanOwnAPlayableConnector(string archetype, bool press)
        {
            var tap = N(archetype, "tap", 2, -3);
            Add(tap, R("next", "tail"), V("style", 4));
            bool joinsSections = archetype.EndsWith("TailTapNote");
            var entities = new List<JObject> { N("NormalTailReleaseNote", "tail", 4, 3),
                Segment("tap", "tail", "tap", "tail", "tap", "tail"), tap,
                E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                    R("attachHead", "tap"), R("attachTail", "tail"), R("activeHead", "tap")) };
            if (joinsSections)
            {
                var first = N("NormalHeadTapNote", "first", 1, -3);
                Add(first, R("next", "tap"));
                Add(tap, R("prev", "first"), R("activeHead", "first"));
                entities.Add(first);
                entities.Add(Segment("first", "tap", "first", "tap", "first", "tap"));
            }
            var json = Chart(entities.ToArray());
            var chart = Load(json);
            var key = chart.Score.NoteArray.OfType<NormalNote>().Single();
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(joinsSections ? 4 : 3));
            Assert.That(key.Category, Is.EqualTo(joinsSections ? NoteCategory.Long : NoteCategory.Normal));
            Assert.That(key.TakesPress, Is.True);
            Assert.That(bar.UpdateConnectorHeadView(0.99), Is.False);
            Assert.That(bar.UpdateConnectorHeadView(1.5), Is.True);
            Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(5f));
            Assert.That(bar.ConnectorHeadViewNote.NativeStyle, Is.EqualTo(4));
            Assert.That(bar.UpdateConnectorHeadView(2), Is.False);
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var headLane = LiveConfig.NativeLanePosition(2.5f);
                var midLane = LiveConfig.NativeLanePosition(5.5f);
                float firstPress = joinsSections ? 0.5f : 0.7f;
                harness.Step(firstPress, 10 + firstPress, LiveHarness.Touch(1, headLane, 10 + firstPress));
                if (joinsSections)
                    Assert.That(chart.Score.NoteArray.Single(n => n.GetType() == typeof(LongNote)).Result,
                        Is.EqualTo(NoteResult.Perfect));
                var held = LiveHarness.Touch(1, headLane, 11, InputTouchPhase.Stationary);
                if (press) harness.Step(1f, 11, held, LiveHarness.Touch(2, headLane, 11));
                else harness.Step(1f, 11, held);
                Assert.That(bar.HasFinger, Is.EqualTo(press));
                harness.Step(1.43f, 11.43, LiveHarness.Touch(1, midLane, 11.43, InputTouchPhase.Moved));
                harness.Step(1.5f, 11.5, LiveHarness.Touch(1, midLane, 11.5, InputTouchPhase.Stationary));
                Assert.That(key.Result, Is.EqualTo(press ? NoteResult.Perfect : NoteResult.Miss));
                Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
                harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(8.5f), 12, InputTouchPhase.Ended));
                Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        private static JObject HoldWithInteriorKey(string archetype)
        {
            var start = N("NormalHeadTapNote", "start", 2, -3);
            var middle = N(archetype, "middle", 3, 2);
            var tail = N("NormalTailReleaseNote", "tail", 5, 2);
            Add(start, R("next", "middle"));
            Add(middle, R("next", "tail"), R("activeHead", "start"), V("connectorEase", 2));
            var tick = E("TransientHiddenTickNote", null, V("#BEAT", 4), V("isAttached", 1),
                R("attachHead", "middle"), R("attachTail", "tail"), R("activeHead", "start"));
            return Chart(middle, tail, tick, Segment("middle", "tail", "middle", "tail", "start", "tail"),
                start, Segment("start", "middle", "start", "middle", "start", "tail"));
        }

        [TestCase("CriticalHeadTapNote", typeof(NormalNote))]
        [TestCase("NormalHeadTraceNote", typeof(FrictionNote))]
        [TestCase("CriticalHeadFlickNote", typeof(FlickNote))]
        [TestCase("NormalHeadTraceFlickNote", typeof(FrictionFlickNote))]
        [TestCase("CriticalHeadReleaseNote", typeof(NormalNote))]
        [TestCase("CriticalTailTapNote", typeof(NormalNote))]
        [TestCase("NormalTailTraceNote", typeof(FrictionNote))]
        [TestCase("CriticalTailFlickNote", typeof(FrictionFlickNote))]
        [TestCase("NormalTailTraceFlickNote", typeof(FrictionFlickNote))]
        [TestCase("CriticalTailReleaseNote", typeof(NormalNote))]
        public void InteriorKeyShapesOneContinuousHold(string archetype, Type keyType)
        {
            var chart = Load(HoldWithInteriorKey(archetype));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var key = chart.Score.NoteArray.Single(n => n != bar);
            var middle = (NoteBase)bar.LineNoteList[1];
            Assert.That(key.GetType(), Is.EqualTo(keyType));
            Assert.That(key.Type, Is.EqualTo(archetype.StartsWith("Critical") ? NoteType.Critical : NoteType.Default));
            Assert.That(key.ParentNote, Is.Null);
            Assert.That(bar.LineNoteList.Count, Is.EqualTo(3));
            Assert.That(middle.NativeLeft, Is.EqualTo(key.NativeLeft));
            Assert.That(middle.NativeConnector.Ease, Is.EqualTo(2));
            Assert.That(middle.HasJudgment, Is.False);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(4));
            AssertCompletesAutoplay(chart);
            Assert.That(key.Result, Is.EqualTo(NoteResult.Auto));
        }

        [TestCase("NormalHeadTapNote", false)]
        [TestCase("NormalHeadTapNote", true)]
        [TestCase("CriticalTailTapNote", false)]
        [TestCase("CriticalTailTapNote", true)]
        public void InteriorTapNeedsAFreshPressWhileTheHoldContinues(string archetype, bool tapMiddle)
        {
            var chart = Load(HoldWithInteriorKey(archetype));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var key = chart.Score.NoteArray.OfType<NormalNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var middle = LiveConfig.JudgmentPositions[7];
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[2], 11));
            var held = LiveHarness.Touch(1, middle, 11, InputTouchPhase.Moved);
            if (tapMiddle) harness.Step(1.5f, 11.5, held, LiveHarness.Touch(2, middle, 11.5));
            else harness.Step(1.5f, 11.5, held);
            harness.Step(1.95f, 11.95, LiveHarness.Touch(1, middle, 11, InputTouchPhase.Stationary));
            harness.Step(2f, 12, LiveHarness.Touch(1, middle, 11, InputTouchPhase.Stationary));
            Assert.That(key.Result, Is.EqualTo(tapMiddle ? NoteResult.Perfect : NoteResult.Miss));
            Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
            harness.Step(2.5f, 12.5, LiveHarness.Touch(1, middle, 12.5, InputTouchPhase.Ended));
            Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase("NormalTailFlickNote")]
        [TestCase("CriticalTailTraceFlickNote")]
        [TestCase("CriticalTailReleaseNote")]
        [TestCase("NormalTailTraceNote")]
        public void HeldFingerCanPlayTheInteriorTailAndContinueToTheEnd(string archetype)
        {
            var chart = Load(HoldWithInteriorKey(archetype));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            var key = chart.Score.NoteArray.Single(n => n != bar);
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.JudgmentPositions[7];
            harness.Step(1f, 11, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[2], 11));
            harness.Step(1.45f, 11.45, LiveHarness.Touch(1, lane, 11.45, InputTouchPhase.Moved));
            bool release = key is NormalNote;
            if (key is FrictionFlickNote)
                harness.Step(1.5f, 11.5, new LiveTouchSample(1, new Vector2(800, 660), lane, 11.5,
                    InputTouchPhase.Moved, new Vector2(0, 60)));
            else
                harness.Step(1.5f, 11.5, LiveHarness.Touch(1, lane, 11.5,
                    release ? InputTouchPhase.Ended : InputTouchPhase.Stationary));
            if (key is FrictionFlickNote { Category: NoteCategory.FrictionFlick })
                harness.Step(1.51f, 11.51, LiveHarness.Touch(1, lane, 11.51, InputTouchPhase.Stationary));
            Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
            int finger = release ? 2 : 1;
            harness.Step(1.93f, 11.93, LiveHarness.Touch(finger, lane, 11.93,
                release ? InputTouchPhase.Began : InputTouchPhase.Stationary));
            harness.Step(2f, 12, LiveHarness.Touch(finger, lane, 12, InputTouchPhase.Stationary));
            Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
            harness.Step(2.5f, 12.5, LiveHarness.Touch(finger, lane, 12.5, InputTouchPhase.Ended));
            Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [Test]
        public void TapTailOnCrossChainGuideIsOneIndependentPressKey()
        {
            var head = N("AnchorNote", "head", 2, -3);
            var tail = N("NormalTailTapNote", "tail", 4, 3);
            var chart = Load(Chart(head, tail, Segment("head", "tail", "head", "tail")));
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            var key = chart.Score.NoteArray.OfType<NormalNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            Assert.That(bar.TailNote.HasJudgment, Is.False);
            Assert.That(key.ParentNote, Is.Null);
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.JudgmentPositions[8];
            harness.Step(2f, 12, LiveHarness.Touch(1, lane, 12));
            Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
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
                Assert.That(bars[0].Head.Result, Is.EqualTo(NoteResult.Perfect));
                Assert.That(bars[0].TailNote.Result, Is.EqualTo(NoteResult.Perfect));
                harness.Step(2.5f, 12.5, LiveHarness.Touch(2, right, 12.5));
                Assert.That(bars[2].Head.Result, Is.EqualTo(NoteResult.None));
                harness.Step(3, 13, LiveHarness.Touch(3, right, 13));
                harness.Step(3.5f, 13.5, LiveHarness.Touch(3, right, 13.5, InputTouchPhase.Stationary));
                Assert.That(bars[2].Head.Result, Is.EqualTo(NoteResult.Perfect));
                Assert.That(bars[2].JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
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
                Assert.That(hold.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
                harness.Step(3, 13, LiveHarness.Touch(1, endLane, 13, InputTouchPhase.Ended));
                Assert.That(hold.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [TestCase("NormalHeadTraceNote", -0.04f, NoteResult.Perfect)]
        [TestCase("CriticalHeadTraceNote", 0.06f, NoteResult.Perfect)]
        [TestCase("NormalHeadTraceNote", -0.10f, NoteResult.Miss)]
        [TestCase("CriticalHeadTraceNote", 0.10f, NoteResult.Miss)]
        [TestCase("NormalTraceNote", -0.04f, NoteResult.Perfect)]
        [TestCase("CriticalTraceNote", 0f, NoteResult.Perfect)]
        [TestCase("NormalTraceNote", 0.06f, NoteResult.Perfect)]
        [TestCase("CriticalTraceNote", 0.10f, NoteResult.Miss)]
        [TestCase("NormalTailTraceNote", -0.10f, NoteResult.Perfect)]
        [TestCase("CriticalTailTraceNote", 0.12f, NoteResult.Perfect)]
        [TestCase("NormalTailTraceNote", 0.14f, NoteResult.Miss)]
        public void TraceHeadUsesChartTimeAndScoresOnceWithItsMovingHold(string archetype, float offset, NoteResult expected)
        {
            var head = N(archetype, "head", 2, 3);
            var tail = N("NormalTailReleaseNote", "tail", 4, -3);
            Add(head, R("next", "tail"), R("#TIMESCALE_GROUP", "stopped"));
            Add(tail, R("activeHead", "head"));
            var json = Chart(head, tail, Segment("head", "tail", "head", "tail", "head", "tail"),
                E("TransientHiddenTickNote", null, V("#BEAT", 3), V("isAttached", 1),
                    R("attachHead", "head"), R("attachTail", "tail")),
                E("#TIMESCALE_GROUP", "stopped", R("first", "stop")),
                E("#TIMESCALE_CHANGE", "stop", V("#BEAT", 0), V("#TIMESCALE", 0), R("next", "move")),
                E("#TIMESCALE_CHANGE", "move", V("#BEAT", 2.5), V("#TIMESCALE", 1)));
            bool joinsSections = archetype.EndsWith("TailTraceNote", StringComparison.Ordinal);
            if (joinsSections)
            {
                var first = N("NormalHeadTapNote", "first", 1, -5);
                Add(first, R("next", "head"));
                Add(head, R("prev", "first"), R("activeHead", "first"));
                ((JArray)json["entities"]).Add(first);
                ((JArray)json["entities"]).Add(Segment("first", "head", "first", "head", "first", "head"));
            }
            var chart = Load(json);
            var key = chart.Score.NoteArray.OfType<FrictionNote>().Single();
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(key.Type, Is.EqualTo(archetype.StartsWith("Critical", StringComparison.Ordinal)
                ? NoteType.Critical : NoteType.Default));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(joinsSections ? 4 : 3));

            if (joinsSections || archetype is "NormalTraceNote" or "CriticalTraceNote")
            {
                Assert.That(bar.UpdateConnectorHeadView(0.99), Is.False);
                Assert.That(bar.UpdateConnectorHeadView(1.5), Is.True);
                Assert.That(bar.ConnectorHeadViewNote, Is.TypeOf<FrictionNote>());
                Assert.That(bar.ConnectorHeadViewNote.IsDecoration, Is.True);
                Assert.That(bar.ConnectorHeadViewNote.LaneStartF, Is.EqualTo(5f));
                Assert.That(bar.UpdateConnectorHeadView(2), Is.False);
            }
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var lane = LiveConfig.JudgmentPositions[8];
                double startTime = joinsSections ? 10.5 : 10.8;
                harness.Step(joinsSections ? 0.5f : 0.8f, startTime, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[0], startTime));
                harness.Step(1f + offset, 11d + offset, LiveHarness.Touch(1, lane, startTime, InputTouchPhase.Moved));
                if (offset < 0)
                {
                    Assert.That(key.Result, Is.EqualTo(NoteResult.None));
                    harness.Step(1.01f, 11.01);
                }
                if (joinsSections) harness.Step(1.16f, 11.16);
                else harness.Step(1.12f, 11.12);
                Assert.That(key.Result, Is.EqualTo(expected));
                harness.Step(1.43f, 11.43, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[5], 11.43));
                harness.Step(1.5f, 11.5, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[5], 11.43, InputTouchPhase.Stationary));
                Assert.That(bar.JudgedPoints.Single().Result, Is.EqualTo(NoteResult.Perfect));
                harness.Step(2f, 12, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[2], 12, InputTouchPhase.Ended));
                Assert.That(bar.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }

            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void UnattachedHiddenTickUsesItsAuthoredLaneAndClearsContactOnRetry()
        {
            var chart = Load(Chart(N("TransientHiddenTickNote", "hidden", 2, 4),
                E("TransientHiddenTickNote", "center", V("#BEAT", 4), V("isAttached", 0))));
            var ticks = chart.Score.NoteArray.Cast<NextRushHiddenTickNote>().ToArray();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            Assert.That(ticks.All(t => !t.IsVisible && t.HasJudgment), Is.True);
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Step(0.96f, 10.96, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[9], 10.96, InputTouchPhase.Stationary));
            harness.Step(1.01f, 11.01);
            Assert.That(ticks[0].Result, Is.EqualTo(NoteResult.Perfect));
            harness.Step(2.01f, 12.01, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[1], 12.01));
            harness.Step(2.12f, 12.12);
            Assert.That(ticks[1].Result, Is.EqualTo(NoteResult.Miss));
            harness.Logic.Result = 0;
            chart.Score.ResetNote();
            harness.Logic.Notes.Reset();
            harness.Logic.RefreshInput();
            harness.Step(1.12f, 21.12);
            Assert.That(ticks[0].Result, Is.EqualTo(NoteResult.Miss));
            AssertCompletesAutoplay(Load(Chart(N("TransientHiddenTickNote", "hidden", 2, 4))));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnattachedHiddenTickOnConnectorChecksContactAtItsOwnLane(bool moveToTick)
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var tick = N("TransientHiddenTickNote", "hidden", 2, 4);
            var tail = N("NormalTailReleaseNote", "tail", 4, 4);
            Add(head, R("next", "hidden"));
            Add(tick, R("next", "tail"), R("prev", "head"), R("activeHead", "head"));
            Add(tail, R("activeHead", "head"));
            var json = Chart(tick, tail, head, Segment("head", "hidden", "head", "tail", "head", "tail"),
                Segment("hidden", "tail", "head", "tail", "head", "tail"));
            var chart = Load(json);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            Assert.That(chart.Score.NoteArray.OfType<LongNote>().Single().LineNoteList.Count, Is.EqualTo(3));
            var hidden = chart.Score.NoteArray.OfType<NextRushHiddenTickNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Step(0.5f, 10.5, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[1], 10.5));
            harness.Step(0.96f, 10.96, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[9], 10.96, InputTouchPhase.Moved));
            harness.Step(1.01f, 11.01, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[moveToTick ? 9 : 1], 11.01, InputTouchPhase.Moved));
            harness.Step(1.03f, 11.03);
            Assert.That(hidden.Result, Is.EqualTo(moveToTick ? NoteResult.Perfect : NoteResult.Miss));
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void StandaloneTicksScoreOnContactAndClearEarlyContactOnRetry()
        {
            var chart = Load(Chart(N("NormalTickNote", null, 2), N("CriticalTickNote", null, 4)));
            var keys = chart.Score.NoteArray.OfType<ConnectionNote>().ToArray();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.JudgmentPositions[5];
            harness.Step(0.95f, 10.95, LiveHarness.Touch(1, lane, 10.95, InputTouchPhase.Stationary));
            Assert.That(keys[0].Result, Is.EqualTo(NoteResult.None));
            harness.Step(1.01f, 11.01);
            harness.Step(1.03f, 11.03);
            Assert.That(keys[0].Result, Is.EqualTo(NoteResult.Perfect));
            harness.Step(1.95f, 11.95, LiveHarness.Touch(2, lane, 11.95, InputTouchPhase.Stationary));
            Assert.That(keys[1].Result, Is.EqualTo(NoteResult.None));
            chart.Score.ResetNote();
            harness.Logic.Notes.Reset();
            harness.Logic.RefreshInput();
            harness.Step(2.10f, 22.10);
            Assert.That(keys.Select(key => key.Result), Is.EqualTo(new[] { NoteResult.Miss, NoteResult.Miss }));
        }

        [Test]
        public void EarlyReleaseTailStillSettlesAPendingWindowedTick()
        {
            var head = N("NormalHeadTapNote", "head", 2, 3);
            var tickNode = N("NormalTickNote", "tick", 3.9, -3);
            var tail = N("NormalTailReleaseNote", "tail", 4, 3);
            Add(head, R("next", "tick"));
            Add(tickNode, R("next", "tail"));
            Add(tail, R("activeHead", "head"));
            var score = Load(Chart(head, tickNode, tail,
                Segment("head", "tick", "head", "tail", "head", "tail"),
                Segment("tick", "tail", "head", "tail", "head", "tail"))).Score;
            var bar = score.NoteArray.OfType<LongNote>().Single();
            var tick = score.NoteArray.SelectMany(n => n.NoteList).OfType<ConnectionNote>().Single();
            var tailLane = LiveConfig.NativeLanePosition(8.5f);
            using var harness = new LiveHarness();
            harness.Arrange(score);
            harness.Step(1f, 11, LiveHarness.Touch(1, tailLane, 11));
            Assert.That(tick.Result, Is.EqualTo(NoteResult.None));

            // The lift is outside the tick's lanes and precedes its target time.
            harness.Step(1.91f, 11.91, LiveHarness.Touch(1, tailLane, 11.91, InputTouchPhase.Ended));
            Assert.That(bar.TailNote.Result, Is.GreaterThanOrEqualTo(NoteResult.Bad));
            Assert.That(tick.Result, Is.EqualTo(NoteResult.Perfect));
        }

        [TestCase(false, 93.340, NoteResult.Perfect)]
        [TestCase(false, 93.390, NoteResult.Miss)]
        [TestCase(true, 93.340, NoteResult.Miss)]
        public void SameBeatHeadAndTickUseTheTicksOwnInputWindow(bool hasActiveHead, double pressTime,
            NoteResult expectedTickResult)
        {
            // Life Reset Button, beat 311: the left head and tick overlap while the right finger flicks.
            var head = N("NormalHeadTapNote", "head", 311, -3, 3);
            var tick = N("NormalTickNote", "tick", 311, -2, 4);
            var anchor = N("AnchorNote", "anchor", 333, -2, 1);
            var flick = N("NormalFlickNote", null, 311, 3, 3);
            Add(head, R("next", "tick"));
            Add(tick, R("next", "anchor"), V("connectorEase", 3));
            Add(anchor, R("activeHead", "head"));
            if (hasActiveHead) Add(tick, R("activeHead", "head"));
            var chart = Chart(head, tick, anchor, flick,
                Segment("head", "tick", "head", "anchor", "head", "anchor"),
                Segment("tick", "anchor", "head", "anchor", "head", "anchor"));
            chart["entities"][1]["data"][1]["value"] = 200;
            var score = Load(chart).Score;
            var notes = score.NoteArray.SelectMany(n => n.NoteList).ToArray();
            ReplayLifeResetButtonHeadAndTick(score, notes.OfType<LongNote>().Single(n => n.PublishesHeadMiss),
                notes.OfType<ConnectionNote>().Single(), pressTime, expectedTickResult);
        }

        [Test]
        public void LifeResetButtonDeviceTraceReplaysOnTheOriginalChartWhenProvided()
        {
            string path = Environment.GetEnvironmentVariable("OPENSEKAI_LIFE_RESET_CHART");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Provide OPENSEKAI_LIFE_RESET_CHART to replay the original chart.");
            var score = NextRushChartLoader.Load(NextRushChartLoader.ReadFile(path), _config).Score;
            var notes = score.NoteArray.SelectMany(n => n.NoteList).ToArray();
            ReplayLifeResetButtonHeadAndTick(score, (LongNote)notes.Single(n => n.Id == 6055),
                (ConnectionNote)notes.Single(n => n.Id == 6056), 93.340, NoteResult.Perfect);
        }

        private static void ReplayLifeResetButtonHeadAndTick(MusicScore score, LongNote head, ConnectionNote tick,
            double pressTime, NoteResult expectedTickResult)
        {
            // Android LiveTrace touch timestamps 2839.095 and 2839.102 map to 93.333 and 93.340.
            const double clockOrigin = 2745.762;
            const int flickTouch = 6413;
            const int headTouch = 6414;
            var flickLane = LiveConfig.NativeLanePosition(9.70f);
            var headLane = LiveConfig.NativeLanePosition(2.40f);
            using var harness = new LiveHarness();
            harness.Arrange(score, score.NoteArray.Where(n => n.MusicScoreInfo.time >= 93.300f).ToArray());
            harness.Step(92.790f, clockOrigin + 92.790);
            harness.Step(93.300f, clockOrigin + 93.300);
            harness.Step(93.317f, clockOrigin + 93.317);
            harness.Step(93.335f, clockOrigin + 93.335,
                LiveHarness.Touch(flickTouch, flickLane, 2839.095));
            double frame = pressTime + 0.007;
            harness.Step((float)frame, clockOrigin + frame,
                LiveHarness.Touch(flickTouch, flickLane, 2839.095, InputTouchPhase.Stationary),
                LiveHarness.Touch(headTouch, headLane, clockOrigin + pressTime));
            if (pressTime == 93.340) Assert.That(head.Result, Is.EqualTo(NoteResult.Perfect));
            Assert.That(tick.Result, Is.EqualTo(expectedTickResult));
        }

        [Test]
        public void OffstageGuideKeepsItsAlphaAndBorrowsAnIndependentTraceWithoutScoringItTwice()
        {
            var head = N("AnchorNote", "head", 2, -14);
            var trace = N("NormalTraceNote", "trace", 3);
            var tail = N("AnchorNote", "tail", 4, 14);
            Add(head, R("next", "trace"), V("segmentKind", 103), V("segmentAlpha", 2));
            Add(trace, R("next", "tail"));
            Add(tail, V("segmentAlpha", 0));
            var chart = Load(Chart(head, trace, tail, Segment("head", "trace", "head", "tail"),
                Segment("trace", "tail", "head", "tail")));
            var guide = chart.Score.NoteArray.OfType<GuideNote>().Single();
            var key = chart.Score.NoteArray.OfType<FrictionNote>().Single();
            var guideMiddle = guide.NoteList.Single(note => note.MusicScoreInfo.time == key.MusicScoreInfo.time);
            Assert.That(guide.LaneStartF, Is.LessThan(0));
            Assert.That(guide.TailNote.LaneEndF, Is.GreaterThan(11));
            Assert.That(guideMiddle.LaneStartF, Is.EqualTo(key.LaneStartF));
            Assert.That(guideMiddle.HasJudgment, Is.False);
            Assert.That(guide.NativeConnector.StartAlpha, Is.EqualTo(2));
            Assert.That(guide.NativeConnector.EndAlpha, Is.EqualTo(1));
            Assert.That(guideMiddle.NativeConnector.StartAlpha, Is.EqualTo(1));
            Assert.That(guideMiddle.NativeConnector.EndAlpha, Is.Zero);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
        }

        [Test]
        public void ActiveIntervalCanEndWithAGuideGap()
        {
            var head = N("NormalHeadTapNote", "head", 1);
            var anchor = N("AnchorNote", "anchor", 2);
            var tail = N("AnchorNote", "tail", 3);
            Add(head, R("next", "anchor"));
            Add(anchor, R("next", "tail"));
            var chart = Load(Chart(head, anchor, tail,
                Segment("head", "anchor", "head", "anchor", "head", "tail"),
                Segment("anchor", "tail", "anchor", "tail")));
            Assert.That(chart.Score.NoteArray.OfType<GuideNote>().Count(), Is.EqualTo(1));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(1));
            AssertCompletesAutoplay(chart);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ForkAndMergeKeepTheSharedKeysAndArmTicksDistinct(bool reverseOrder)
        {
            var head = N("NormalHeadTapNote", "h", 1);
            var left = N("NormalTickNote", "l", 3, -3);
            var right = N("NormalTickNote", "r", 3, 3);
            var tail = N("NormalTailReleaseNote", "t", 5);
            Add(head, R("next", "l"));
            Add(left, R("next", "t"), R("activeHead", "h"));
            Add(right, R("activeHead", "h"));
            Add(tail, R("activeHead", "h"));
            JObject Edge(string a, string b) => Segment(a, b, a, b, "h", "t");
            var hidden = E("TransientHiddenTickNote", null, V("#BEAT", 2), V("isAttached", 1),
                R("attachHead", "h"), R("attachTail", "r"), R("activeHead", "h"));
            var entities = new[] { head, left, right, tail, hidden, Edge("h", "l"), Edge("l", "t"), Edge("h", "r"), Edge("r", "t") };
            var json = Chart(reverseOrder ? entities.Reverse().ToArray() : entities);
            var chart = Load(json);
            var arms = chart.Score.NoteArray.OfType<LongNote>().ToArray();
            Assert.That(arms.Length, Is.EqualTo(4));
            Assert.That(arms.All(arm => arm.LineNoteList.Count == 2), Is.True);
            var tickArm = arms.Single(arm => arm.JudgedPoints.Count > 0);
            Assert.That(tickArm.ChildNote.LaneStartF, Is.EqualTo(8));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(5));
            AssertCompletesAutoplay(chart);

            var score = Load(json).Score;
            using var harness = new LiveHarness();
            harness.Arrange(score);
            for (int frame = 29; frame <= 155; frame++)
            {
                float time = frame / 60f;
                double inputTime = 10d + time;
                if (frame < 30 || frame > 150) { harness.Step(time, inputTime); continue; }
                float lane = 5.5f + 3f * (1f - Mathf.Abs(time - 1.5f));
                var phase = frame == 30 ? InputTouchPhase.Began : frame == 150 ? InputTouchPhase.Ended : InputTouchPhase.Moved;
                var rightTouch = LiveHarness.Touch(1, LiveConfig.NativeLanePosition(lane), inputTime, phase);
                if (reverseOrder && frame > 30 && frame < 150)
                {
                    var leftPhase = frame == 31 ? InputTouchPhase.Began : InputTouchPhase.Moved;
                    var leftTouch = LiveHarness.Touch(2, LiveConfig.NativeLanePosition(11f - lane), inputTime, leftPhase);
                    harness.Step(time, inputTime, rightTouch, leftTouch);
                }
                else harness.Step(time, inputTime, rightTouch);
            }
            Assert.That(score.NoteArray.OfType<NormalNote>().Select(n => n.Result),
                Is.All.EqualTo(NoteResult.Perfect), "shared head and tail");
            var ticks = score.NoteArray.OfType<ConnectionNote>().OrderBy(n => n.LaneStartF).ToArray();
            Assert.That(ticks.Select(n => n.Result),
                Is.EqualTo(new[] { reverseOrder ? NoteResult.Perfect : NoteResult.Miss, NoteResult.Perfect }));
            Assert.That(score.NoteArray.OfType<LongNote>().SelectMany(n => n.JudgedPoints).Single().Result,
                Is.EqualTo(NoteResult.Perfect), "hidden tick on the held arm");
        }

        [Test]
        public void ActiveEndpointsCanSitOutsideTheDrawnEdges()
        {
            var head = N("NormalHeadTapNote", "h", 1);
            var tail = N("NormalTailReleaseNote", "t", 5);
            Add(tail, R("activeHead", "h"));
            var chart = Load(Chart(head, N("AnchorNote", "a", 2), N("AnchorNote", "b", 4), tail,
                Segment("a", "b", "a", "b", "h", "t")));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            Assert.That(bar.MusicScoreInfo.time, Is.EqualTo(1f));
            Assert.That(bar.ChildNote.MusicScoreInfo.time, Is.EqualTo(2f));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            AssertCompletesAutoplay(chart);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SamePointTicksUseTheAuthoredArmAtAJunction(bool merge, bool reverseOrder)
        {
            double beat = merge ? 3 : 1;
            var joint = N("AnchorNote", "j", beat);
            var left = N("AnchorNote", "l", merge ? 1 : 3, -3);
            var right = N("AnchorNote", "r", merge ? 1 : 3, 3);
            Add(joint, R(merge ? "prev" : "next", "r"));
            JObject Arm(string arm) => merge
                ? Segment(arm, "j", arm, "j", arm, "j")
                : Segment("j", arm, "j", arm, "j", arm);
            JObject Tick(string kind) => E(kind, null, V("#BEAT", beat), V("isAttached", 1),
                R("attachHead", "j"), R("attachTail", "j"));
            var entities = new[] { joint, left, right, Arm("l"), Arm("r"),
                Tick("NormalTickNote"), Tick("TransientHiddenTickNote") };
            var json = Chart(reverseOrder ? entities.Reverse().ToArray() : entities);
            var chart = Load(json);
            var arm = chart.Score.NoteArray.OfType<LongNote>().Single(n => n.JudgedPoints.Any());
            Assert.That(merge ? arm.LaneStartF : arm.ChildNote.LaneStartF, Is.EqualTo(8));
            Assert.That(arm.JudgedPoints.Count, Is.EqualTo(merge ? 1 : 2));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            AssertCompletesAutoplay(chart);

            chart = Load(json);
            arm = chart.Score.NoteArray.OfType<LongNote>().Single(n => n.JudgedPoints.Any());
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            for (int frame = 29; frame <= 100; frame++)
            {
                float time = frame / 60f;
                if (reverseOrder || frame < 30) { harness.Step(time, 10d + time); continue; }
                float progress = Mathf.Clamp01(time - 0.5f);
                float lane = 5.5f + 3f * (merge ? 1f - progress : progress);
                harness.Step(time, 10d + time, LiveHarness.Touch(1, LiveConfig.NativeLanePosition(lane),
                    10d + time, frame == 30 ? InputTouchPhase.Began : InputTouchPhase.Moved));
            }
            Assert.That(arm.JudgedPoints.Concat(chart.Score.NoteArray.OfType<ConnectionNote>()).Select(n => n.Result),
                Is.All.EqualTo(reverseOrder ? NoteResult.Miss : NoteResult.Perfect));
        }

        [Test]
        public void PlayableForkRejectsASameBeatCycle()
        {
            var json = Chart(N("NormalHeadTapNote", "h", 1), N("AnchorNote", "a", 2), N("AnchorNote", "b", 2),
                N("NormalTailReleaseNote", "t", 3), Segment("h", "a", "h", "a", "h", "t"),
                Segment("a", "b", "a", "b", "h", "t"), Segment("b", "a", "b", "a", "h", "t"),
                Segment("b", "t", "b", "t", "h", "t"));
            Assert.That(() => Load(json), Throws.TypeOf<InvalidDataException>().With.Message.Contains("循环"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DisconnectedSectionsNeverCreateABodyOrAnExtraKeyInTheGap(bool sharedInterval)
        {
            var head = N("NormalHeadTapNote", "h", 1);
            var a = N("AnchorNote", "a", 2, -3);
            var b = N("AnchorNote", "b", 4, 3);
            var tail = N("NormalTailReleaseNote", "t", 5);
            Add(head, R("next", "a"));
            Add(a, R("next", "b"));
            Add(b, R("next", "t"));
            var chart = Load(Chart(head, a, b, tail, Segment("h", "a", "h", "a", "h", sharedInterval ? "t" : "a"),
                Segment("b", "t", "b", "t", sharedInterval ? "h" : "b", "t")));
            var arms = chart.Score.NoteArray.OfType<LongNote>().ToArray();
            Assert.That(arms.Select(arm => arm.MusicScoreInfo.time), Is.EqualTo(new[] { 0.5f, 2f }));
            Assert.That(arms.Select(arm => arm.ChildNote.MusicScoreInfo.time), Is.EqualTo(new[] { 1f, 2.5f }));
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            AssertCompletesAutoplay(chart);
        }

        [Test]
        public void LaterEdgeCanBePickedUpAtItsStartAfterMissingTheTraceOwner()
        {
            var head = N("NormalTraceNote", "head", 1, -4);
            var firstEnd = N("AnchorNote", "firstEnd", 2, -4);
            var nextStart = N("AnchorNote", "nextStart", 4, 3);
            Add(head, R("next", "firstEnd"));
            Add(firstEnd, R("next", "nextStart"));
            Add(nextStart, R("next", "tail"));
            var chart = Load(Chart(head, firstEnd, nextStart, N("AnchorNote", "tail", 5, 3),
                Segment("head", "firstEnd", "head", "firstEnd", "head", "tail"),
                Segment("nextStart", "tail", "nextStart", "tail", "head", "tail")));
            var second = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single(n => n.MusicScoreInfo.time == 2f);
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Step(0.65f, 10.65);
            harness.Step(2f, 12, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[8], 12));
            Assert.That(chart.Score.NoteArray.OfType<FrictionNote>().Single().Result, Is.EqualTo(NoteResult.Miss));
            Assert.That(second.HasFinger, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(true, false, true)]
        [TestCase(false, false, true)]
        [TestCase(true, true, true)]
        [TestCase(false, true, true)]
        public void ShortReleaseOwnedHoldRequiresSeparateHeadAndTailLifts(bool liftHead, bool fork, bool tailOwner = false)
        {
            var head = N(tailOwner ? "CriticalTailReleaseNote" : "NormalReleaseNote", "head", 2, -3);
            var tail = N("NormalTailReleaseNote", "tail", 2.12, tailOwner ? -3 : 3);
            Add(head, R("next", "tail"));
            Add(tail, R("activeHead", "head"));
            var json = Chart(tail, head, Segment("head", "tail", "head", "tail", "head", "tail"));
            if (fork)
            {
                ((JArray)json["entities"]).Add(N("AnchorNote", "arm", 2.1));
                ((JArray)json["entities"]).Add(Segment("head", "arm", "head", "arm", "head", "tail"));
            }
            var chart = Load(json);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            var key = chart.Score.NoteArray.OfType<NormalNote>().Single(n => n.MusicScoreInfo.time == 1f);
            var end = fork ? chart.Score.NoteArray.OfType<NormalNote>().Single(n => n != key)
                : chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single().TailNote;
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var left = LiveConfig.JudgmentPositions[2];
            var right = tailOwner ? left : LiveConfig.JudgmentPositions[8];
            harness.Step(.8f, 10.8, LiveHarness.Touch(1, left, 10.8), LiveHarness.Touch(2, right, 10.8));
            harness.Step(1f, 11, LiveHarness.Touch(1, left, 11, liftHead ? InputTouchPhase.Ended : InputTouchPhase.Stationary));
            Assert.That(key.Result, Is.EqualTo(liftHead ? NoteResult.Perfect : NoteResult.None));
            Assert.That(end.Result, Is.EqualTo(NoteResult.None));
            harness.Step(1.06f, 11.06, LiveHarness.Touch(2, right, 11.06, InputTouchPhase.Ended));
            Assert.That(end.Result, Is.EqualTo(liftHead ? NoteResult.Perfect : NoteResult.None));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShortTraceOwnedHoldChecksTheTraceBeforeAcceptingARelease(bool touchTrace)
        {
            var trace = N("NormalTraceNote", "trace", 2, -3);
            Add(trace, R("next", "tail"));
            var chart = Load(Chart(trace, N("NormalTailReleaseNote", "tail", 2.1, 3),
                Segment("trace", "tail", "trace", "tail", "trace", "tail")));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            var lane = LiveConfig.JudgmentPositions[8];
            harness.Step(0.9f, 10.9, LiveHarness.Touch(1, lane, 10.9));
            if (touchTrace) harness.Step(1f, 11, LiveHarness.Touch(2, LiveConfig.JudgmentPositions[2], 11));
            harness.Step(1.05f, 11.05, LiveHarness.Touch(1, lane, 11.05, InputTouchPhase.Ended));
            var bar = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            Assert.That(bar.TailNote.Result, Is.EqualTo(touchTrace ? NoteResult.Perfect : NoteResult.None));
        }

        [Test]
        public void TickAtTheEndOfAHoldSettlesOnceWithoutAReleaseTail()
        {
            var head = N("NormalHeadTapNote", "head", 2);
            var tick = N("CriticalTickNote", "tick", 4);
            var end = N("AnchorNote", "end", 6);
            Add(head, R("next", "tick"));
            Add(tick, R("next", "end"), R("activeHead", "head"), V("segmentKind", 101));
            var json = Chart(tick, end, head, Segment("head", "tick", "head", "tick", "head", "tick"),
                Segment("tick", "end", "tick", "end"));
            foreach (bool touchedAtTick in new[] { true, false })
            {
                var chart = Load(json);
                var bar = chart.Score.NoteArray.OfType<LongNote>().Single(n => n.HasJudgment);
                var endpoint = chart.Score.NoteArray.OfType<ConnectionNote>().Single();
                Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
                Assert.That(bar.TailNote.HasJudgment, Is.False);
                Assert.That(bar.JudgedPoints, Is.Empty);
                Assert.That(endpoint.ParentNote, Is.Null);
                Assert.That(endpoint.Type, Is.EqualTo(NoteType.Critical));

                using var harness = new LiveHarness();
                harness.Arrange(chart.Score);
                var lane = LiveConfig.JudgmentPositions[5];
                harness.Step(1f, 11, LiveHarness.Touch(1, lane, 11));
                if (touchedAtTick)
                    harness.Step(2.01f, 12.01, LiveHarness.Touch(1, lane, 12.01, InputTouchPhase.Stationary));
                else
                    harness.Step(2.01f, 12.01);
                harness.Step(2.04f, 12.04);
                Assert.That(endpoint.Result, Is.EqualTo(touchedAtTick ? NoteResult.Perfect : NoteResult.Miss));
                Assert.That(bar.State, Is.EqualTo(NoteState.Done));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void TickAtTheStartOfAnActiveSectionScoresOnceAndAllowsItsReleaseTail()
        {
            var guide = N("AnchorNote", "guide", 2, -3);
            var tick = N("NormalTickNote", "tick", 4, 3);
            var end = N("NormalTailReleaseNote", "end", 6, 3);
            Add(guide, R("next", "tick"));
            Add(tick, R("next", "end"), R("activeHead", "tick"), V("segmentKind", 1));
            Add(end, R("activeHead", "tick"));
            var json = Chart(tick, guide, end, Segment("guide", "tick", "guide", "tick"),
                Segment("tick", "end", "tick", "end", "tick", "end"));
            var chart = Load(json);
            var hold = chart.Score.NoteArray.OfType<FrictionHideLongNote>().Single();
            var key = chart.Score.NoteArray.OfType<ConnectionNote>().Single();
            Assert.That(hold.HasJudgment, Is.False);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                var lane = LiveConfig.JudgmentPositions[8];
                harness.Step(2, 12, LiveHarness.Touch(1, lane, 12));
                harness.Step(2.5f, 12.5, LiveHarness.Touch(1, lane, 12.5, InputTouchPhase.Stationary));
                harness.Step(3, 13, LiveHarness.Touch(1, lane, 13, InputTouchPhase.Ended));
                Assert.That(key.Result, Is.EqualTo(NoteResult.Perfect));
                Assert.That(hold.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(Load(json));
        }

        [Test]
        public void CrossChainGuideBorrowsTwoExistingNodesWithoutLinkingTheirJudgments()
        {
            var a = N("NormalHeadTapNote", "a", 1, -3);
            var middle = N("AnchorNote", "middle", 2, -2);
            var aEnd = N("AnchorNote", "aEnd", 3, -1);
            var b = N("CriticalHeadTapNote", "b", 2.5, 2);
            var bEnd = N("CriticalTailReleaseNote", "bEnd", 5, 3);
            Add(a, R("next", "middle"));
            Add(middle, R("next", "aEnd"), V("segmentKind", 104));
            Add(b, R("next", "bEnd"));
            var chart = Load(Chart(aEnd, b, middle, bEnd, a,
                Segment("a", "middle", "a", "aEnd", "a", "aEnd"),
                Segment("middle", "aEnd", "a", "aEnd", "a", "aEnd"),
                Segment("b", "bEnd", "b", "bEnd", "b", "bEnd"),
                Segment("middle", "bEnd", "middle", "bEnd")));
            var holds = chart.Score.NoteArray.OfType<LongNote>().Where(n => n.HasJudgment).ToArray();
            var guide = chart.Score.NoteArray.OfType<GuideNote>().Single();
            Assert.That(holds[0].LineNoteList, Has.Count.EqualTo(3));
            Assert.That(holds[1].LineNoteList, Has.Count.EqualTo(2));
            Assert.That(guide.NativeConnector.Kind, Is.EqualTo(104));
            Assert.That(guide.NativeLeft, Is.EqualTo(((NoteBase)holds[0].LineNoteList[1]).NativeLeft));
            Assert.That(guide.TailNote.NativeLeft, Is.EqualTo(holds[1].TailNote.NativeLeft));
            Assert.That(guide.NoteList.All(note => !note.HasJudgment), Is.True);
            Assert.That(chart.TotalNoteCount, Is.EqualTo(3));
            AssertCompletesAutoplay(chart);
            Assert.That(holds[1].TailNote.Result, Is.EqualTo(NoteResult.Auto));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CrossChainGeometryRejectsDuplicateEdgesAndSameBeatCycles(bool cycle)
        {
            var a = N("AnchorNote", "a", 2, -1);
            var b = N("AnchorNote", "b", 2, 1);
            Assert.That(() => Load(Chart(a, b, Segment("a", "b", "a", "b"),
                cycle ? Segment("b", "a", "b", "a") : Segment("a", "b", "a", "b"))),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains(cycle ? "循环" : "重复"));
        }

        [Test]
        public void ActiveConnectorCanJoinTwoRawChainsIntoOneContinuousHold()
        {
            var head = N("NormalHeadTapNote", "head", 1, -4);
            var left = N("AnchorNote", "left", 2, -3);
            var right = N("AnchorNote", "right", 4, 3);
            var end = N("NormalTailReleaseNote", "end", 5, 4);
            Add(head, R("next", "left"));
            Add(right, R("next", "end"), V("connectorEase", 2));
            Add(end, R("prev", "right"), R("activeHead", "head"));
            var json = Chart(head, left, right, end,
                Segment("head", "left", "head", "left", "head", "end"),
                Segment("left", "right", "left", "right", "head", "end"),
                Segment("right", "end", "right", "end", "head", "end"));
            var chart = Load(json);
            var hold = chart.Score.NoteArray.OfType<LongNote>().Single();
            Assert.That(chart.TotalNoteCount, Is.EqualTo(2));
            Assert.That(hold.LineNoteList, Has.Count.EqualTo(4));
            using (var harness = new LiveHarness())
            {
                harness.Arrange(chart.Score);
                harness.Step(0.5f, 10.5, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[1], 10.5));
                harness.Step(1.5f, 11.5, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[5], 11.5, InputTouchPhase.Moved));
                Assert.That(hold.LaneStartF, Is.EqualTo(5).Within(1e-5));
                Assert.That(hold.HasFinger, Is.True);
                harness.Step(2.5f, 12.5, LiveHarness.Touch(1, LiveConfig.JudgmentPositions[9], 12.5, InputTouchPhase.Ended));
                Assert.That(hold.TailNote.Result, Is.EqualTo(NoteResult.Perfect));
            }
            AssertCompletesAutoplay(Load(json));
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
        public void LegacyPopWaitsForStyleHeadEvenWhenBothGeometryEndpointsAreVisible(bool legacy)
        {
            var style = N("AnchorNote", "style", 6);
            Add(style, V("segmentKind", 103), R("#TIMESCALE_GROUP", "fast"));
            var head = N("AnchorNote", "head", 7);
            var tail = N("AnchorNote", "tail", 8);
            Add(head, R("next", "tail"), R("#TIMESCALE_GROUP", "slow"));
            Add(tail, R("#TIMESCALE_GROUP", "slow"));
            var edge = Segment("head", "tail", "style", "tail");
            Add(edge, V("legacyHiddenPop", legacy ? 1 : 0));
            var chart = Load(Chart(style, head, tail, edge,
                E("#TIMESCALE_GROUP", "fast", V("forceNoteSpeed", 12)),
                E("#TIMESCALE_GROUP", "slow", V("forceNoteSpeed", 1))));
            var bar = chart.Score.NoteArray.OfType<GuideNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("legacy pop boundary");
            float oldLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<NoteLineView>();
                view.Setup(_config, "traceLine_eff.png", 1, guide: true);
                view.Add(bar);
                view.Excute(1.97f);
                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertices.Any(v => v != Vector3.zero), Is.EqualTo(!legacy));
                view.Excute(1.973f);
                Assert.That(mesh.vertices.Any(v => v != Vector3.zero), Is.True);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = oldLead;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LegacyPopFindsFirstWindowBeforeAnEasedScrollReversal()
        {
            var timeline = new NextRushScrollTimeline(new[] {
                new NextRushScrollTimeline.Change(-2, 2, 0, 1, true, false),
                new NextRushScrollTimeline.Change(2, -2, 0, 0, true, false)
            }, 0);
            // Distance is -t * (2 - t). It first falls to 1 at 1 - sqrt(2),
            // leaves the visible range after zero and returns at the target.
            double spawn = timeline.LegacyConnectorSpawnTime(timeline.Sample(2), 1);
            Assert.That(spawn, Is.InRange(1 - Math.Sqrt(2) - 0.22, 1 - Math.Sqrt(2)));
        }

        [Test]
        public void LegacyPopSkipsAnInvisibleStoppedIntervalAndChecksTheMarkerBoundary()
        {
            var timeline = new NextRushScrollTimeline(new[] {
                new NextRushScrollTimeline.Change(-2, 0, 0, 0, false, false),
                new NextRushScrollTimeline.Change(2, 1, 10, 0, false, false)
            }, 0);
            Assert.That(timeline.LegacyConnectorSpawnTime(timeline.Sample(2), 1), Is.EqualTo(1.99).Within(1e-6));
            Assert.That(timeline.LegacyConnectorSpawnTime(timeline.Sample(-2), 1), Is.EqualTo(-2));
            Assert.That(timeline.LegacyConnectorSpawnTime(timeline.Sample(-3), 1), Is.EqualTo(double.PositiveInfinity));
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
        public void NativeGuideFillsItsWidthWithSolidColorAndUsesOnlyAuthoredAlpha()
        {
            var start = N("AnchorNote", "start", 2, size: 2);
            var solidEnd = N("AnchorNote", "solidEnd", 3, size: 2);
            var fadeEnd = N("AnchorNote", "fadeEnd", 4, size: 2);
            Add(start, R("next", "solidEnd"), V("segmentKind", 102));
            Add(solidEnd, R("next", "fadeEnd"), V("segmentKind", 102));
            Add(fadeEnd, V("segmentAlpha", 0));
            var chart = Load(Chart(start, solidEnd, fadeEnd, Segment("start", "solidEnd", "start", "solidEnd"),
                Segment("solidEnd", "fadeEnd", "solidEnd", "fadeEnd")));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("solid native Guide");
            var cameraHost = new GameObject("solid Guide test camera");
            float originalViewLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<NoteLineView>();
                view.Setup(_config, "traceLine_eff.png", 1, guide: true);
                view.Add(chart.Score.NoteArray.OfType<GuideNote>().Single());
                view.Excute(1f);
                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                var renderer = host.GetComponent<MeshRenderer>();
                Assert.That(renderer.enabled, Is.True);
                Assert.That(mesh.uv.All(uv => uv.x == -1), Is.True);
                Assert.That(mesh.colors.Min(color => color.a), Is.Zero);
                Assert.That(mesh.colors.Max(color => color.a), Is.EqualTo(1));
                Assert.That(mesh.vertices[2], Is.EqualTo(mesh.vertices[4]));
                Assert.That(mesh.vertices[3], Is.EqualTo(mesh.vertices[5]));
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

                var vertices = mesh.vertices;
                var inner = (vertices[0] + vertices[7]) * 0.5f;
                Vector3 across = (vertices[6] - vertices[0]) * 0.25f;
                var camera = cameraHost.AddComponent<Camera>();
                camera.orthographic = true;
                var bounds = renderer.bounds;
                camera.transform.position = bounds.center + new Vector3(0, 0, -5);
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.1f;
                var pixels = ReadGuidePixels(camera, renderer, inner - across, inner, inner + across);
                foreach (Color pixel in pixels)
                    AssertPixelColor(pixel, new Color32(214, 115, 123, 255));
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = originalViewLead;
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(cameraHost);
            }
        }

        [TestCase(12, IndexFormat.UInt16)]
        [TestCase(90, IndexFormat.UInt32)]
        public void DenseNativeGuidesKeepAllSegmentsInTheirLayer(int guideCount, IndexFormat indexFormat)
        {
            var entities = Enumerable.Range(0, guideCount).SelectMany(i =>
            {
                string startName = "start" + i;
                string endName = "end" + i;
                var start = N("AnchorNote", startName, 2, lane: i == guideCount - 1 ? 3 : -3);
                var end = N("AnchorNote", endName, 4);
                Add(start, R("next", endName), V("segmentKind", i == guideCount - 1 ? 102 : 101));
                return new[] { start, end, Segment(startName, endName, startName, endName) };
            });
            var chart = Load(Chart(entities.ToArray()));
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score);
            harness.Logic.Notes.BindNativeVisuals(1);
            var host = new GameObject("dense native Guides");
            float originalViewLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0;
                var view = host.AddComponent<NoteLineView>();
                view.Setup(_config, "traceLine_eff.png", 1, guide: true);
                foreach (var guide in chart.Score.NoteArray.OfType<GuideNote>()) view.Add(guide);
                view.Excute(1f);

                var mesh = host.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.EqualTo(guideCount * 100 * 8));
                Assert.That(mesh.indexFormat, Is.EqualTo(indexFormat));
                Assert.That(mesh.GetIndexCount(0), Is.EqualTo(guideCount * 100 * 18));
                Assert.That(mesh.GetIndices(0).Max(), Is.EqualTo(mesh.vertexCount - 1));
                Assert.That(mesh.colors32.Last(), Is.EqualTo(new Color32(214, 115, 123, 255)));

                view.Excute(1.9f);
                Assert.That(mesh.vertexCount, Is.InRange(guideCount * 8, guideCount * 100 * 8 - 1));
                Assert.That(mesh.vertexCount % (guideCount * 8), Is.Zero);
                view.Excute(2.1f);
                Assert.That(host.GetComponent<MeshRenderer>().enabled, Is.False);
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = originalViewLead;
                UnityEngine.Object.DestroyImmediate(host);
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
            Add(style, V("segmentKind", 106), V("segmentLayer", 3), V("segmentPresentation", 1),
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
                Assert.That(mesh.colors.All(color => Mathf.Abs(color.r - 214f / 255) < 0.00001f
                    && Mathf.Abs(color.g - 115f / 255) < 0.00001f
                    && Mathf.Abs(color.b - 205f / 255) < 0.00001f), Is.True);
                Assert.That(mesh.uv.All(uv => uv.x == 2), Is.True);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    Color pixel = ReadGuidePixels(camera, renderer, camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 5f)))[0];
                    // The authored swatch is half opaque at this frame and blends over black.
                    AssertPixelColor(pixel, new Color(107f / 255, 57.5f / 255, 102.5f / 255));
                }
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

        private static Color[] ReadGuidePixels(Camera camera, Renderer guide, params Vector3[] worldPositions)
        {
            const int testLayer = 31;
            const int size = 128;
            guide.gameObject.layer = testLayer;
            camera.cullingMask = 1 << testLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                return worldPositions.Select(position =>
                {
                    var viewport = camera.WorldToViewportPoint(position);
                    return pixels.GetPixel((int)(viewport.x * size), (int)(viewport.y * size));
                }).ToArray();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void AssertPixelColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(2f / 255));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(2f / 255));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(2f / 255));
        }

        [Test]
        public void NativeHeadDrawsAtItsStartingLaneOnRetryBeforeTheJudgmentScan()
        {
            var head = N("NormalHeadTapNote", "head", 4, lane: -0.25, size: 2.5);
            var anchor = N("AnchorNote", "anchor", 5, lane: 4);
            var tail = N("NormalTailReleaseNote", "tail", 6, lane: 3);
            Add(head, R("next", "anchor"), R("#TIMESCALE_GROUP", "group"));
            Add(anchor, R("next", "tail"), R("#TIMESCALE_GROUP", "group"));
            Add(tail, R("activeHead", "head"), R("#TIMESCALE_GROUP", "group"));
            var chart = Load(Chart(head, anchor, tail,
                Segment("head", "anchor", "head", "tail", "head", "tail"),
                Segment("anchor", "tail", "head", "tail", "head", "tail"),
                E("#TIMESCALE_GROUP", "group", R("first", "stop")),
                E("#TIMESCALE_CHANGE", "stop", V("#BEAT", 2), V("#TIMESCALE", 0), R("next", "resume")),
                E("#TIMESCALE_CHANGE", "resume", V("#BEAT", 3.5), V("#TIMESCALE", 1))));
            var bar = chart.Score.NoteArray.OfType<LongNote>().Single();
            using var harness = new LiveHarness();
            harness.Arrange(chart.Score, displayTimeOffset: 0.5f, auto: true);
            harness.Logic.Notes.BindNativeVisuals(0.5);
            var host = new GameObject("native long head retry view");
            var markerHost = new GameObject("moving connector head");
            float originalViewLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.ViewLeadSeconds = 0f;
                var view = host.AddComponent<LongNoteView>();
                view.Setup(LiveConfig.SpawnPosition, LiveConfig.JudgmentPositions, 1);
                var moving = markerHost.AddComponent<LongNoteView>();
                moving.Setup(LiveConfig.SpawnPosition, LiveConfig.JudgmentPositions, 1);
                const float firstVisible = 0.8f;
                harness.OnUpdate(firstVisible, 10d + firstVisible);
                view.Spawn(bar, 0);
                view.Move(firstVisible);
                Assert.That(bar.State, Is.EqualTo(NoteState.Wait));
                Assert.That(host.transform.localScale.x, Is.GreaterThan(0));
                Vector3 firstPosition = host.transform.localPosition;

                harness.OnUpdate(1.6f, 11.6);
                harness.OnUpdate(2f, 12);
                harness.OnUpdate(2.5f, 12.5);
                view.Move(2.5f);
                Assert.That(host.transform.localScale.x, Is.Zero);
                Assert.That(bar.UpdateConnectorHeadView(2.5), Is.True);
                moving.Spawn(bar.ConnectorHeadViewNote, 0);
                moving.Move(2.5f);
                Assert.That(markerHost.transform.localPosition.x, Is.GreaterThan(firstPosition.x + 1));

                view.Unspawn();
                harness.Logic.Session.Reset(new LivePresentationBridge());
                harness.OnUpdate(firstVisible, 20d + firstVisible);
                view.Spawn(bar, 0);
                view.Move(firstVisible);
                Assert.That(bar.State, Is.EqualTo(NoteState.Wait));
                Assert.That(host.transform.localScale.x, Is.GreaterThan(0));
                Assert.That(host.transform.localPosition, Is.EqualTo(firstPosition));
                Assert.That(bar.LaneEndF - bar.LaneStartF, Is.EqualTo(bar.NativeRight - bar.NativeLeft));
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = originalViewLead;
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(markerHost);
            }
        }

        [Test]
        public void MovingHeadUsesTheCurrentSegmentWhenItsVisibilityChanges()
        {
            var head = N("NormalHeadTapNote", "head", 1);
            var anchor = N("AnchorNote", "anchor", 2);
            var tail = N("NormalTailReleaseNote", "tail", 4);
            Add(head, R("next", "anchor"), V("style", 4));
            Add(anchor, R("next", "tail"), R("#TIMESCALE_GROUP", "anchorGroup"), V("style", 2), V("segmentKind", 2));
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
                Assert.That(host.transform.localScale.x, Is.Zero);
                Assert.That(bar.UpdateConnectorHeadView(0.8), Is.True);
                Assert.That(bar.ConnectorHeadViewNote.Type, Is.EqualTo(NoteType.Default));
                view.Move(1.1f);
                Assert.That(host.transform.localScale.x, Is.Zero);
                Assert.That(bar.UpdateConnectorHeadView(1.1), Is.True);
                Assert.That(bar.ConnectorHeadViewNote.Type, Is.EqualTo(NoteType.Critical));
                Assert.That(bar.ConnectorHeadViewNote.NativeStyle, Is.EqualTo(4));
                view.Spawn(bar.ConnectorHeadViewNote, 0);
                view.Move(1.1f);
                Assert.That(host.transform.localScale.x, Is.EqualTo(1f));
                Assert.That(bar.UpdateConnectorHeadView(1.3), Is.False);
                Assert.That(bar.UpdateConnectorHeadView(1.6), Is.True);
                view.Move(1.6f);
                Assert.That(host.transform.localScale.x, Is.EqualTo(1f));
                Assert.That(bar.UpdateConnectorHeadView(2), Is.False);
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
                Assert.That(note.Result, Is.EqualTo(NoteResult.Perfect));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(click);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NegativeBpmMarkersAnchorTheTimelineAtBeatZero(bool explicitZero)
        {
            var json = Chart(E("#BPM_CHANGE", null, V("#BEAT", -2), V("#BPM", 60)),
                E("#BPM_CHANGE", null, V("#BEAT", -4), V("#BPM", 240)),
                N("NormalTapNote", null, -5), N("NormalTapNote", null, -2),
                N("NormalTapNote", null, 0), N("NormalTapNote", null, 1));
            if (!explicitZero) json["entities"][1].Remove();
            var score = Load(json).Score;
            Assert.That(score.NoteArray.Select(n => n.MusicScoreInfo.time),
                Is.EqualTo(new[] { -2.75f, -2f, 0f, explicitZero ? 0.5f : 1f }));
            score.GetMusicScoreInfoFromTime(-2.25f, out var frame);
            Assert.That(frame.bpm, Is.EqualTo(240));
            Assert.That(frame.bar + frame.barProgress, Is.EqualTo(-0.75f));
        }

        [Test]
        public void FirstPositiveBpmExtendsAcrossZeroUntilTheNextChange()
        {
            var json = Chart(E("#BPM_CHANGE", null, V("#BEAT", 8), V("#BPM", 240)),
                E("#BPM_CHANGE", null, V("#BEAT", 4), V("#BPM", 90)),
                N("NormalTapNote", null, -3), N("NormalTapNote", null, 0),
                N("NormalTapNote", null, 3), N("NormalTapNote", null, 4),
                N("NormalTapNote", null, 8), N("NormalTapNote", null, 10));
            json["entities"][1].Remove();
            var score = Load(json).Score;
            Assert.That(score.NoteArray.Select(n => n.MusicScoreInfo.time),
                Is.EqualTo(new[] { -2f, 0f, 2f, 8f / 3, 16f / 3, 16f / 3 + 0.5f }).Within(1e-6));
            score.GetMusicScoreInfoFromTime(-2, out var frame);
            Assert.That(frame.bpm, Is.EqualTo(90));
            Assert.That(frame.bar + frame.barProgress, Is.EqualTo(-0.75f));
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
