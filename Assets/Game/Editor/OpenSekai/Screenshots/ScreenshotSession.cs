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
        private const string LibraryScene = "Assets/Game/Scenes/Library.unity";
        private const string KeyRequest = "OpenSekai.Screenshots.Request";
        private const string KeyRunning = "OpenSekai.Screenshots.Running";
        private const string KeyStartedPlay = "OpenSekai.Screenshots.StartedPlay";
        private const string KeyGameViewIndex = "OpenSekai.Screenshots.GameViewIndex";
        private const string KeyRunInBackground = "OpenSekai.Screenshots.RunInBackground";
        private const string KeyQuit = "OpenSekai.Screenshots.Quit";
        private const string KeyExitCode = "OpenSekai.Screenshots.ExitCode";

        /// <summary>Written next to the request while a run is going and replaced by the final status.</summary>
        public const string StatusFileName = "status.json";

        static ScreenshotSession()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!string.IsNullOrEmpty(SessionState.GetString(KeyRequest, string.Empty)))
            {
                EditorApplication.update += Tick;
                // A reload in Edit Mode with a run still recorded means Play Mode was left under it
                // (a script recompile stops Play Mode): close the run instead of staying busy forever.
                EditorApplication.delayCall += () =>
                {
                    string requestPath = SessionState.GetString(KeyRequest, string.Empty);
                    if (!string.IsNullOrEmpty(requestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        ShotRequest request = TryLoad(requestPath);
                        Finish(requestPath, "failed", request?.outDir, "Play Mode ended (script reload?) before the run finished.", 1);
                        Cleanup();
                    }
                };
            }
        }

        public static bool IsBusy => !string.IsNullOrEmpty(SessionState.GetString(KeyRequest, string.Empty));

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
            if (EditorApplication.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != LibraryScene
                && Sekai.SceneManager.Instance.CurrentScene != Sekai.SceneManager.Scene.Library)
            {
                throw new InvalidOperationException("Play Mode is running outside the Library scene; stop it first.");
            }

            WriteStatus(requestPath, "running", request.outDir, null);
            SessionState.SetString(KeyRequest, requestPath);
            SessionState.SetBool(KeyQuit, request.quitWhenDone);
            SessionState.SetInt(KeyGameViewIndex, GameViewResolution.Apply(request.width, request.height));
            SessionState.SetBool(KeyRunInBackground, Application.runInBackground);
            Application.runInBackground = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(KeyStartedPlay, false);
                BeginDriver();
                return "running in the current Play Mode session; output " + request.outDir;
            }

            if (EditorSceneManager.GetSceneManagerSetup().All(s => s.path != LibraryScene) || EditorSceneManager.GetActiveScene().path != LibraryScene)
            {
                if (Enumerable.Range(0, EditorSceneManager.sceneCount).Any(i => EditorSceneManager.GetSceneAt(i).isDirty))
                {
                    Finish(requestPath, "failed", request.outDir, "An open scene has unsaved changes; save it or open Library.unity first.", 1);
                    throw new InvalidOperationException("An open scene has unsaved changes.");
                }
                EditorSceneManager.OpenScene(LibraryScene, OpenSceneMode.Single);
            }
            SessionState.SetBool(KeyStartedPlay, true);
            EditorApplication.isPlaying = true;
            EditorApplication.delayCall += () =>
            {
                // The editor can refuse Play Mode (a modal dialog, a failed import); don't stay busy.
                if (IsBusy && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Finish(requestPath, "failed", request.outDir, "The editor did not enter Play Mode.", 1);
                    Cleanup();
                }
            };
            return "entering Play Mode; output " + request.outDir;
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
                    new Shot { kind = "info", package = package, times = { 0.6f, 1.5f, 3f, 5.9f } },
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
            string requestPath = SessionState.GetString(KeyRequest, string.Empty);
            if (string.IsNullOrEmpty(requestPath))
            {
                return;
            }

            if (change == PlayModeStateChange.EnteredPlayMode && !SessionState.GetBool(KeyRunning, false))
            {
                BeginDriver();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (SessionState.GetBool(KeyRunning, false))
                {
                    // Play Mode was stopped under the run.
                    ShotRequest request = TryLoad(requestPath);
                    Finish(requestPath, "failed", request?.outDir, "Play Mode stopped before the run finished.", 1);
                }
                Cleanup();
            }
        }

        private static void BeginDriver()
        {
            string requestPath = SessionState.GetString(KeyRequest, string.Empty);
            ShotRequest request;
            try
            {
                request = ShotRequest.Load(requestPath);
            }
            catch (Exception exception)
            {
                Finish(requestPath, "failed", null, exception.Message, 1);
                EndPlay();
                return;
            }

            SessionState.SetBool(KeyRunning, true);
            try
            {
                ScreenshotDriver.Begin(request, report =>
                {
                    int failed = report.shots.Count(s => !s.ok);
                    SessionState.SetBool(KeyRunning, false);
                    Finish(requestPath, failed == 0 ? "done" : "failed", request.outDir,
                        failed == 0 ? null : failed + " shot(s) failed; see report.json", failed == 0 ? 0 : 1);
                    EndPlay();
                });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(KeyRunning, false);
                Finish(requestPath, "failed", request.outDir, exception.Message, 1);
                EndPlay();
            }
        }

        private static void EndPlay()
        {
            if (SessionState.GetBool(KeyStartedPlay, false) && EditorApplication.isPlaying)
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
            bool quit = SessionState.GetBool(KeyQuit, false);
            int exitCode = SessionState.GetInt(KeyExitCode, 0);
            GameViewResolution.Restore(SessionState.GetInt(KeyGameViewIndex, -1));
            Application.runInBackground = SessionState.GetBool(KeyRunInBackground, Application.runInBackground);
            EditorApplication.update -= Tick;
            foreach (string key in new[] { KeyRequest, KeyRunning, KeyStartedPlay, KeyGameViewIndex, KeyRunInBackground, KeyQuit, KeyExitCode })
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
            SessionState.SetInt(KeyExitCode, exitCode);
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
            string path = Path.Combine(Path.GetDirectoryName(requestPath), Path.GetFileNameWithoutExtension(requestPath) + "." + StatusFileName);
            File.WriteAllText(path, JsonConvert.SerializeObject(new { status, outDir, message, at = DateTime.Now.ToString("o") }, Formatting.Indented));
        }

        private static ShotRequest TryLoad(string path)
        {
            try { return ShotRequest.Load(path); }
            catch { return null; }
        }
    }
}
