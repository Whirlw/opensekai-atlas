using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sekai;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Shared.Diagnostics
{
    public static class LatencyCapture
    {
        private const string PREF_KEY = "diagnostics.latency_capture";
        private const string MIX_PREF_KEY = "diagnostics.audio_mix_probe";
        // A dense Master segment records about 0.8 rows per ms of play; keep a full song in memory.
        internal const int CAPACITY = 262144;
        // Android probe rows are appended at segment end and must not compete with gameplay rows.
        private const int PROBE_CAPACITY = AndroidTouchProbe.CAPACITY + 4096;
        private const int MIX_CAPACITY = 2048;

        private struct Sample
        {
            public string Stage, Detail;
            public double Time, Source, Song, Note, Value;
            public double X, Y, AndroidEvent, AndroidReceive;
            public int Frame, Touch, Finger, NoteId;
            public int RequestSequence;
            public TouchPhase Phase;
            public NoteResult Result;
        }

        private static Sample[] _samples;
        private static Sample[] _spareSamples;
        private static int _count;
        private static int _overflow;
        private static Sample[] _probeSamples;
        private static int _probeCount;
        private static int _probeOverflow;
        private static string _metadata;
        private static string _lastRouteReport;
        private static AudioMixProbe.Ticket[] _mixTickets;
        private static int _mixCount;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PREF_KEY, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(PREF_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool MixProbeEnabled
        {
            get => PlayerPrefs.GetInt(MIX_PREF_KEY, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(MIX_PREF_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        internal static bool Active { get; private set; }

        internal static void Begin(string chart, string difficulty, bool autoplay,
            float audioLatencyMs, float visualLatencyMs, float judgeOffsetMs, bool legacy)
        {
            if (Active || !LiveTrace.Enabled || !Enabled)
                return;

            _samples = Interlocked.Exchange(ref _spareSamples, null) ?? new Sample[CAPACITY];
            _count = 0;
            _overflow = 0;
            _probeSamples = new Sample[PROBE_CAPACITY];
            _probeCount = 0;
            _probeOverflow = 0;
            bool probe = MixProbeEnabled;
            if (probe)
            {
                // Each ticket belongs to one capture. An in-flight callback keeps its old ticket.
                _mixTickets = new AudioMixProbe.Ticket[MIX_CAPACITY];
                for (int i = 0; i < _mixTickets.Length; i++)
                    _mixTickets[i] = new AudioMixProbe.Ticket();
            }
            AudioSettings.GetDSPBufferSize(out int block, out int blocks);
            _metadata = FormattableString.Invariant(
                $"# build={LiveTrace.BuildStamp} version={Application.version} device={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} chart={chart} difficulty={difficulty} autoplay={autoplay} target_fps={Application.targetFrameRate} reported_refresh_hz={Screen.currentResolution.refreshRateRatio.value:R} resolution={Screen.width}x{Screen.height} audio_L_ms={audioLatencyMs:R} visual_V_ms={visualLatencyMs:R} judge_J_ms={judgeOffsetMs:R} legacy={legacy} input_update={InputSystem.settings.updateMode} merge_disabled={InputSystem.settings.disableRedundantEventsMerging} sample_rate={AudioSettings.outputSampleRate} dsp_buffer={block}x{blocks} dpi={Screen.dpi:R}");
            _metadata += $" mix_probe={probe}";
            _lastRouteReport = AudioOutputTuning.LatestRouteReport;
            _metadata = OneLine(_metadata) + "\n# observed_audio=" + OneLine(AudioOutputTuning.Report);
            if (!ReferenceEquals(_lastRouteReport, AudioOutputTuning.Report))
                _metadata += " | " + OneLine(_lastRouteReport);
            EnhancedTouch.onFingerDown += Receive;
            EnhancedTouch.onFingerMove += Receive;
            EnhancedTouch.onFingerUp += Receive;
            Active = true;
            LiveTrace.EnabledChanged += OnTestModeChanged;
            AndroidTouchProbe.Begin();
        }

        private static void OnTestModeChanged(bool enabled)
        {
            if (!enabled)
                _ = End("test_mode_disabled");
        }

        private static void Receive(Finger finger)
        {
            EnhancedTouch touch = finger.lastTouch;
            if (touch.valid)
            {
                Vector2 position = touch.screenPosition;
                Record("received", touch.touchId, finger.index, touch.phase, source: touch.time,
                    x: position.x, y: position.y);
            }
        }

        internal static void RecordFrame(double clockAnchor, double musicSeconds, bool playingMusic)
        {
            if (!Active)
                return;

            string routeReport = AudioOutputTuning.LatestRouteReport;
            if (!ReferenceEquals(_lastRouteReport, routeReport))
            {
                _lastRouteReport = routeReport;
                Record("audio_route", detail: routeReport);
            }

            AndroidTouchProbe.Frame();
            Record("frame", source: clockAnchor, song: musicSeconds, note: AudioSettings.dspTime,
                value: Time.unscaledDeltaTime, detail: playingMusic ? "bgm" : "free_run");
        }

        internal static int RecordNoteSe(int noteId, string cue)
        {
            if (!Active)
                return 0;

            int previousCount = _count;
            Record("note_se", noteId: noteId, detail: cue);
            return _count > previousCount ? _count : 0;
        }

        internal static AudioMixProbe.Ticket ReserveMixProbe(int requestSequence)
        {
            if (_mixTickets == null || _mixCount == _mixTickets.Length)
                return null;

            AudioMixProbe.Ticket ticket = _mixTickets[_mixCount++];
            ticket.RequestSequence = requestSequence;
            return ticket;
        }

        internal static void Record(string stage, int touch = -1, int finger = -1,
            TouchPhase phase = TouchPhase.None, int noteId = -1, double source = double.NaN,
            double song = double.NaN, double note = double.NaN, double value = double.NaN,
            string detail = null, NoteResult result = NoteResult.None, int requestSequence = 0,
            double x = double.NaN, double y = double.NaN, double androidEvent = double.NaN,
            double androidReceive = double.NaN, double time = double.NaN)
        {
            if (!Active)
                return;

            var sample = new Sample
            {
                Stage = stage, Detail = detail, Time = double.IsNaN(time) ? Time.realtimeSinceStartupAsDouble : time,
                Frame = Time.frameCount, Touch = touch, Finger = finger, Phase = phase,
                NoteId = noteId, Source = source, Song = song, Note = note, Value = value, Result = result,
                RequestSequence = requestSequence,
                X = x, Y = y, AndroidEvent = androidEvent, AndroidReceive = androidReceive,
            };
            if (IsProbeStage(stage))
            {
                if (_probeCount == _probeSamples.Length)
                    _probeOverflow++;
                else
                    _probeSamples[_probeCount++] = sample;
                return;
            }

            if (_count == _samples.Length)
            {
                _overflow++;
                return;
            }

            _samples[_count++] = sample;
        }

        internal static Task End(string reason)
        {
            if (!Active)
                return Task.CompletedTask;

            AndroidTouchProbe.End();
            for (int i = 0; i < _mixCount; i++)
            {
                AudioMixProbe.Ticket ticket = _mixTickets[i];
                double delay = ticket.DelayMs;
                Record("note_se_mix", value: delay, detail: double.IsNaN(delay) ? "no_signal" : "source_filter",
                    requestSequence: ticket.RequestSequence);
            }
            _mixTickets = null;
            _mixCount = 0;
            Active = false;
            LiveTrace.EnabledChanged -= OnTestModeChanged;
            EnhancedTouch.onFingerDown -= Receive;
            EnhancedTouch.onFingerMove -= Receive;
            EnhancedTouch.onFingerUp -= Receive;

            // Each export owns its completed buffer. A retry or resumed play can record independently.
            Sample[] samples = _samples;
            int count = _count;
            int overflow = _overflow;
            Sample[] probeSamples = _probeSamples;
            int probeCount = _probeCount;
            int probeOverflow = _probeOverflow;
            string metadata = _metadata;
            string directory = Path.Combine(Application.persistentDataPath, "logs");
            string path = Path.Combine(directory, $"latency-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.tsv");
            _samples = null;
            _count = 0;
            _probeSamples = null;
            _probeCount = 0;
            _metadata = null;
            return Task.Run(() => Export(path, samples, count, overflow, probeSamples, probeCount, probeOverflow,
                metadata, reason));
        }

        private static void Export(string path, Sample[] samples, int count, int overflow, Sample[] probeSamples,
            int probeCount, int probeOverflow, string metadata, string reason)
        {
            string pendingPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var writer = new StreamWriter(pendingPath))
                {
                    writer.WriteLine(metadata);
                    string probeSummary = probeCount > 0 || probeOverflow > 0 ? $" probe_overflow={probeOverflow}" : "";
                    writer.WriteLine($"# end={reason} rows={count + probeCount} overflow={overflow}{probeSummary}");
                    writer.WriteLine("seq\tstage\tunity_s\tframe\ttouch_id\tfinger\tphase\tnote_id\tsource_s\tsong_s\tnote_s\tvalue\tresult\tdetail\trequest_seq\tx\ty\tandroid_event_s\tandroid_receive_s");
                    // Gameplay rows first, then the reserved probe rows; seq stays continuous.
                    for (int i = 0; i < count + probeCount; i++)
                    {
                        Sample row = i < count ? samples[i] : probeSamples[i - count];
                        writer.WriteLine(FormattableString.Invariant(
                            $"{i + 1}\t{row.Stage}\t{row.Time:R}\t{row.Frame}\t{row.Touch}\t{row.Finger}\t{row.Phase}\t{row.NoteId}\t{row.Source:R}\t{row.Song:R}\t{row.Note:R}\t{row.Value:R}\t{row.Result}\t{OneLine(row.Detail)}\t{row.RequestSequence}\t{row.X:R}\t{row.Y:R}\t{row.AndroidEvent:R}\t{row.AndroidReceive:R}"));
                    }
                }
                File.Move(pendingPath, path);
                Debug.Log($"[LatencyCapture] wrote {path} ({count} rows, {overflow} overflow, {probeCount} probe rows, {probeOverflow} probe overflow)");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LatencyCapture] export failed: " + exception.Message);
                try
                {
                    File.Delete(pendingPath);
                }
                catch (IOException)
                {
                    // The temporary file is never presented as a completed capture.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            finally
            {
                Array.Clear(samples, 0, count);
                Interlocked.CompareExchange(ref _spareSamples, samples, null);
            }
        }

        /// <summary>Visits the gameplay rows recorded so far in this segment, in order. Used by EditMode replays.</summary>
        internal static void Scan(Action<string, int, int, double, NoteResult> visit)
        {
            for (int i = 0; i < _count; i++)
            {
                Sample row = _samples[i];
                visit(row.Stage, row.NoteId, row.Touch, row.Value, row.Result);
            }
        }

        private static bool IsProbeStage(string stage) =>
            stage == "clock_anchor" || stage.StartsWith("android_", StringComparison.Ordinal);

        private static string OneLine(string value) =>
            value?.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }
}
