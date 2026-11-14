using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Sekai;
using OpenSekai.App.Library;
using UnityEditor;
using UnityEngine;
using CriWare;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using OpenSekai.App.UI;
using Sekai.Core.Live;

namespace OpenSekai.EditorTools
{
    /// <summary>Optional end-to-end check using the local synthetic baseline package.</summary>
    public static class LibraryPlayCheck
    {
        private const string BaselinePackageId = "codexbaseline20261101tap";

        [MenuItem("OpenSekai/Library/Play Local Baseline Automatically")]
        public static void PlayLocalBaseline()
        {
            PlayLocalBaselineAsync(true).Forget();
        }

        [MenuItem("OpenSekai/Library/Play Local Baseline Manually")]
        public static void PlayLocalBaselineManually()
        {
            PlayLocalBaselineAsync(false).Forget();
        }

        [MenuItem("OpenSekai/Library/Retire Local Playback Check")]
        public static void RetireLocalPlaybackCheck()
        {
            var boot = UserDataManager.Instance.FreeLiveBootData;
            var controller = UnityEngine.Object.FindFirstObjectByType<Sekai.Core.Live.SoloLiveController>();
            if (controller == null || boot?.CustomMusicScoreId != BaselinePackageId)
            {
                throw new InvalidOperationException("No local synthetic playback check is running.");
            }
            controller.Retire();
        }

        private static async UniTask PlayLocalBaselineAsync(bool isAuto)
        {
            if (!EditorApplication.isPlaying || SceneManager.Instance.CurrentScene != SceneManager.Scene.Library)
            {
                throw new InvalidOperationException("Run Library before starting its local playback check.");
            }

            CustomMusicScoreEntry entry = ResolveBaselineEntry();
            if (entry == null)
            {
                throw new InvalidOperationException(
                    "No package with id '" + BaselinePackageId + "' was found in " + SongPackageStorage.RootDirectory
                    + " (nor in Tools/fixtures/baseline_tap).");
            }
            // The lightweight library page is the only host left in the Library scene.
            LibraryScreen libraryPage = UnityEngine.Object.FindFirstObjectByType<LibraryScreen>();
            if (libraryPage == null)
            {
                throw new InvalidOperationException("The library screen is not ready.");
            }

            LibrarySceneSetup.BeginStartupCheck();
            PlayStartResult result = await PlayService.Instance.StartAsync(
                new PlayRequest(entry.RootDirectory, isAuto, SceneManager.Scene.Library), libraryPage.GetCancellationTokenOnDestroy());
            if (!result.Started)
            {
                throw new InvalidOperationException("Local playback did not start: " + result.Message);
            }
            var boot = UserDataManager.Instance.FreeLiveBootData;
            if (boot == null || boot.IsAuto != isAuto || boot.ReturnScene != SceneManager.Scene.Library
                || boot.MusicData.IsTestPlay)
            {
                throw new InvalidOperationException("Playback request was not transferred correctly.");
            }
            Debug.Log("[LibraryPlayCheck] " + (isAuto ? "Automatic" : "Manual")
                + " playback started through PlayService; expected return scene: Library.");
        }

        /// <summary>
        /// Resolves the test package the same way the library page does: whatever lives under the
        /// player's CustomMusicScores folder with the fixture id wins, so a hand edited chart is what
        /// the check actually plays. Falls back to the repository fixture when nothing is installed.
        /// </summary>
        private static CustomMusicScoreEntry ResolveBaselineEntry()
        {
            foreach (SongPackageItem item in SongPackageManager.Shared.LoadItems())
            {
                if (item?.Entry?.Manifest?.id == BaselinePackageId)
                {
                    return CustomMusicScoreStorage.LoadEntry(item.Entry.RootDirectory);
                }
            }

            string repositoryFixture = Path.GetFullPath(Path.Combine(
                Application.dataPath, "../Tools/fixtures/baseline_tap"));
            return Directory.Exists(repositoryFixture) ? CustomMusicScoreStorage.LoadEntry(repositoryFixture) : null;
        }
    }
}
