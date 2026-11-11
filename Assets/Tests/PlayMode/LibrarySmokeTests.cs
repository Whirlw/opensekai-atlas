using System.IO;
using System.Reflection;
using System.Threading;
using System.Collections;
using NUnit.Framework;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core.Live;
using GameSceneManager = Sekai.SceneManager;
using UnityEngine;
using SceneLoader = UnityEngine.SceneManagement.SceneManager;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    /// <summary>Exercises Library scene assembly and baseline package playback.</summary>
    public class LibrarySmokeTests
    {
        private const string LibraryScenePath = "Assets/Game/Scenes/Library.unity";
        private const float BootTimeoutSeconds = 15f;

        [UnityTest]
        public IEnumerator LibrarySceneBootsAndShowsThePage()
        {
            AsyncOperation load = SceneLoader.LoadSceneAsync(LibraryScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "the library scene must exist at " + LibraryScenePath);

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            LibraryScreen screen = null;

            while (Time.realtimeSinceStartup < deadline)
            {
                screen = Object.FindObjectOfType<LibraryScreen>();
                if (screen != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(screen, Is.Not.Null, "LibraryScreen was not created within " + BootTimeoutSeconds + " s");
            Assert.That(ScreenManager.ExistsInstance, Is.True, "ScreenManager should be alive after boot");
        }

        [UnityTest]
        [Explicit("Whole-song prototype for P4-3 integration.")]
        public IEnumerator BaselineTapAutoplaysAndReturnsToTheLibrary()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            var originalLibrary = Object.FindFirstObjectByType<LibraryScreen>();
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/fixtures/baseline_tap"));
            var request = new PlayRequest(fixture, true, GameSceneManager.Scene.Library);
            using var cancellation = new CancellationTokenSource();
            var start = PlayService.Instance.StartAsync(request, cancellation.Token).GetAwaiter();
            yield return WaitFor(() => start.IsCompleted, BootTimeoutSeconds, "starting baseline playback");
            var started = start.GetResult();
            Assert.That(started.Started, Is.True, started.Message);

            yield return WaitFor(
                () => SceneLoader.GetActiveScene().name == "Core" && Object.FindFirstObjectByType<SoloLiveController>() != null,
                BootTimeoutSeconds, "loading Core and creating the live controller");

            yield return WaitFor(
                () => SceneLoader.GetActiveScene().name == "Library" && Object.FindFirstObjectByType<LibraryResultPanel>() != null,
                90f, "returning to Library and showing the result");
            var returnedLibrary = Object.FindFirstObjectByType<LibraryScreen>();
            Assert.That(returnedLibrary, Is.Not.Null);
            Assert.That(originalLibrary == null, Is.True, "the original Library scene should have unloaded");

            var panel = Object.FindFirstObjectByType<LibraryResultPanel>();
            var result = (PlayResult)typeof(LibraryResultPanel)
                .GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Kind, Is.EqualTo(PlayResultKind.Completed));
            Assert.That(result.IsAuto, Is.True);
            Assert.That(result.Score, Is.EqualTo(1_000_000),
                $"Rank={result.Rank}, auto={result.AutoCount}/{result.TotalCombo}, misses={result.Miss}");
            Assert.That(result.Rank, Is.EqualTo(ScoreRank.S));
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout, string stage)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(condition(), Is.True,
                $"Timed out {stage}. Active scene: {SceneLoader.GetActiveScene().name}");
        }
    }
}
