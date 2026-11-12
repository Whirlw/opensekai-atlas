using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core.Live;
using UnityEngine;
using SceneLoader = UnityEngine.SceneManagement.SceneManager;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
            Assert.That(screen.IsReady, Is.True, "the library view should be assembled");
            Assert.That(ScreenManager.ExistsInstance, Is.True, "ScreenManager should be alive after boot");
        }

        [UnityTest]
        public IEnumerator BaselineTapAutoplaysAndReturnsToTheLibrary()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            var originalLibrary = Object.FindFirstObjectByType<LibraryScreen>();
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/fixtures/baseline_tap"));
            originalLibrary.ShowResult(PlayResult.FromLoadError("Baseline Tap", true, fixture, "Retry smoke setup"));
            var retryPanel = Object.FindFirstObjectByType<LibraryResultPanel>();
            Assert.That(retryPanel, Is.Not.Null, "the library should display the retry panel");
            var retry = retryPanel.transform.Find("Panel/Retry")?.GetComponent<Button>();
            Assert.That(retry, Is.Not.Null, "the result panel should wire its retry button");
            retry.onClick.Invoke();

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

        [UnityTest]
        public IEnumerator ShippedWavePackRegistersEveryManifestCue()
        {
            var sound = new SoundManager();
            bool loaded = false;
            sound.LoadExternalSoundPack(() => loaded = true);
            yield return WaitFor(() => loaded, BootTimeoutSeconds, "loading the built-in WAV manifest");

            string manifest = Path.Combine(Application.streamingAssetsPath, "sound", "manifest.csv");
            var bundles = File.ReadLines(manifest).Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim().Split(','))
                .GroupBy(row => row[0].Trim(), row => row[1].Trim());

            foreach (var bundle in bundles)
            {
                Assert.That(sound.IsLoadedSoundBundle(bundle.Key), Is.True, bundle.Key);
                var cues = sound.GetCueInfos(bundle.Key);
                Assert.That(cues.Select(cue => cue.name), Is.EquivalentTo(bundle), bundle.Key);
                Assert.That(cues.All(cue => cue.length > 0), Is.True, bundle.Key);
            }

            uint playback = sound.PlaySE("se_live_clear");
            Assert.That(playback, Is.Not.Zero, "the result cue can start playback");
            sound.StopSE(playback);

            const string missing = "__opensekai_missing_sound_fixture__";
            Assert.That(sound.ExistsCueName(missing), Is.False);
            string message = missing + " is not registered.";
            int warningCount = 0;
            void CountMissingCueWarning(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition == message)
                    warningCount++;
            }

            Application.logMessageReceived += CountMissingCueWarning;
            try
            {
                LogAssert.Expect(LogType.Warning, message);
                Assert.That(sound.PlaySE(missing), Is.Zero);
                Assert.That(sound.PlaySE(missing), Is.Zero);
                Assert.That(warningCount, Is.EqualTo(1));
            }
            finally
            {
                Application.logMessageReceived -= CountMissingCueWarning;
            }
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
