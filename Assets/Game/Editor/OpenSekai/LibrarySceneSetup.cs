using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OpenSekai.App.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using Sekai;
using Sekai.Core;

namespace OpenSekai.EditorTools
{
    public static class LibrarySceneSetup
    {
        public const string SCENE_PATH = "Assets/Game/Scenes/Library.unity";
        private static bool _startupCheckActive;
        private static bool _previousRunInBackground;

        [MenuItem("OpenSekai/Library/Create or Update Scene")]
        public static void CreateOrUpdateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play mode before configuring the Library scene.");
            }
            if (Enumerable.Range(0, UnitySceneManager.sceneCount)
                .Any(i => UnitySceneManager.GetSceneAt(i).isDirty))
            {
                throw new InvalidOperationException("Save the open scene changes before configuring Library.");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Game/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets/Game", "Scenes");
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE_PATH) == null)
            {
                throw new InvalidOperationException("The Library scene is missing at " + SCENE_PATH + ".");
            }

            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            var entryObject = scene.GetRootGameObjects().Single(root => root.name == "EntryPoint");
            // The editor entry point is matched by type name so this tool does not depend on the
            // editor assembly types it is meant to strip.
            foreach (var legacyEntry in entryObject.GetComponents<MonoBehaviour>())
            {
                if (legacyEntry != null && legacyEntry.GetType().Name == "MusicScoreMakerEntryPoint")
                {
                    UnityEngine.Object.DestroyImmediate(legacyEntry);
                }
            }
            if (entryObject.GetComponent<LibraryEntryPoint>() == null)
            {
                entryObject.AddComponent<LibraryEntryPoint>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Could not save Library.");
            }

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(SCENE_PATH, true) };
            scenes.AddRange(EditorBuildSettings.scenes.Where(existing => existing.path != SCENE_PATH));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[LibrarySetup] Library saved and registered as the first build scene; legacy scenes preserved.");
        }

        [MenuItem("OpenSekai/Library/Begin Startup Check")]
        public static void BeginStartupCheck()
        {
            if (!EditorApplication.isPlaying || _startupCheckActive)
            {
                return;
            }
            _previousRunInBackground = Application.runInBackground;
            _startupCheckActive = true;
            Application.runInBackground = true;
            EditorApplication.update += TickStartupCheck;
            EditorApplication.playModeStateChanged += RestoreStartupCheck;
        }

        private static void TickStartupCheck()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private static void RestoreStartupCheck(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode || !_startupCheckActive)
            {
                return;
            }
            Application.runInBackground = _previousRunInBackground;
            EditorApplication.update -= TickStartupCheck;
            EditorApplication.playModeStateChanged -= RestoreStartupCheck;
            _startupCheckActive = false;
        }

        [MenuItem("OpenSekai/Library/Verify Running Scene")]
        public static void VerifyRunningScene()
        {
            if (!EditorApplication.isPlaying || UnitySceneManager.GetActiveScene().path != SCENE_PATH)
            {
                throw new InvalidOperationException("Run Library before checking its startup.");
            }

            var entries = UnityEngine.Object.FindObjectsByType<LibraryEntryPoint>(FindObjectsSortMode.None);
            int legacyEntries = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Count(component => component != null && component.GetType().Name == "MusicScoreMakerEntryPoint");
            var libraryScreens = UnityEngine.Object.FindObjectsByType<LibraryScreen>(FindObjectsSortMode.None);
            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            bool valid = entries.Length == 1 && legacyEntries == 0
                && libraryScreens.Length == 1 && eventSystems.Length == 1
                && !PlayService.Instance.IsStarting
                && canvases.Any(canvas => canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null);
            string report = JsonConvert.SerializeObject(new
            {
                valid,
                scene = UnitySceneManager.GetActiveScene().name,
                route = Sekai.SceneManager.Instance.CurrentScene.ToString(),
                frameCount = Time.frameCount,
                libraryEntries = entries.Length,
                editorEntries = legacyEntries,
                lightweightLibraryScreens = libraryScreens.Length,
                eventSystems = eventSystems.Length,
                isStartingPlayback = PlayService.Instance.IsStarting
            });
            if (!valid)
            {
                throw new InvalidOperationException("Library startup check failed: " + report);
            }
            Debug.Log("[LibraryCheck] " + report);
        }
    }
}
