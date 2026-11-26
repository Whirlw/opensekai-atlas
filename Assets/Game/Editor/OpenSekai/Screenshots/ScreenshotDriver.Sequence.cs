using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LitMotion;
using OpenSekai.App.UI;
using Sekai.Core.Live;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenSekai.EditorTools.Screenshots
{
    internal sealed partial class ScreenshotDriver
    {
        private static ScreenshotDriver _active;
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private readonly ManualMotionDispatcher _homeClock = new ManualMotionDispatcher();
        private readonly Stopwatch _timer = new Stopwatch();
        private float _previousDelta;
        private SequenceReport _sequence;
        private int _expected;
        private static readonly Color32 _clipGround = new Color32(12, 15, 20, 255);

        private static float CaptureStep(Shot shot) =>
            1f / (shot.to.HasValue ? shot.fps : Mathf.Clamp(shot.fps <= 0 ? 60 : shot.fps, 10, 240));

        private static IEnumerable<double> CaptureTimes(Shot shot, float fallback)
        {
            if (shot.to.HasValue) return Enumerable.Range(0, shot.FrameCount).Select(shot.FrameTime);
            if (shot.times == null || shot.times.Count == 0) return new[] { (double)fallback };
            return shot.times.OrderBy(t => t).Select(t => (double)t);
        }

        private IEnumerator CaptureLiveOffsets(Shot shot, SoloLiveController controller, bool info)
        {
            double elapsed = 0d;
            double step = 1d / shot.fps;
            for (int i = 0; i < shot.FrameCount; i++)
            {
                double target = shot.FrameTime(i);
                while (target - elapsed > 1e-9)
                {
                    double delta = Math.Min(step, target - elapsed);
                    Time.captureDeltaTime = (float)delta;
                    yield return new WaitForEndOfFrame();
                    elapsed += delta;
                    if (controller == null) throw new InvalidOperationException("The live has already ended.");
                    if (info) KeepMusicOff(controller);
                }
                if (info && controller.JustPlayingState) throw new InvalidOperationException("The info page has already ended.");
                WriteSequenceFrame();
            }
        }

        private IEnumerator WithSequence(Shot shot, IEnumerator body)
        {
            bool timedResult = string.Equals(shot.kind, "result", StringComparison.OrdinalIgnoreCase) && shot.times?.Count > 0;
            if (!shot.to.HasValue && !timedResult)
            {
                yield return body;
                yield break;
            }
            var previousScheduler = HomeMotion.CaptureScheduler;
            float? previousHeld = MedalClock.Held;
            var previousIntroClock = BaseLiveController.CaptureIntroClock;
            try
            {
                HomeMotion.CaptureScheduler = _homeClock.Scheduler;
                MedalClock.Held = 0f;
                Time.captureDeltaTime = CaptureStep(shot);
                if (shot.kind == "info") BaseLiveController.CaptureIntroClock = () => Time.time;
                if (shot.to.HasValue && shot.kind != "clip") BeginSequence(shot, shot.RangeStem(FileStem(shot, null)));
                yield return body;
                if (shot.to.HasValue && shot.kind != "clip") EndSequence();
            }
            finally
            {
                BaseLiveController.CaptureIntroClock = previousIntroClock;
                HomeMotion.CaptureScheduler = previousScheduler;
                MedalClock.Held = previousHeld;
                _homeClock.Reset();
                Time.captureDeltaTime = _previousDelta;
            }
        }

        private void BeginSequence(Shot shot, string stem)
        {
            string folder = stem;
            string path = Path.Combine(_request.outDir, folder);
            for (int suffix = 2; Directory.Exists(path) || File.Exists(path); suffix++)
            {
                folder = stem + "-" + suffix;
                path = Path.Combine(_request.outDir, folder);
            }
            _sequence = new SequenceReport
            {
                kind = shot.kind, file = folder, fps = shot.fps, from = shot.from.Value, to = shot.to.Value,
                width = _request.width, height = _request.height
            };
            _reports.Add(_sequence);
            _expected = shot.FrameCount;
            _timer.Restart();
            Directory.CreateDirectory(path);
        }

        private void EndSequence(string error = null)
        {
            if (error == null && _sequence.frames != _expected)
                throw new InvalidDataException("Sequence ended early.");
            _sequence.ok = error == null;
            _sequence.elapsedSeconds = _timer.Elapsed.TotalSeconds;
            _sequence.detail = $"{_sequence.frames}/{_expected} frames at {_sequence.fps} fps";
            if (error != null) _sequence.note = $"{_sequence.frames}/{_expected} frames written. {error}";
            Log(_sequence.file + " " + (_sequence.note ?? _sequence.detail));
            _sequence = null;
        }

        private IEnumerator DrawSequence(Shot shot, Action<float> seek, Image ground = null)
        {
            double previous = Math.Min(0d, shot.from.Value);
            for (int i = 0; i < shot.FrameCount; i++)
            {
                double time = shot.FrameTime(i);
                // Pre-roll cropped clips through the same callbacks (including the award's nested shine).
                double step = 1d / shot.fps;
                while (time - previous > step + 1e-9)
                {
                    _homeClock.Update(step);
                    previous += step;
                }
                _homeClock.Update(Math.Max(0d, time - previous));
                previous = time;
                MedalClock.Held = (float)time;
                seek?.Invoke((float)time);
                if (shot.alpha) ground.color = Color.black;
                // Resume in Update so graphics that dirty their mesh there see the new held time.
                yield return null;
                yield return new WaitForEndOfFrame();
                if (!shot.alpha) WriteSequenceFrame();
                else
                {
                    Texture2D black = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        ground.color = Color.white;
                        yield return null;
                        yield return new WaitForEndOfFrame();
                        WriteSequenceFrame(black);
                    }
                    finally { Object.Destroy(black); }
                }
            }
        }

        private void WriteSequenceFrame(Texture2D black = null)
        {
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (texture.width != _sequence.width || texture.height != _sequence.height)
                    throw new InvalidDataException($"Game view is {texture.width}x{texture.height}.");
                if (black != null)
                {
                    Color32[] colors = black.GetPixels32();
                    Color32[] white = texture.GetPixels32();
                    for (int i = 0; i < colors.Length; i++)
                    {
                        Color32 b = colors[i], w = white[i];
                        int alpha = Mathf.Clamp(255 - (w.r - b.r + w.g - b.g + w.b - b.b + 1) / 3, 0, 255);
                        colors[i] = alpha == 0 ? default : new Color32(Unmultiply(b.r, alpha), Unmultiply(b.g, alpha),
                            Unmultiply(b.b, alpha), (byte)alpha);
                    }
                    if (!texture.Reinitialize(texture.width, texture.height, TextureFormat.RGBA32, false))
                        throw new InvalidOperationException("Could not create RGBA frame.");
                    texture.SetPixels32(colors);
                    texture.Apply(false);
                }
                string name = Shot.FrameName(_sequence.file, _sequence.frames);
                string path = Path.Combine(_request.outDir, _sequence.file, name);
                byte[] png = texture.EncodeToPNG();
                // A failed write must never leave a truncated numbered frame for the editing software.
                File.WriteAllBytes(path + ".part", png);
                File.Move(path + ".part", path);
                _sequence.frames++;
            }
            finally { Object.Destroy(texture); }
        }

        private static byte Unmultiply(byte value, int alpha) => (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);

        private void DisposeStack()
        {
            while (_stack.Count > 0)
            {
                try { (_stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { UnityEngine.Debug.LogException(error); }
            }
        }

        internal static void Interrupt()
        {
            if (_active == null) return;
            ScreenshotDriver driver = _active;
            _active = null;
            if (driver._sequence != null) driver.EndSequence("Play Mode stopped.");
            driver.DisposeStack();
            StopMotionClock();
            Time.captureDeltaTime = driver._previousDelta;
            AudioListener.volume = driver._previousVolume;
            driver.RestoreLiveSettings();
            driver.SaveReport();
        }
    }

    [Serializable]
    internal sealed class SequenceReport : ShotReport
    {
        public int frames;
        public int fps;
        public double from;
        public double to;
        public double elapsedSeconds;
    }
}
