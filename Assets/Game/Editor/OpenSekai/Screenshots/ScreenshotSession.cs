using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenSekai.EditorTools.Screenshots
{
    /// <summary>
    /// Entry points of the screenshot tool. A run puts the Game view on the requested resolution,
    /// enters Play Mode on the Library scene, hands the request to <see cref="ScreenshotDriver"/>,
    /// and puts everything back afterwards. The pending run is kept in SessionState because entering
    /// Play Mode reloads the domain.
    ///
    ///   Open editor:   OpenSekai.EditorTools.Screenshots.ScreenshotSession.Start("path/to/request.json")
    ///                  (Tools/capture-screens.ps1 sends this through `unity command script eval`)
    ///   Command line:  Unity.exe -projectPath . -executeMethod
    ///                  OpenSekai.EditorTools.Screenshots.ScreenshotSession.CaptureFromCommandLine
    ///                  -shotRequest path/to/request.json
    ///   Menu:          OpenSekai/Screenshots/...
    /// </summary>
    [InitializeOnLoad]
    public static class ScreenshotSession
    {
        private const string LIBRARY_SCENE = "Assets/Game/Scenes/Library.unity";
        private const string KEY_REQUEST = "OpenSekai.Screenshots.Request";
        private const string KEY_RUNNING = "OpenSekai.Screenshots.Running";
        private const string KEY_STARTED_PLAY = "OpenSekai.Screenshots.StartedPlay";
        private const string KEY_GAME_VIEW_INDEX = "OpenSekai.Screenshots.GameViewIndex";
        private const string KEY_RUN_IN_BACKGROUND = "OpenSekai.Screenshots.RunInBackground";
        private const string KEY_QUIT = "OpenSekai.Screenshots.Quit";
        private const string KEY_EXIT_CODE = "OpenSekai.Screenshots.ExitCode";

        /// <summary>Written next to the request while a run is going and replaced by the final status.</summary>
        public const string STATUS_FILE_NAME = "status.json";

        static ScreenshotSession()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!string.IsNullOrEmpty(SessionState.GetString(KEY_REQUEST, string.Empty)))
            {
                EditorApplication.update += Tick;
                // A reload in Edit Mode with a run still recorded means Play Mode was left under it
                // (a script recompile stops Play Mode): close the run instead of staying busy forever.
                EditorApplication.delayCall += () =>
                {
                    string requestPath = SessionState.GetString(KEY_REQUEST, string.Empty);
                    if (!string.IsNullOrEmpty(requestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        ShotRequest request = TryLoad(requestPath);
                        try { Finish(requestPath, "failed", request?.outDir, "Play Mode ended (script reload?) before the run finished.", 1); }
                        finally { Cleanup(); }
                    }
                };
            }
        }

        public static bool IsBusy => !string.IsNullOrEmpty(SessionState.GetString(KEY_REQUEST, string.Empty));

        /// <summary>
        /// Starts a run from a request file. Returns at once; the result is in the request's
        /// outDir/report.json and the status file next to the request.
        /// </summary>
        public static string Start(string requestPath)
        {
            requestPath = Path.GetFullPath(requestPath);
            ShotRequest request = ShotRequest.Load(requestPath);
            if (IsBusy)
            {
                throw new InvalidOperationException("A screenshot run is already in progress.");
            }
            if (EditorUtility.scriptCompilationFailed)
            {
                WriteStatus(requestPath, "failed", request.outDir, "Scripts have compile errors; Play Mode cannot start.");
                throw new InvalidOperationException("Scripts have compile errors; Play Mode cannot start.");
            }
            if (EditorApplication.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != LIBRARY_SCENE
                && Sekai.SceneManager.Instance.CurrentScene != Sekai.SceneManager.Scene.Library)
            {
                throw new InvalidOperationException("Play Mode is running outside the Library scene; stop it first.");
            }

            bool needsLibrary = !EditorApplication.isPlaying && EditorSceneManager.GetActiveScene().path != LIBRARY_SCENE;
            if (needsLibrary && Enumerable.Range(0, EditorSceneManager.sceneCount).Any(i => EditorSceneManager.GetSceneAt(i).isDirty))
            {
                const string UNSAVED_SCENE_MESSAGE = "An open scene has unsaved changes; save it before starting a screenshot run.";
                WriteStatus(requestPath, "failed", request.outDir, UNSAVED_SCENE_MESSAGE);
                throw new InvalidOperationException(UNSAVED_SCENE_MESSAGE);
            }
            try
            {
                WriteStatus(requestPath, "running", request.outDir, null);
                SessionState.SetString(KEY_REQUEST, requestPath);
                SessionState.SetBool(KEY_QUIT, request.quitWhenDone);
                SessionState.SetBool(KEY_RUN_IN_BACKGROUND, Application.runInBackground);
                SessionState.SetInt(KEY_GAME_VIEW_INDEX, GameViewResolution.Apply(request.width, request.height));
                Application.runInBackground = true;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;

                if (EditorApplication.isPlaying)
                {
                    SessionState.SetBool(KEY_STARTED_PLAY, false);
                    BeginDriver();
                    return "running in the current Play Mode session; output " + request.outDir;
                }

                if (needsLibrary)
                {
                    EditorSceneManager.OpenScene(LIBRARY_SCENE, OpenSceneMode.Single);
                }
                SessionState.SetBool(KEY_STARTED_PLAY, true);
                EditorApplication.isPlaying = true;
                EditorApplication.delayCall += () =>
                {
                    // The editor can refuse Play Mode (a modal dialog, a failed import); don't stay busy.
                    if (IsBusy && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        try { Finish(requestPath, "failed", request.outDir, "The editor did not enter Play Mode.", 1); }
                        finally { Cleanup(); }
                    }
                };
                return "entering Play Mode; output " + request.outDir;
            }
            catch (Exception exception)
            {
                try { Finish(requestPath, "failed", request.outDir, exception.Message, 1); }
                finally { Cleanup(); }
                throw;
            }
        }

        /// <summary>For <c>-executeMethod</c>: <c>-shotRequest &lt;file&gt;</c>, then quits with 0 when every shot worked.</summary>
        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-shotRequest");
            if (at < 0 || at + 1 >= args.Length)
            {
                Debug.LogError("[Screenshots] -shotRequest <file> is required.");
                EditorApplication.Exit(2);
                return;
            }
            string path = Path.GetFullPath(args[at + 1]);
            ShotRequest request = ShotRequest.Load(path);
            if (!request.quitWhenDone)
            {
                // A command-line run always quits; keep the request honest about it.
                request.quitWhenDone = true;
                File.WriteAllText(path, JsonConvert.SerializeObject(request, Formatting.Indented));
            }
            try
            {
                Start(path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("OpenSekai/Screenshots/Capture Sample Set (2800x1272)", priority = 300)]
        private static void CaptureSamplePhone() => StartSample(2800, 1272);

        [MenuItem("OpenSekai/Screenshots/Capture Sample Set (1750x795)", priority = 301)]
        private static void CaptureSampleSmall() => StartSample(1750, 795);

        [MenuItem("OpenSekai/Screenshots/Open Last Output", priority = 320)]
        private static void OpenLastOutput()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/screenshots"));
            string last = Directory.Exists(root) ? Directory.GetDirectories(root).OrderBy(d => d).LastOrDefault() : null;
            if (last != null) EditorUtility.RevealInFinder(last);
        }

        private static void StartSample(int width, int height)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string package = OpenSekai.Infrastructure.Packages.SongPackageManager.Shared.LoadItems()
                .Select(i => i?.Entry?.Manifest?.id).FirstOrDefault(id => !string.IsNullOrEmpty(id)) ?? "baseline_tap";
            var request = new ShotRequest
            {
                outDir = "TestResults/screenshots/" + stamp,
                width = width,
                height = height,
                shots =
                {
                    new Shot { kind = "library" },
                    new Shot { kind = "settings" },
                    new Shot { kind = "result", result = ResultSample.Preset("fc") },
                    new Shot { kind = "info", package = package, times = { 0.3f, 0.8f, 1.5f, 2.7f } },
                    new Shot { kind = "live", package = package, times = { 20f, 30f } }
                }
            };
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/OpenSekaiShots/request-" + stamp + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(request, Formatting.Indented));
            Debug.Log("[Screenshots] " + Start(path));
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            string requestPath = SessionState.GetString(KEY_REQUEST, string.Empty);
            if (string.IsNullOrEmpty(requestPath))
            {
                return;
            }

            if (change == PlayModeStateChange.EnteredPlayMode && !SessionState.GetBool(KEY_RUNNING, false))
            {
                BeginDriver();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                ScreenshotDriver.StopMotionClock();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    if (SessionState.GetBool(KEY_RUNNING, false))
                    {
                        ShotRequest request = TryLoad(requestPath);
                        Finish(requestPath, "failed", request?.outDir, "Play Mode stopped before the run finished.", 1);
                    }
                }
                finally { Cleanup(); }
            }
        }

        private static void BeginDriver()
        {
            string requestPath = SessionState.GetString(KEY_REQUEST, string.Empty);
            ShotRequest request;
            try
            {
                request = ShotRequest.Load(requestPath);
            }
            catch (Exception exception)
            {
                try { Finish(requestPath, "failed", null, exception.Message, 1); }
                finally { EndPlay(); }
                return;
            }

            SessionState.SetBool(KEY_RUNNING, true);
            try
            {
                ScreenshotDriver.Begin(request, report =>
                {
                    int failed = report.shots.Count(s => !s.ok);
                    SessionState.SetBool(KEY_RUNNING, false);
                    try
                    {
                        Finish(requestPath, failed == 0 ? "done" : "failed", request.outDir,
                            failed == 0 ? null : failed + " shot(s) failed; see report.json", failed == 0 ? 0 : 1);
                    }
                    finally { EndPlay(); }
                });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(KEY_RUNNING, false);
                try { Finish(requestPath, "failed", request.outDir, exception.Message, 1); }
                finally { EndPlay(); }
            }
        }

        private static void EndPlay()
        {
            if (SessionState.GetBool(KEY_STARTED_PLAY, false) && EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false; // Cleanup runs on EnteredEditMode.
            }
            else
            {
                Cleanup();
            }
        }

        private static void Cleanup()
        {
            bool quit = SessionState.GetBool(KEY_QUIT, false);
            int exitCode = SessionState.GetInt(KEY_EXIT_CODE, 0);
            try { GameViewResolution.Restore(SessionState.GetInt(KEY_GAME_VIEW_INDEX, -1)); }
            catch (Exception error) { Debug.LogException(error); }
            Application.runInBackground = SessionState.GetBool(KEY_RUN_IN_BACKGROUND, Application.runInBackground);
            EditorApplication.update -= Tick;
            foreach (string key in new[] { KEY_REQUEST, KEY_RUNNING, KEY_STARTED_PLAY, KEY_GAME_VIEW_INDEX, KEY_RUN_IN_BACKGROUND, KEY_QUIT, KEY_EXIT_CODE })
            {
                SessionState.EraseString(key);
                SessionState.EraseBool(key);
                SessionState.EraseInt(key);
            }
            if (quit)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>An unfocused editor throttles the player loop; keep it stepping while a run is on.</summary>
        private static void Tick()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private static void Finish(string requestPath, string status, string outDir, string message, int exitCode)
        {
            SessionState.SetInt(KEY_EXIT_CODE, exitCode);
            WriteStatus(requestPath, status, outDir, message);
            if (message != null)
            {
                Debug.LogWarning("[Screenshots] " + status + ": " + message);
            }
        }

        private static void WriteStatus(string requestPath, string status, string outDir, string message)
        {
            if (string.IsNullOrEmpty(requestPath))
            {
                return;
            }
            string path = Path.Combine(Path.GetDirectoryName(requestPath), Path.GetFileNameWithoutExtension(requestPath) + "." + STATUS_FILE_NAME);
            File.WriteAllText(path, JsonConvert.SerializeObject(new { status, outDir, message, at = DateTime.Now.ToString("o") }, Formatting.Indented));
        }

        private static ShotRequest TryLoad(string path)
        {
            try { return ShotRequest.Load(path); }
            catch { return null; }
        }
    }
}
