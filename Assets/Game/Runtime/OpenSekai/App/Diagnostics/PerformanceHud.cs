using System;
using System.Text;
using System.Collections.Generic;
using OpenSekai.Charts;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Diagnostics
{
    /// <summary>
    /// Optional frame pacing HUD, controlled from settings. Draws an IMGUI overlay and logs one line every
    /// two seconds (readable through `adb logcat -s Unity`), which is how on-device numbers get
    /// collected while the developer plays a chart.
    ///
    /// All statistics are computed per one second window: the first window after start is skipped
    /// because it still contains loading hitches, and the buffer is cleared after every window so an
    /// old 55 Hz idle sample can never leak into a 165 Hz gameplay number.
    /// </summary>
    public sealed class PerformanceHud : MonoBehaviour
    {
        private const float SAMPLE_WINDOW_SECONDS = 1f;
        private const float LOG_INTERVAL_SECONDS = 2f;
        private const string ENABLED_PREF_KEY = "diagnostics.performance_hud.enabled";

        private static PerformanceHud _instance;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(ENABLED_PREF_KEY, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(ENABLED_PREF_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_instance == null) Bootstrap();
                else _instance.gameObject.SetActive(value);
            }
        }

        private float[] _samples = new float[512];
        private int _sampleCount;
        private float _windowStartTime;
        private float _logStartTime;
        private bool _warmedUp;

        private float _lastFps;
        private float _lastAvgMs;
        private float _lastP50;
        private float _lastP95;
        private float _lastP99;
        private float _lastWorstMs;
        private int _lastFrames;
        private float _liveWorstMs;
        private BaseLiveController _liveController;
        private MusicScore _tickScore;
        private List<MusicScoreInfo> _tickTimeline;
        private float _nextControllerSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject("OpenSekai.PerformanceHud");
            DontDestroyOnLoad(host);
            host.SetActive(false);
            _instance = host.AddComponent<PerformanceHud>();
            host.SetActive(Enabled);
        }

        private void OnEnable()
        {
            _sampleCount = 0;
            _lastFrames = 0;
            _lastFps = 0f;
            _lastWorstMs = 0f;
            _liveWorstMs = 0f;
            _warmedUp = false;
            _windowStartTime = Time.unscaledTime;
            _logStartTime = Time.unscaledTime;
            LogDeviceInfo();
        }

        private void LogDeviceInfo()
        {
            Resolution resolution = Screen.currentResolution;
            var builder = new StringBuilder();
            builder.Append("[PerfHud] device=").Append(SystemInfo.deviceModel)
                .Append(" gpu=").Append(SystemInfo.graphicsDeviceName)
                .Append(" api=").Append(SystemInfo.graphicsDeviceType)
                .Append(" cpu=").Append(SystemInfo.processorCount).Append(" cores")
                .Append(" mem=").Append(SystemInfo.systemMemorySize).Append(" MB")
                .Append(" window=").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" screen=").Append(resolution.width).Append('x').Append(resolution.height)
                .Append(" refresh=").Append(resolution.refreshRateRatio.value.ToString("F1"))
                .Append(" targetFrameRate=").Append(Application.targetFrameRate)
                .Append(" vsyncCount=").Append(QualitySettings.vSyncCount);
            Debug.Log(builder.ToString());
            // The output latency the device adds is part of the same picture as the frame pacing.
            Debug.Log("[PerfHud] " + Sekai.AudioOutputTuning.Report);
        }

        private void Update()
        {
            if (_liveController == null && Time.unscaledTime >= _nextControllerSearch)
            {
                _liveController = UnityEngine.Object.FindFirstObjectByType<BaseLiveController>();
                _nextControllerSearch = Time.unscaledTime + 0.5f;
            }

            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f)
            {
                return;
            }

            float ms = delta * 1000f;
            if (_sampleCount == _samples.Length)
            {
                Array.Resize(ref _samples, _samples.Length * 2);
            }
            _samples[_sampleCount++] = ms;

            if (ms > _liveWorstMs)
            {
                _liveWorstMs = ms;
            }

            float now = Time.unscaledTime;
            if (now - _windowStartTime >= SAMPLE_WINDOW_SECONDS)
            {
                CloseWindow(now - _windowStartTime);
                _windowStartTime = now;
            }

            if (now - _logStartTime >= LOG_INTERVAL_SECONDS)
            {
                LogWindow();
                _logStartTime = now;
            }
        }

        private void CloseWindow(float elapsed)
        {
            if (_sampleCount == 0 || elapsed <= 0f)
            {
                return;
            }

            var copy = new float[_sampleCount];
            Array.Copy(_samples, copy, _sampleCount);
            Array.Sort(copy);

            if (!_warmedUp)
            {
                // The first window still contains the loading hitch, skip it.
                _warmedUp = true;
                _sampleCount = 0;
                _liveWorstMs = 0f;
                return;
            }

            _lastFrames = _sampleCount;
            _lastFps = _sampleCount / elapsed;
            _lastWorstMs = _liveWorstMs;
            _lastP50 = copy[Mathf.Clamp((int)(_sampleCount * 0.50f), 0, _sampleCount - 1)];
            _lastP95 = copy[Mathf.Clamp((int)(_sampleCount * 0.95f), 0, _sampleCount - 1)];
            _lastP99 = copy[Mathf.Clamp((int)(_sampleCount * 0.99f), 0, _sampleCount - 1)];

            float sum = 0f;
            for (int i = 0; i < _sampleCount; i++)
            {
                sum += copy[i];
            }
            _lastAvgMs = sum / _sampleCount;

            _sampleCount = 0;
            _liveWorstMs = 0f;
        }

        private void LogWindow()
        {
            if (_lastFrames <= 0)
            {
                return;
            }

            Debug.Log("[PerfHud] fps=" + _lastFps.ToString("F1")
                + " frames=" + _lastFrames
                + " avgMs=" + _lastAvgMs.ToString("F2")
                + " p50=" + _lastP50.ToString("F2")
                + " p95=" + _lastP95.ToString("F2")
                + " p99=" + _lastP99.ToString("F2")
                + " worst=" + _lastWorstMs.ToString("F2")
                + " res=" + Screen.width + "x" + Screen.height);
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(24, Screen.height / 32),
                alignment = TextAnchor.UpperLeft
            };
            style.normal.textColor = Color.white;

            string text = "FPS " + _lastFps.ToString("F0")
                + "  worst " + _lastWorstMs.ToString("F1") + "ms"
                + "\n" + Screen.width + "x" + Screen.height
                + "\nTick " + CurrentTick();

            GUI.Label(new Rect(24f, 24f, Screen.width - 48f, Screen.height * 0.25f), text, style);
        }

        private string CurrentTick()
        {
            if (_liveController == null || !_liveController.HasActiveTimeline)
                return "--";

            MusicScore score = _liveController.BootData?.MusicData?.MusicScore;
            if (score == null || score.musicScoreInfoArray == null || score.musicScoreInfoArray.Length == 0)
                return "--";

            if (!ReferenceEquals(score, _tickScore))
            {
                _tickScore = score;
                _tickTimeline = ChartTimeline.CreateSortedMusicScoreInfoList(score.musicScoreInfoArray);
            }

            MusicScoreInfo frame = MusicScore.CurrentFrameInfo;
            return ChartTimeline.CalculateTicksFromBarAndProgressSorted(frame.bar, frame.barProgress, _tickTimeline).ToString();
        }
    }
}
