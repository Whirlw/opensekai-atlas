using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using Newtonsoft.Json;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    /// <summary>
    /// Runs a <see cref="ShotRequest"/> inside Play Mode, from the Library scene. Lives on a hidden
    /// DontDestroyOnLoad object so it survives the Library -> Core -> Library round trip of a live.
    ///
    /// Live frames are reproducible: the music is stopped as soon as it would start, so the live
    /// clock runs on its own and moves by the frame time, and the frame time is fixed with
    /// Time.captureDeltaTime. The live is fast-forwarded in 100 ms steps (the clock's own cap) up to
    /// <c>lead</c> seconds before each time, played at <c>fps</c> from there, and the last step is
    /// trimmed so the captured frame lands on the requested millisecond.
    /// </summary>
    internal sealed class ScreenshotDriver : IPlayerLoopItem
    {
        private const BindingFlags ANY = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float FAST_FORWARD_STEP = 0.1f;
        private const float REAL_TIMEOUT = 120f;

        private static readonly ManualMotionDispatcher _motionClock = new ManualMotionDispatcher();

        private ShotRequest _request;
        private readonly List<ShotReport> _reports = new List<ShotReport>();
        private Action<RunReport> _onDone;
        private GameObject _host;
        private float _previousVolume;
        private bool _settingsChanged;
        private int? _previousFakePerfect;

        internal static void Begin(ShotRequest request, Action<RunReport> onDone)
        {
            // Editor-assembly MonoBehaviours cannot be added at runtime, so the coroutine runs on an
            // inert built-in component (no graphic, nothing raycasts into it).
            var host = new GameObject("[OpenSekai Screenshots]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(host);
            var runner = host.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var driver = new ScreenshotDriver { _request = request, _onDone = onDone, _host = host };
            PlayerLoopHelper.AddAction(PlayerLoopTiming.LastPreLateUpdate, driver);
            runner.StartCoroutine(driver.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(_request.outDir);
            _previousVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            Log("output " + _request.outDir + " at " + _request.width + "x" + _request.height);

            yield return WaitForLibrary();
            int index = 0;
            foreach (Shot shot in _request.shots)
            {
                index++;
                IEnumerator body;
                switch ((shot.kind ?? string.Empty).ToLowerInvariant())
                {
                    case "live": body = LiveShot(shot, false); break;
                    case "info": body = LiveShot(shot, true); break;
                    case "finish": body = FinishShot(shot); break;
                    case "library": body = LibraryShot(shot, null); break;
                    case "settings": body = LibraryShot(shot, "settings"); break;
                    case "settings-trace": body = LibraryShot(shot, "settings-trace"); break;
                    case "result": body = LibraryShot(shot, "result"); break;
                    case "actions": body = LibraryShot(shot, "actions"); break;
                    case "actions-armed": body = LibraryShot(shot, "actions-armed"); break;
                    case "loading": body = LibraryShot(shot, "loading"); break;
                    case "return": body = ReturnShot(shot); break;
                    case "home": body = HomeShot(shot, false); break;
                    case "intro": body = HomeShot(shot, true); break;
                    case "handoff": body = HandoffShot(shot); break;
                    case "about": body = AboutShot(shot); break;
                    case "server": body = ServerShot(shot); break;
                    default:
                        Fail(shot, "unknown kind '" + shot.kind + "'");
                        continue;
                }
                yield return Guard(body, shot);
            }

            Time.captureDeltaTime = 0f;
            AudioListener.volume = _previousVolume;
            var report = new RunReport
            {
                outDir = _request.outDir,
                width = _request.width,
                height = _request.height,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                shots = _reports
            };
            File.WriteAllText(Path.Combine(_request.outDir, "report.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
            Log("done: " + _reports.Count(r => r.ok) + " ok, " + _reports.Count(r => !r.ok) + " failed");
            _onDone?.Invoke(report);
            UnityEngine.Object.Destroy(_host);
        }

        /// <summary>Runs one shot; an exception fails that shot and puts the game back on the library.</summary>
        private IEnumerator Guard(IEnumerator body, Shot shot)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool moved;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception exception)
                {
                    Fail(shot, exception.Message);
                    Debug.LogException(exception);
                    break;
                }
                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
                else
                {
                    yield return top.Current;
                }
            }

            yield return LeaveLive();
            StopMotionClock();
        }

        // ---------------------------------------------------------------- library pages

        private IEnumerator LibraryShot(Shot shot, string panel)
        {
            yield return WaitForLibrary();
            LibraryScreen screen = UnityEngine.Object.FindFirstObjectByType<LibraryScreen>();
            LibrarySettingsPanel settings = UnityEngine.Object.FindFirstObjectByType<LibrarySettingsPanel>(FindObjectsInactive.Include);
            LibraryResultPanel resultPanel = UnityEngine.Object.FindFirstObjectByType<LibraryResultPanel>(FindObjectsInactive.Include);
            LibraryActionsPanel actionsPanel = UnityEngine.Object.FindFirstObjectByType<LibraryActionsPanel>(FindObjectsInactive.Include);
            LibraryLoadingOverlay loading = UnityEngine.Object.FindFirstObjectByType<LibraryLoadingOverlay>(FindObjectsInactive.Include);
            settings?.Hide();
            resultPanel?.Hide();
            actionsPanel?.Hide();
            loading?.Hide();
            object view = screen.GetType().GetField("_view", ANY)?.GetValue(screen);
            object presenter = screen.GetType().GetField("_presenter", ANY)?.GetValue(screen);
            SongPackageItem selected = view?.GetType().GetField("_selected", ANY)?.GetValue(view) as SongPackageItem;

            string detail = null;
            if (panel == "settings" || panel == "settings-trace")
            {
                if (settings == null) throw new InvalidOperationException("LibrarySettingsPanel not found.");
                settings.Show();
                if (panel == "settings-trace")
                {
                    GameObject trace = settings.transform.GetComponentsInChildren<Transform>(true)
                        .Select(t => t.gameObject).FirstOrDefault(g => g.name == "TracePanel");
                    if (trace == null) throw new InvalidOperationException("TracePanel not found under the settings panel.");
                    trace.SetActive(true);
                    trace.transform.SetAsLastSibling();
                }
            }
            else if (panel == "result")
            {
                ResultSample sample = shot.result ?? new ResultSample();
                // Played from the selected package, so the card shows its jacket like a real run.
                screen.ShowResult(ToResult(sample, selected?.Entry?.RootDirectory));
                detail = sample.kind + " " + sample.score + " " + sample.rank;
            }

            else if (panel == "actions" || panel == "actions-armed")
            {
                if (presenter == null || selected == null) throw new InvalidOperationException("No package is selected.");
                presenter.GetType().GetMethod("OpenActionsForSelected", ANY).Invoke(presenter, null);
                if (panel == "actions-armed")
                {
                    // The first tap on 删除曲包 only arms it.
                    actionsPanel.GetType().GetMethod("OnDeleteClicked", ANY).Invoke(actionsPanel, null);
                }
                detail = selected.Entry?.RootDirectory;
            }
            else if (panel == "loading")
            {
                if (view == null || selected == null) throw new InvalidOperationException("No package is selected.");
                view.GetType().GetMethod("ShowLoading", ANY)
                    .Invoke(view, new object[] { selected.Entry.Manifest?.title ?? "Loading", selected.Entry.RootDirectory });
                view.GetType().GetMethod("SetLoadingMessage", ANY).Invoke(view, new object[] { "正在读取谱面…" });
                detail = selected.Entry?.RootDirectory;
            }

            // Two frames for layout and TextMeshPro to settle, then long enough for the panel's
            // opening motion (and the result's score count) to finish.
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Capture(shot, FileStem(shot, null), detail, null);

            settings?.Hide();
            resultPanel?.Hide();
            actionsPanel?.Hide();
            if (panel == "loading")
            {
                view?.GetType().GetMethod("HideLoading", ANY)?.Invoke(view, null);
            }
        }

        private static PlayResult ToResult(ResultSample sample, string packageDirectory = null)
        {
            packageDirectory = packageDirectory ?? string.Empty;
            Enum.TryParse(sample.kind, true, out PlayResultKind kind);
            if (kind == PlayResultKind.LoadError)
            {
                return PlayResult.FromLoadError(sample.title, sample.auto, packageDirectory, sample.message);
            }
            Enum.TryParse(sample.rank, true, out ScoreRank rank);
            var score = new LiveScore
            {
                totalScore = sample.score,
                rank = rank,
                maxCombo = sample.maxCombo,
                totalComboCount = sample.totalCombo,
                perfectCount = sample.perfect,
                greatCount = sample.great,
                goodCount = sample.good,
                badCount = sample.bad,
                missCount = sample.miss,
                autoCount = sample.auto ? sample.totalCombo : 0,
                life = sample.life
            };
            return new PlayResult(kind, sample.auto, packageDirectory, sample.title, sample.playLevel, score);
        }

        /// <summary>
        /// The real way back from a run: start a live, retire from it once it plays, and capture
        /// the library as it comes back with the result card up. Unlike the "result" shot the page
        /// is rebuilt from scratch here, so the card opens before the jacket has been decoded.
        /// </summary>
        private IEnumerator ReturnShot(Shot shot)
        {
            yield return StartLive(shot, false, null);
            SoloLiveController controller = null;
            yield return WaitUntil(() => (controller = UnityEngine.Object.FindFirstObjectByType<SoloLiveController>()) != null
                && controller.JustPlayingState, "the live to start");
            controller.OnRetireByMySelf();
            yield return WaitForLibrary();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture(shot, FileStem(shot, null), "returned from a retired live", null);
        }

        // ---------------------------------------------------------------- home page and splash

        /// <summary>Intro times taken when a request names none: lanes, fall, hit, morph, orbit, wordmark, flight, home.</summary>
        private static readonly float[] _defaultIntroTimes = { 0.45f, 0.95f, 1.12f, 1.45f, 1.9f, 2.6f, 3.5f, 4.4f };

        /// <summary>
        /// The splash and the home page are one seekable timeline with the backdrop's drift frozen
        /// at the same time, so these frames are reproducible like live frames. The home page is
        /// that timeline's last frame.
        /// </summary>
        private IEnumerator HomeShot(Shot shot, bool intro)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            float[] times = intro
                ? (shot.times != null && shot.times.Count > 0 ? shot.times.ToArray() : _defaultIntroTimes)
                : new[] { HomeScreen.IntroLength };
            home.SeekIntro(times[0]);
            // The emblem is drawn on worker threads and the jackets load from disk; let both land.
            yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
            yield return new WaitForSecondsRealtime(1.2f);

            foreach (float time in times)
            {
                home.SeekIntro(time);
                yield return null;
                yield return null;
                string stem = intro ? FileStem(shot, time) : FileStem(shot, null);
                yield return Capture(shot, stem, intro ? "intro +" + time.ToString("0.000") + "s" : "home page", null, intro ? time : (float?)null);
            }

            home.ShowLibraryImmediately();
        }

        /// <summary>
        /// The library card growing over the page (two held frames), then the real round trip
        /// through the calls a tap and the back button make: the library once it is up, and the
        /// home page once it has taken back over.
        /// </summary>
        private IEnumerator HandoffShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            LibraryScreen library = UnityEngine.Object.FindFirstObjectByType<LibraryScreen>();
            if (home == null || library == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            home.SeekIntro(HomeScreen.IntroLength);
            yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
            yield return new WaitForSecondsRealtime(1.2f);
            foreach (float amount in new[] { 0.35f, 0.7f })
            {
                home.SeekHandoff(amount);
                yield return null;
                yield return null;
                yield return Capture(shot, FileStem(shot, amount), "library card grown " + amount.ToString("0.00"), null, amount);
            }

            home.ShowHomeImmediately();
            yield return null;
            home.EnterLibrary();
            yield return WaitUntil(() => library.IsPresented && !home.IsHomeShown, "the library after the hand-over");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture(shot, FileStem(shot, null) + "-library", "after tapping the library card", null);

            home.ReturnHome();
            yield return WaitUntil(() => home.IsHomeShown && !library.IsPresented, "the home page after back");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(shot, FileStem(shot, null) + "-home", "after the library's back button", null);
            home.ShowLibraryImmediately();
        }

        /// <summary>The home page's 关于 card, then the hint a tap on an unbuilt part (服务器) shows.</summary>
        private IEnumerator AboutShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            home.SeekIntro(HomeScreen.IntroLength);
            yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
            yield return new WaitForSecondsRealtime(1.2f);
            typeof(HomeScreen).GetMethod("OpenAbout", ANY).Invoke(home, null);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture(shot, FileStem(shot, null), "about card", null);

            home.ShowHomeImmediately();
            typeof(HomeScreen).GetMethod("ShowComingSoon", ANY).Invoke(home, new object[] { "资源包" });
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture(shot, FileStem(shot, null) + "-soon", "coming-soon hint", null);
            home.ShowLibraryImmediately();
        }

        /// <summary>
        /// The server part: the server card growing over the home page (two held frames), the
        /// real tap into the server list, the 添加服务器 card, then UntitledCharts (from the live
        /// server, so these frames need the network): its level home page, the advanced search,
        /// every level, and the first level's page with each of its three tabs. Last, back out
        /// to the home page the way the back button goes.
        /// </summary>
        private IEnumerator ServerShot(Shot shot)
        {
            yield return WaitForLibrary();
            HomeScreen home = HomeScreen.Current;
            if (home == null)
            {
                throw new InvalidOperationException("The Library scene has no home page.");
            }

            string stem = FileStem(shot, null);
            home.SeekIntro(HomeScreen.IntroLength);
            yield return WaitUntil(() => home.IsBrandReady, "the brand emblem");
            yield return new WaitForSecondsRealtime(1.2f);
            foreach (float amount in new[] { 0.35f, 0.7f })
            {
                home.SeekServerHandoff(amount);
                yield return null;
                yield return null;
                yield return Capture(shot, FileStem(shot, amount), "server card grown " + amount.ToString("0.00"), null, amount);
            }

            home.ShowHomeImmediately();
            yield return null;
            home.EnterServer();
            yield return WaitUntil(() => home.IsServerShown, "the server list after the hand-over");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(shot, stem + "-list", "server list", null);

            ServerScreen screen = UnityEngine.Object.FindFirstObjectByType<ServerScreen>();
            if (screen == null || !(screen.Top is ServerListPage list))
            {
                throw new InvalidOperationException("The server part did not open on its list.");
            }

            list.OpenAddPanel();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture(shot, stem + "-add", "add server card", null);
            list.CloseAddPanel();

            // Opened directly, not added: a screenshot run never changes the player's server list.
            var untitled = new OpenSekai.App.Server.ServerEntry(
                "screenshots-untitledcharts", OpenSekai.App.Server.ServerKind.Sonolus, "UntitledCharts", "https://untitledcharts.com");
            screen.OpenServer(untitled);
            SonolusBrowsePage browse = null;
            yield return WaitUntil(() => (browse = screen.Top as SonolusBrowsePage) != null && browse.IsHomeReady, "the UntitledCharts sections");
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture(shot, stem + "-sonolus-home", "UntitledCharts level home page", null);

            browse.OpenAdvancedSearch();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture(shot, stem + "-sonolus-search", "advanced search", null);
            browse.CloseAdvancedSearch();

            browse.ShowAllLevels();
            yield return WaitUntil(() => browse.IsListReady, "every level");
            yield return new WaitForSecondsRealtime(3f);
            yield return Capture(shot, stem + "-sonolus-all", "every level", null);

            SonolusLevel first = browse.FirstLevel;
            screen.OpenLevel(browse.Session, first);
            SonolusLevelPage level = null;
            yield return WaitUntil(() => (level = screen.Top as SonolusLevelPage) != null && level.HasDetails, "the level details");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Capture(shot, stem + "-level", "level page, description", null);
            string[] tabs = { null, "community", "leaderboard" };
            for (int tab = 1; tab < 3; tab++)
            {
                level.ShowTab(tab);
                int current = tab;
                yield return WaitUntil(() => level.IsTabLoaded, "the level page's " + tabs[current] + " tab");
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture(shot, stem + "-level-" + tabs[tab], "level page, " + tabs[tab], null);
            }

            while (screen.TryHandleBack())
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            screen.Back();
            yield return WaitUntil(() => home.IsHomeShown && !home.IsServerShown, "the home page after back");
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(shot, stem + "-home", "after the server list's back button", null);
            home.ShowLibraryImmediately();
        }

        // ---------------------------------------------------------------- live and info page

        private IEnumerator LiveShot(Shot shot, bool infoPage)
        {
            List<float> times = (shot.times ?? new List<float>()).OrderBy(t => t).ToList();
            if (times.Count == 0)
            {
                times.Add(infoPage ? 1.5f : 30f);
            }
            float frame = 1f / Mathf.Clamp(shot.fps <= 0 ? 60 : shot.fps, 10, 240);

            StartMotionClock(frame);
            yield return StartLive(shot, infoPage, null);
            SoloLiveController controller = null;
            yield return WaitUntil(() => (controller = UnityEngine.Object.FindFirstObjectByType<SoloLiveController>()) != null, "the live controller");

            if (infoPage)
            {
                // The page starts in the same step that starts the live clock.
                yield return WaitUntil(() => { KeepMusicOff(controller); return controller.CapturePhase != BaseLiveController.MusicPhase.NotStarted; }, "the info page");
                yield return new WaitForEndOfFrame();
                int startFrame = Time.frameCount;
                foreach (float offset in times)
                {
                    int target = startFrame + Mathf.RoundToInt(offset / frame);
                    while (Time.frameCount < target)
                    {
                        yield return new WaitForEndOfFrame();
                        KeepMusicOff(controller);
                    }
                    float actual = (Time.frameCount - startFrame) * frame;
                    string note = controller.JustPlayingState
                        ? "the live had already started (the editor ran below ~37 fps real time); the HUD may be fading in"
                        : null;
                    CaptureFrame(shot, FileStem(shot, offset), "info +" + actual.ToString("0.000") + "s", note, actual);
                }
                yield break;
            }

            yield return StartLiveClock(controller);
            long startMs = controller.CaptureTimeMs;
            double frameMs = 1000d * frame;
            foreach (float seconds in times)
            {
                long targetMs = (long)Math.Round(seconds * 1000d);
                long leadMs = (long)Math.Round(Mathf.Max(0f, shot.lead) * 1000d);
                long playFromMs = Math.Max(targetMs - leadMs, controller.CaptureTimeMs);
                string note = targetMs < startMs
                    ? "the live starts at " + (startMs / 1000d).ToString("0.000") + " s (first note minus the lead-in); this is its first frame"
                    : null;
                int frameIndex = 0;
                while (true)
                {
                    if (controller == null || controller.CaptureFinished)
                    {
                        throw new InvalidOperationException("The chart was over before " + seconds + " s (every note was done at "
                            + (controller.CaptureTimeMs / 1000d).ToString("0.000") + " s). Use kind \"finish\" for the end of a live.");
                    }
                    long now = controller.CaptureTimeMs;
                    if (now >= targetMs)
                    {
                        break;
                    }
                    // Fast-forward to exactly lead seconds before the target, then step at fps, so the
                    // frames that matter are the same whatever the live's start point was.
                    long next = now < playFromMs
                        ? Math.Min(now + (long)(FAST_FORWARD_STEP * 1000f), playFromMs)
                        : Math.Min(targetMs, Math.Max(now + 1, playFromMs + (long)Math.Round(++frameIndex * frameMs)));
                    yield return Step(controller, next - now);
                }
                long captured = controller.CaptureTimeMs;
                CaptureFrame(shot, FileStem(shot, seconds), "music " + (captured / 1000d).ToString("0.000") + "s", note, captured / 1000f);
            }
        }

        private static IEnumerator StartLiveClock(SoloLiveController controller)
        {
            yield return WaitUntil(() => controller.CapturePhase != BaseLiveController.MusicPhase.NotStarted, "the live clock to start");
            // The controller waits a short, real-time interval before enabling gameplay. Park its
            // clock before that interval can move the first simulation frame by a variable amount.
            TakeClock(controller);
            controller.CaptureTimeMs = controller.TimelineStartMs;
            yield return WaitUntil(() => controller.JustPlayingState, "the live to start");
            yield return new WaitForEndOfFrame();
        }

        /// <summary>
        /// Advances the live by one frame of <paramref name="stepMs"/>: the next frame's Update sees the
        /// clock moved by that much and every scaled-time animation moved by the same amount.
        /// </summary>
        private static IEnumerator Step(SoloLiveController controller, long stepMs)
        {
            stepMs = Math.Max(1L, stepMs);
            Time.captureDeltaTime = stepMs / 1000f;
            controller.CaptureTimeMs += stepMs;
            yield return new WaitForEndOfFrame();
            TakeClock(controller);
        }

        /// <summary>
        /// Stops the music and parks the controller's clock: with the phase at NotStarted its
        /// UpdateMusicTime leaves the time alone, so the time set by <see cref="Step"/> is the time the
        /// frame is drawn and judged at.
        /// </summary>
        private static void TakeClock(SoloLiveController controller)
        {
            if (controller == null)
            {
                return;
            }
            KeepMusicOff(controller);
            controller.CapturePhase = BaseLiveController.MusicPhase.NotStarted;
        }

        private static readonly HashSet<ParticleSystem> _seededParticles = new HashSet<ParticleSystem>();

        /// <summary>
        /// Particle systems pick a new random seed every play, so the same tap effect looks different
        /// in two runs. Give each one a seed made from its place in the hierarchy (pools are built in
        /// the same order every time). A system that is already playing is restarted to take the seed.
        /// </summary>
        private static void SeedParticles()
        {
            foreach (ParticleSystem system in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (system == null || !_seededParticles.Add(system) || !system.useAutoRandomSeed)
                {
                    continue;
                }
                bool playing = system.isPlaying;
                if (playing)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                system.useAutoRandomSeed = false;
                system.randomSeed = StableHash(system.transform);
                if (playing)
                {
                    system.Play(false);
                }
            }
        }

        private static uint StableHash(Transform transform)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (Transform t = transform; t != null; t = t.parent)
                {
                    foreach (char c in t.name)
                    {
                        hash = (hash ^ c) * 16777619;
                    }
                    hash = (hash ^ (uint)t.GetSiblingIndex()) * 16777619;
                }
                return hash;
            }
        }

        private static void StartMotionClock(float step)
        {
            Time.captureDeltaTime = step;
            LiveMotion.CaptureScheduler = _motionClock.Scheduler;
        }

        internal static void StopMotionClock()
        {
            if (LiveMotion.CaptureScheduler == _motionClock.Scheduler)
            {
                LiveMotion.CaptureScheduler = null;
                _motionClock.Reset();
                _seededParticles.Clear();
                Time.captureDeltaTime = 0f;
            }
        }

        bool IPlayerLoopItem.MoveNext()
        {
            if (_host == null)
            {
                return false;
            }
            if (LiveMotion.CaptureScheduler == _motionClock.Scheduler)
            {
                // Tick once after gameplay updates and before rendering the current frame.
                _motionClock.Update(Time.captureDeltaTime);
                SeedParticles();
            }
            return true;
        }

        /// <summary>The end of a live: fast-forward until every note is done, then count frames from there.</summary>
        private IEnumerator FinishShot(Shot shot)
        {
            List<float> times = (shot.times ?? new List<float>()).OrderBy(t => t).ToList();
            if (times.Count == 0)
            {
                times.Add(2f);
            }
            float frame = 1f / Mathf.Clamp(shot.fps <= 0 ? 60 : shot.fps, 10, 240);

            StartMotionClock(FAST_FORWARD_STEP);
            yield return StartLive(shot, false, string.IsNullOrWhiteSpace(shot.finishAnimation) ? "allperfect" : shot.finishAnimation);
            SoloLiveController controller = null;
            yield return WaitUntil(() => (controller = UnityEngine.Object.FindFirstObjectByType<SoloLiveController>()) != null, "the live controller");
            yield return StartLiveClock(controller);
            float deadline = Time.realtimeSinceStartup + 600f;
            while (!controller.CaptureFinished)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("The live did not finish within 10 minutes.");
                yield return Step(controller, (long)(FAST_FORWARD_STEP * 1000f));
            }
            // From here on only scaled time matters: the result coroutine waits with WaitForSeconds.
            Time.captureDeltaTime = frame;
            long endedAt = controller.CaptureTimeMs;
            int startFrame = Time.frameCount;
            foreach (float offset in times)
            {
                int target = startFrame + Mathf.RoundToInt(offset / frame);
                while (Time.frameCount < target)
                {
                    yield return new WaitForEndOfFrame();
                }
                float actual = (Time.frameCount - startFrame) * frame;
                CaptureFrame(shot, FileStem(shot, offset), "finish +" + actual.ToString("0.000") + "s (last note done at "
                    + (endedAt / 1000d).ToString("0.000") + " s; the result animation starts at +1 s)", null, actual);
            }
        }

        private IEnumerator StartLive(Shot shot, bool infoPage, string finishAnimation)
        {
            yield return WaitForLibrary();
            string directory = ResolvePackage(shot.package);
            PlayStartResult result = null;
            Exception error = null;
            yield return PlayService.Instance
                .StartAsync(new PlayRequest(directory, true, SceneManager.Scene.Library), CancellationToken.None)
                .ToCoroutine(r => result = r, e => error = e);
            if (error != null || result == null || !result.Started)
            {
                throw new InvalidOperationException("The live did not start: " + (error?.Message ?? result?.Message));
            }

            FreeLiveBootData boot = UserDataManager.Instance.FreeLiveBootData;
            if (!string.IsNullOrWhiteSpace(shot.difficulty))
            {
                SetDifficulty(boot, shot.difficulty.Trim().ToLowerInvariant());
            }
            // The live shot has no use for the info page; the info shot needs it whatever the settings say.
            boot.LiveSettingData.CustomMusicScoreMusicInfoDisplayMode = infoPage
                ? LiveSettingData.MUSIC_INFO_DISPLAY_MODE_CUSTOM_SCORE
                : LiveSettingData.MUSIC_INFO_DISPLAY_MODE_SKIP;
            boot.MusicData.PlayStartEffectEnabled = infoPage;

            // The views read these options from the shared settings, not from the boot's copy. Only the
            // in-memory settings change; they are put back when the shot leaves the live.
            int fakePerfect = shot.fakePerfect ? LiveSettingData.AUTO_FAKE_PERFECT_MODE_FAKE_PERFECT : LiveSettingData.AUTO_FAKE_PERFECT_MODE_AUTO;
            LiveSettingData shared = OpenSekai.Shared.Settings.LiveSettingsStore.Shared.Load();
            if (!_settingsChanged)
            {
                _previousFakePerfect = shared.CustomMusicScoreAutoFakePerfectMode;
                _settingsChanged = true;
            }
            shared.CustomMusicScoreAutoFakePerfectMode = fakePerfect;
            boot.LiveSettingData.CustomMusicScoreAutoFakePerfectMode = fakePerfect;
            if (finishAnimation != null)
            {
                boot.ResultAnimationPreviewOverride = ResultAnimation(finishAnimation);
            }
            Log((infoPage ? "info" : "live") + ": " + Path.GetFileName(directory) + " " + boot.MusicData.DifficultyString);
        }

        private static int ResultAnimation(string name)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "none": return LiveSettingData.AUTO_RESULT_ANIMATION_NONE;
                case "allperfect": case "ap": return LiveSettingData.AUTO_RESULT_ANIMATION_ALL_PERFECT;
                case "fullcombo": case "fc": return LiveSettingData.AUTO_RESULT_ANIMATION_FULL_COMBO;
                case "clear": return LiveSettingData.AUTO_RESULT_ANIMATION_CLEAR;
                case "finish": case "lifezero": return LiveSettingData.AUTO_RESULT_ANIMATION_FINISH;
                default: throw new InvalidOperationException("Unknown finishAnimation '" + name + "' (allperfect, fullcombo, clear, finish, none).");
            }
        }

        private static void SetDifficulty(FreeLiveBootData boot, string difficulty)
        {
            if (!Enum.TryParse(difficulty, true, out MusicDifficulty value) || value == MusicDifficulty.none)
            {
                throw new InvalidOperationException("Unknown difficulty '" + difficulty + "' (easy, normal, hard, expert, master, append).");
            }
            LiveMusicData music = boot.MusicData;
            typeof(LiveMusicData).GetProperty(nameof(LiveMusicData.DifficultyEnum)).SetValue(music, value);
            typeof(LiveMusicData).GetProperty(nameof(LiveMusicData.DifficultyString)).SetValue(music, value.ToString());
            if (music.Difficulty != null)
            {
                music.Difficulty.musicDifficulty = value.ToString();
            }
        }

        /// <summary>Leaves the live, if one is up, and waits for the library page again.</summary>
        private IEnumerator LeaveLive()
        {
            SoloLiveController controller = UnityEngine.Object.FindFirstObjectByType<SoloLiveController>();
            if (controller != null)
            {
                Time.captureDeltaTime = FAST_FORWARD_STEP;
                // A finished live waits for the rest of the music; skip it like the player can.
                if (controller.CaptureFinished) controller.SkipOutro();
                else controller.OnRetireByMySelf();
                // The retire publishes a result for the library; the screenshot run is not a session.
                PlayResultHandoff.Clear();
            }
            yield return WaitForLibrary();
            Time.captureDeltaTime = 0f;
            UnityEngine.Object.FindFirstObjectByType<LibraryResultPanel>(FindObjectsInactive.Include)?.Hide();
            if (_settingsChanged)
            {
                LiveSettingData shared = OpenSekai.Shared.Settings.LiveSettingsStore.Shared.Load();
                shared.CustomMusicScoreAutoFakePerfectMode = _previousFakePerfect;
                _settingsChanged = false;
            }
        }

        /// <summary>The audio would move the clock in real time: stop it, the clock then runs on the frame time.</summary>
        private static void KeepMusicOff(SoloLiveController controller)
        {
            if (controller != null && controller.CapturePhase == BaseLiveController.MusicPhase.Playing)
            {
                SoundManager.Instance.StopIngame();
                controller.CapturePhase = BaseLiveController.MusicPhase.Ended;
            }
        }


        internal static string ResolvePackage(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new InvalidOperationException("A live/info shot needs a package.");
            }
            if (Directory.Exists(query))
            {
                return Path.GetFullPath(query);
            }

            var candidates = new List<(string dir, string id, string title)>();
            foreach (SongPackageItem item in SongPackageManager.Shared.LoadItems())
            {
                if (item?.Entry?.RootDirectory != null)
                {
                    candidates.Add((item.Entry.RootDirectory, item.Entry.Manifest?.id, item.Entry.Manifest?.title));
                }
            }
            string fixtures = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/fixtures"));
            if (Directory.Exists(fixtures))
            {
                foreach (string dir in Directory.GetDirectories(fixtures))
                {
                    if (File.Exists(Path.Combine(dir, "manifest.json")))
                    {
                        candidates.Add((dir, null, null));
                    }
                }
            }

            bool Is(string a) => string.Equals(a, query, StringComparison.OrdinalIgnoreCase);
            bool Has(string a) => a != null && a.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            var match = candidates.FirstOrDefault(c => Is(c.id) || Is(Path.GetFileName(c.dir)) || Is(c.title));
            if (match.dir == null) match = candidates.FirstOrDefault(c => Has(Path.GetFileName(c.dir)) || Has(c.title));
            if (match.dir == null)
            {
                throw new InvalidOperationException("No package matches '" + query + "'. Known: "
                    + string.Join(", ", candidates.Select(c => Path.GetFileName(c.dir))));
            }
            return match.dir;
        }

        // ---------------------------------------------------------------- shared

        private static IEnumerator WaitForLibrary()
        {
            return WaitUntil(() =>
            {
                LibraryScreen screen = UnityEngine.Object.FindFirstObjectByType<LibraryScreen>();
                // A cold start opens on the splash and the home page, with the library built
                // under them; library shots want the library itself on screen.
                if (screen != null && !screen.IsPresented && HomeScreen.Current != null && !PlayService.Instance.IsStarting)
                {
                    HomeScreen.Current.ShowLibraryImmediately();
                }

                return screen != null && screen.IsReady && screen.IsPresented && !PlayService.Instance.IsStarting
                    && SceneManager.Instance.CurrentScene == SceneManager.Scene.Library
                    && UnityEngine.Object.FindFirstObjectByType<SoloLiveController>() == null;
            }, "the library page");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + REAL_TIMEOUT;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    throw new TimeoutException("Timed out waiting for " + what + ".");
                }
                yield return null;
            }
        }

        private IEnumerator Capture(Shot shot, string stem, string detail, string note, float? time = null)
        {
            yield return new WaitForEndOfFrame();
            CaptureFrame(shot, stem, detail, note, time);
        }

        private void CaptureFrame(Shot shot, string stem, string detail, string note, float? time = null)
        {
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(_request.outDir, stem + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            var report = new ShotReport
            {
                ok = true,
                kind = shot.kind,
                file = Path.GetFileName(path),
                width = texture.width,
                height = texture.height,
                time = time,
                detail = detail,
                note = note
            };
            if (texture.width != _request.width || texture.height != _request.height)
            {
                report.note = (note == null ? string.Empty : note + "; ") + "captured at " + texture.width + "x" + texture.height
                    + " instead of " + _request.width + "x" + _request.height;
            }
            UnityEngine.Object.Destroy(texture);
            _reports.Add(report);
            Log("wrote " + report.file + " (" + report.width + "x" + report.height + ")" + (detail != null ? " " + detail : string.Empty)
                + (report.note != null ? " NOTE " + report.note : string.Empty));
        }

        private void Fail(Shot shot, string message)
        {
            _reports.Add(new ShotReport { ok = false, kind = shot.kind, file = FileStem(shot, null), note = message });
            Debug.LogError("[Screenshots] " + (shot.kind ?? "?") + " failed: " + message);
        }

        private static string FileStem(Shot shot, float? time)
        {
            string stem = string.IsNullOrWhiteSpace(shot.name) ? (shot.kind ?? "shot") : shot.name;
            if (time.HasValue)
            {
                stem += "_" + time.Value.ToString("000.000", System.Globalization.CultureInfo.InvariantCulture) + "s";
            }
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                stem = stem.Replace(c, '_');
            }
            return stem;
        }

        private static void Log(string message) => Debug.Log("[Screenshots] " + message);
    }

    [Serializable]
    internal sealed class RunReport
    {
        public string outDir;
        public int width;
        public int height;
        public int screenWidth;
        public int screenHeight;
        public List<ShotReport> shots;
    }

    [Serializable]
    internal sealed class ShotReport
    {
        public bool ok;
        public string kind;
        public string file;
        public int width;
        public int height;
        public float? time;
        public string detail;
        public string note;
    }
}
