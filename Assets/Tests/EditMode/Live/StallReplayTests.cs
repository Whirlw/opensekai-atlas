using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenSekai.Shared.Diagnostics;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// Replays one real device run (DEINOS PHAINEIN Master, OnePlus PLQ110, 165 Hz) through the live
    /// logic, once with the device's own frames and again with main-thread stalls injected. A stall drops
    /// the frames inside it; their touches reach the first frame after it the way EnhancedTouch delivers
    /// them: one sample per touch, Began before a same-update Ended, moves folded into the latest record.
    /// The judgment clock reads event timestamps, so a stall inside the supported lateness should change
    /// nothing a player can see.
    /// </summary>
    public sealed class StallReplayTests
    {
        private const string FIXTURE = "deinos-phainein-device";
        private const float ORTHOGRAPHIC_SIZE = 5f;

        private sealed class Sample
        {
            internal int Touch, Finger;
            internal InputTouchPhase Phase;
            internal double Time;
            internal Vector2 Position, Delta;
        }

        private sealed class Frame
        {
            internal double Game;
            internal float Logic;
            internal float Delta;
            internal readonly List<Sample> Touches = new();
        }

        private sealed class Replay
        {
            internal readonly List<Frame> Frames = new();
            internal readonly Dictionary<int, NoteResult> DeviceResults = new();
            internal readonly Dictionary<int, double> DevicePressErrors = new();
            internal Vector2 Screen;
        }

        internal sealed class Outcome
        {
            internal readonly Dictionary<int, (NoteResult Result, NoteResultDescription Description, float At, string Kind)> Judged = new();
            internal readonly Dictionary<int, double> PressErrors = new();
            internal readonly Dictionary<int, int> PressTouches = new();
            internal LiveScore Score;
        }

        private sealed class JudgmentRecorder : LiveViewBase
        {
            internal readonly Outcome Outcome = new();
            internal float Now;

            public override void JudgmentNote(NoteBase note)
            {
                if (!Outcome.Judged.ContainsKey(note.Id))
                    Outcome.Judged[note.Id] = (note.Result, note.Description, Now, note.GetType().Name);
            }
        }

        private static Replay _replay;
        private static readonly Dictionary<bool, Outcome> _baselines = new();

        private static Outcome Baseline(Replay replay, bool frameMatching)
        {
            if (!_baselines.TryGetValue(frameMatching, out Outcome outcome))
                _baselines[frameMatching] = outcome = Run(replay, replay.Frames, frameMatching);
            return outcome;
        }

        private static Replay Load()
        {
            if (_replay != null)
                return _replay;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures", FIXTURE, "replay.tsv.gz"));
            var replay = new Replay();
            CultureInfo c = CultureInfo.InvariantCulture;
            using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress), Encoding.UTF8);
            string header = reader.ReadLine();
            string[] size = header.Split(' ').First(part => part.StartsWith("screen=", StringComparison.Ordinal)).Substring(7).Split('x');
            replay.Screen = new Vector2(float.Parse(size[0], c), float.Parse(size[1], c));
            Frame frame = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] f = line.Split('\t');
                switch (f[0])
                {
                    case "F":
                        frame = new Frame { Game = double.Parse(f[1], c), Logic = float.Parse(f[2], c), Delta = float.Parse(f[3], c) };
                        replay.Frames.Add(frame);
                        break;
                    case "T":
                        frame.Touches.Add(new Sample
                        {
                            Touch = int.Parse(f[1], c), Finger = int.Parse(f[2], c),
                            Phase = Enum.Parse<InputTouchPhase>(f[3]), Time = double.Parse(f[4], c),
                            Position = new Vector2(float.Parse(f[5], c), float.Parse(f[6], c)),
                            Delta = new Vector2(float.Parse(f[7], c), float.Parse(f[8], c)),
                        });
                        break;
                    case "J":
                        // The original device capture uses the old name for centered hits.
                        replay.DeviceResults[int.Parse(f[1], c)] = f[2] == "JustPerfect"
                            ? NoteResult.Perfect
                            : Enum.Parse<NoteResult>(f[2]);
                        break;
                    case "P":
                        replay.DevicePressErrors[int.Parse(f[1], c)] = double.Parse(f[3], c);
                        break;
                }
            }

            return _replay = replay;
        }

        /// <summary>Drops every frame that starts inside a stall and hands its touches to the next frame.</summary>
        private static List<Frame> InjectStalls(List<Frame> frames, double stallSeconds, double periodSeconds, double phaseSeconds)
        {
            var result = new List<Frame>(frames.Count);
            var pending = new List<Sample>();
            var deferred = new List<Sample>();
            double origin = frames[0].Game + phaseSeconds;
            double previousGame = frames[0].Game;
            foreach (Frame frame in frames)
            {
                double sincePeriod = frame.Game - origin;
                bool stalled = sincePeriod > 0 && sincePeriod % periodSeconds < stallSeconds && frame != frames[0];
                if (stalled)
                {
                    pending.AddRange(frame.Touches);
                    continue;
                }

                pending.AddRange(frame.Touches);
                var delivered = new Frame { Game = frame.Game, Logic = frame.Logic, Delta = (float)(frame.Game - previousGame) };
                previousGame = frame.Game;
                delivered.Touches.AddRange(deferred);
                deferred.Clear();
                Collapse(pending, delivered.Touches, deferred);
                pending.Clear();
                result.Add(delivered);
            }

            return result;
        }

        private static void Collapse(List<Sample> batch, List<Sample> deliver, List<Sample> nextFrame)
        {
            foreach (IGrouping<int, Sample> touch in batch.GroupBy(s => s.Touch))
            {
                Sample[] samples = touch.ToArray();
                if (samples.Length == 1)
                {
                    deliver.Add(samples[0]);
                    continue;
                }

                Sample began = samples.FirstOrDefault(s => s.Phase == InputTouchPhase.Began);
                Sample last = samples[samples.Length - 1];
                Vector2 delta = samples.Where(s => s.Phase != InputTouchPhase.Began).Aggregate(Vector2.zero, (sum, s) => sum + s.Delta);
                if (began != null)
                {
                    deliver.Add(began);
                    if (last != began)
                        nextFrame.Add(Clone(last, delta));
                }
                else
                {
                    deliver.Add(Clone(last, delta));
                }
            }
        }

        private static Sample Clone(Sample s, Vector2 delta) => new Sample
        {
            Touch = s.Touch, Finger = s.Finger, Phase = s.Phase, Time = s.Time, Position = s.Position, Delta = delta,
        };

        private static Outcome Run(Replay replay, List<Frame> frames, bool frameMatching)
        {
            MusicScore score = AutoplayCompletionTests.LoadFixture(FIXTURE);
            var bundle = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
            bool testMode = LiveTrace.Enabled;
            LiveTraceCategory categories = LiveTrace.Categories;
            bool captureEnabled = LatencyCapture.Enabled;
            bool mix = LatencyCapture.MixProbeEnabled;
            string marker = "stall-replay-" + Guid.NewGuid().ToString("N");
            string logs = Path.Combine(Application.persistentDataPath, "logs");
            Task export = Task.CompletedTask;
            var recorder = new GameObject("stall-replay-recorder").AddComponent<JudgmentRecorder>();
            try
            {
                using var harness = new LiveHarness(bundle);
                LiveConfig.UseFramePressMatching = frameMatching;
                var scoreLogic = new ScoreLogic(bundle);
                harness.Arrange(score);
                harness.Logic.scoreLogic = scoreLogic;
                scoreLogic.Setup(null, score);
                harness.BindViews(recorder);

                LiveTrace.Categories = LiveTraceCategory.None;
                LiveTrace.Enabled = true;
                LatencyCapture.Enabled = true;
                LatencyCapture.MixProbeEnabled = false;
                LatencyCapture.Begin(marker, "master", false, 0f, 0f, 0f, false);
                float worldHeight = ORTHOGRAPHIC_SIZE * 2f;
                float worldWidth = worldHeight * replay.Screen.x / replay.Screen.y;
                var touches = new List<LiveTouchSample>();
                foreach (Frame frame in frames)
                {
                    touches.Clear();
                    foreach (Sample s in frame.Touches)
                    {
                        var world = new Vector3((s.Position.x / replay.Screen.x - 0.5f) * worldWidth,
                            (s.Position.y / replay.Screen.y - 0.5f) * worldHeight, 0f);
                        touches.Add(new LiveTouchSample(s.Touch, s.Position, world, s.Time, s.Phase, s.Delta, s.Finger));
                    }

                    LiveUtility.FrameDeltaTimeOverride = frame.Delta;
                    recorder.Now = frame.Logic;
                    harness.Step(frame.Logic, frame.Game, touches.ToArray());
                }

                LatencyCapture.Scan((stage, noteId, touch, value, result) =>
                {
                    if (stage == "press_spent" && result != NoteResult.None && !recorder.Outcome.PressErrors.ContainsKey(noteId))
                    {
                        recorder.Outcome.PressErrors[noteId] = value;
                        recorder.Outcome.PressTouches[noteId] = touch;
                    }
                });
                recorder.Outcome.Score = scoreLogic.Score;
            }
            finally
            {
                LiveUtility.FrameDeltaTimeOverride = float.NaN;
                UnityEngine.Object.DestroyImmediate(recorder.gameObject);
                export = LatencyCapture.End("stall_replay");
                export.GetAwaiter().GetResult();
                LatencyCapture.MixProbeEnabled = mix;
                LatencyCapture.Enabled = captureEnabled;
                LiveTrace.Enabled = testMode;
                LiveTrace.Categories = categories;
                if (Directory.Exists(logs))
                {
                    foreach (string file in Directory.GetFiles(logs, "latency-*.tsv"))
                    {
                        if (File.ReadLines(file).FirstOrDefault()?.Contains($" chart={marker} ") == true)
                            File.Delete(file);
                    }
                }
            }

            return recorder.Outcome;
        }

        private static string Compare(Outcome baseline, Outcome run, out int resultChanges, out int errorChanges)
        {
            var report = new StringBuilder();
            resultChanges = 0;
            errorChanges = 0;
            foreach (int id in baseline.Judged.Keys.Union(run.Judged.Keys).OrderBy(id => id))
            {
                bool inBase = baseline.Judged.TryGetValue(id, out var b);
                bool inRun = run.Judged.TryGetValue(id, out var r);
                if (!inBase || !inRun || b.Result != r.Result || b.Description != r.Description)
                {
                    resultChanges++;
                    if (resultChanges <= 30)
                        report.AppendLine(FormattableString.Invariant(
                            $"    note {id} {(inBase ? b.Kind : r.Kind)} t={(inBase ? b.At : r.At):F3}: {(inBase ? $"{b.Result}/{b.Description}" : "-")} -> {(inRun ? $"{r.Result}/{r.Description}" : "-")}"));
                }
            }

            foreach (int id in baseline.PressErrors.Keys.Union(run.PressErrors.Keys).OrderBy(id => id))
            {
                bool inBase = baseline.PressErrors.TryGetValue(id, out double b);
                bool inRun = run.PressErrors.TryGetValue(id, out double r);
                if (!inBase || !inRun || Math.Abs(b - r) > 0.01)
                {
                    errorChanges++;
                    if (errorChanges <= 6)
                        report.AppendLine(FormattableString.Invariant(
                            $"    press {id}: {(inBase ? b.ToString("F2") : "-")} -> {(inRun ? r.ToString("F2") : "-")} ms"));
                }
            }

            return report.ToString();
        }

        private static string Summary(Outcome o) => FormattableString.Invariant(
            $"score={o.Score.totalScore} maxCombo={o.Score.maxCombo} P={o.Score.perfectCount} Gr={o.Score.greatCount} Go={o.Score.goodCount} Bad={o.Score.badCount} Miss={o.Score.missCount} judged={o.Judged.Count} presses={o.PressErrors.Count}");

        [Test]
        public void TheDeviceRunReplaysToTheDevicesJudgments()
        {
            // The capture's build matched a frame's presses as one chord; replay it the same way.
            Replay replay = Load();
            Outcome baseline = Baseline(replay, frameMatching: true);
            var both = replay.DeviceResults.Where(pair => baseline.Judged.ContainsKey(pair.Key)).ToArray();
            var mismatched = both.Where(pair => baseline.Judged[pair.Key].Result != pair.Value).ToArray();
            string differing = string.Join("\n", mismatched.Take(15)
                .Select(pair => FormattableString.Invariant($"    note {pair.Key} {baseline.Judged[pair.Key].Kind}: device {pair.Value}, replay {baseline.Judged[pair.Key].Result}")));
            // A flick's swipe threshold is Screen.dpi-based and the capture predates the dpi header field, so
            // flicks are compared loosely. Any other difference must be a press the replay matches to 0.05 ms
            // that sits on a grade edge.
            var taps = mismatched.Where(pair => !(baseline.Judged[pair.Key].Kind is nameof(FlickNote) or nameof(FrictionFlickNote))).ToArray();
            int pressSame = replay.DevicePressErrors.Count(pair =>
                baseline.PressErrors.TryGetValue(pair.Key, out double e) && Math.Abs(e - pair.Value) < 0.05);
            TestContext.WriteLine($"[replay] {Summary(baseline)}");
            TestContext.WriteLine($"[replay] judged by both {both.Length}/{replay.DeviceResults.Count} (the rest are child notes the harness view is not bound to); press errors within 0.05 ms {pressSame}/{replay.DevicePressErrors.Count}");
            TestContext.WriteLine(differing);

            Assert.That(both.Length, Is.GreaterThan(1500), "the harness view must see the run's notes");
            Assert.That(mismatched.Length - taps.Length, Is.LessThanOrEqualTo(12), "flick differences beyond the dpi gap\n" + differing);
            Assert.That(taps.Length, Is.LessThanOrEqualTo(2), "a tap or hold must get the device's result\n" + differing);
            Assert.That(taps.All(pair => replay.DevicePressErrors.ContainsKey(pair.Key)), Is.True,
                "a tap judged differently must be a press on a grade edge\n" + differing);
            Assert.That(pressSame, Is.EqualTo(replay.DevicePressErrors.Count), "every press must land on the device's note at the device's error");
        }

        /// <summary>
        /// Up to 50 ms a stall may not cost a note, the combo, or a press its note. A late frame's presses
        /// are judged in time order (JudgmentScheduler press passes), so a finger that pressed again inside
        /// the stall, or a run of fingers, lands where it would have at full frame rate. What still moves is
        /// bounded: the controller hands the logic whole-millisecond music time, so a touch mapped on a
        /// later frame can move by up to 1 ms, and flicks finish on the frame or sample that sees them.
        /// </summary>
        [Test]
        public void StallsUpToFiftyMillisecondsCostNoNoteAndNoCombo([Values(20, 50)] int stallMs,
            [Values(0.0, 0.31, 0.62)] double phase, [Values(false, true)] bool frameMatching)
        {
            Replay replay = Load();
            Outcome baseline = Baseline(replay, frameMatching);
            List<Frame> stalled = InjectStalls(replay.Frames, stallMs / 1000.0, 1.0, phase);
            Outcome run = Run(replay, stalled, frameMatching);
            string report = Compare(baseline, run, out int resultChanges, out int errorChanges);
            int largestId = baseline.PressErrors.Keys.Intersect(run.PressErrors.Keys)
                .OrderByDescending(id => Math.Abs(baseline.PressErrors[id] - run.PressErrors[id])).First();
            double largestShift = Math.Abs(baseline.PressErrors[largestId] - run.PressErrors[largestId]);
            string largestKind = baseline.Judged.TryGetValue(largestId, out var largestNote) ? largestNote.Kind : "?";
            TestContext.WriteLine(FormattableString.Invariant(
                $"[stall {stallMs} ms every 1.0 s, phase {phase:F2} s, {(frameMatching ? "frame matching" : "serial")}] result changes {resultChanges}, press error changes {errorChanges}, largest {largestShift:F3} ms on {largestKind} {largestId} (touch {baseline.PressTouches[largestId]} -> {run.PressTouches[largestId]})"));
            TestContext.WriteLine($"  baseline {Summary(baseline)}");
            TestContext.WriteLine($"  stalled  {Summary(run)}");
            TestContext.Write(report);

            Assert.That(run.Score.missCount, Is.EqualTo(baseline.Score.missCount), report);
            Assert.That(run.Score.maxCombo, Is.EqualTo(baseline.Score.maxCombo), report);
            Assert.That(run.PressErrors.Keys, Is.EquivalentTo(baseline.PressErrors.Keys), "every press must stay on its note");
            // A press that stayed with its touch moves only by the whole-millisecond music clock. Serial
            // judging never lets a stall hand a note to another touch. Under frame matching two presses
            // closer than one pass are one moment and compete as a chord, so a note may change hands
            // between them, by no more than the pass width.
            foreach (int id in baseline.PressErrors.Keys)
            {
                double shift = Math.Abs(baseline.PressErrors[id] - run.PressErrors[id]);
                if (!frameMatching)
                    Assert.That(run.PressTouches[id], Is.EqualTo(baseline.PressTouches[id]), $"note {id} changed hands");
                double bound = baseline.PressTouches[id] == run.PressTouches[id]
                    ? 1.05
                    : JudgmentScheduler.PRESS_PASS_SECONDS * 1000.0 + 1.05;
                Assert.That(shift, Is.LessThan(bound), FormattableString.Invariant(
                    $"press on note {id} moved {shift:F3} ms (touch {baseline.PressTouches[id]} -> {run.PressTouches[id]})"));
            }
        }

        /// <summary>
        /// The full sweep behind the P2 stall report, 100 ms included. At 100 ms flicks whose swipe is folded
        /// into one sample can still miss, and a slide connection point can drop; it reports rather than
        /// asserts.
        /// </summary>
        [Test]
        [Explicit("characterisation sweep; reports the stall table")]
        public void StallSweep([Values(20, 50, 100)] int stallMs, [Values(0.0, 0.31, 0.62)] double phase,
            [Values(false, true)] bool frameMatching)
        {
            Replay replay = Load();
            Outcome baseline = Baseline(replay, frameMatching);
            List<Frame> stalled = InjectStalls(replay.Frames, stallMs / 1000.0, 1.0, phase);
            Outcome run = Run(replay, stalled, frameMatching);
            string report = Compare(baseline, run, out int resultChanges, out int errorChanges);
            TestContext.WriteLine(FormattableString.Invariant(
                $"[stall {stallMs} ms every 1.0 s, phase {phase:F2} s, {(frameMatching ? "frame matching" : "serial")}] frames {replay.Frames.Count} -> {stalled.Count}; result changes {resultChanges}, press error changes {errorChanges}"));
            TestContext.WriteLine($"  baseline {Summary(baseline)}");
            TestContext.WriteLine($"  stalled  {Summary(run)}");
            TestContext.Write(report);
        }

        /// <summary>
        /// What the time-ordered default changes on a real run against the old per-frame chord matching: the
        /// device run without stalls, judged both ways.
        /// </summary>
        [Test]
        public void SerialJudgingAgainstFrameMatchingOnTheDeviceRun()
        {
            Replay replay = Load();
            Outcome frame = Baseline(replay, frameMatching: true);
            Outcome serial = Baseline(replay, frameMatching: false);
            string report = Compare(frame, serial, out int resultChanges, out int errorChanges);
            int handsChanged = frame.PressTouches.Count(pair => serial.PressTouches.TryGetValue(pair.Key, out int t) && t != pair.Value);
            TestContext.WriteLine($"[serial vs frame matching] result changes {resultChanges}, press error changes {errorChanges}, notes pressed by another touch {handsChanged}");
            TestContext.WriteLine($"  frame  {Summary(frame)}");
            TestContext.WriteLine($"  serial {Summary(serial)}");
            TestContext.Write(report);

            Assert.That(serial.Score.missCount, Is.LessThanOrEqualTo(frame.Score.missCount), report);
            Assert.That(serial.Score.maxCombo, Is.GreaterThanOrEqualTo(frame.Score.maxCombo), report);
        }
    }
}
