using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using OpenSekai.App;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Live.Simulation;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core.Live;
using UnityEngine;
using SceneLoader = UnityEngine.SceneManagement.SceneManager;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

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
            if (SceneLoader.GetActiveScene().path != LibraryScenePath)
            {
                AsyncOperation load = SceneLoader.LoadSceneAsync(LibraryScenePath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "the library scene must exist at " + LibraryScenePath);
                yield return load;
            }

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
            Assert.That(HomeScreen.Current, Is.Not.Null, "the library scene provides the home page");
            if (!screen.IsPresented)
                HomeScreen.Current.ShowLibraryImmediately();
            Assert.That(screen.IsPresented, Is.True);
            var back = screen.transform.Find("TopBar/Back")?.GetComponent<Button>();
            Assert.That(back, Is.Not.Null, "the library should reserve a back button in its top bar");
            Assert.That(back.interactable, Is.True, "the back button can return to the home page");
            Assert.That(ScreenManager.ExistsInstance, Is.True, "ScreenManager should be alive after boot");
            // Without an EventSystem the page renders but nothing is clickable: uGUI pointer events
            // never fire. The only EventSystem in the project is the one this boot path instantiates.
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.Not.Null,
                "the library boot must register an EventSystem for pointer input");
        }

        [UnityTest]
        public IEnumerator PlayingPreviewSurvivesViewSwitchAndStopsWhenTheSelectionBecomesUnplayable()
        {
            string root = Path.Combine(Path.GetTempPath(), "OpenSekai-LibraryPreview-" + System.Guid.NewGuid().ToString("N"));
            LibraryScreen screen = null;
            try
            {
                var packages = new SongPackageManager(root);
                string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/fixtures/baseline_tap"));
                packages.ImportFolder(fixture);
                var missing = packages.CreateNewEntry();
                missing.Manifest.title = "ZZZ 缺少音频";
                packages.SaveManifest(missing, missing.Manifest);

                screen = LibraryScreen.Create(new AppContext(packages, PlayService.Instance));
                AudioSource audio = null;
                var preview = screen.transform.Find("Main/DetailColumn/Detail/Info/Preview");
                yield return WaitFor(() =>
                        (audio = screen.GetComponents<AudioSource>().FirstOrDefault(source => source.isPlaying)) != null &&
                        preview.gameObject.activeInHierarchy,
                    BootTimeoutSeconds, "playing the selected song preview");
                AudioClip initialClip = audio.clip;
                var length = screen.transform.Find("Main/DetailColumn/Detail/Info/Length").GetComponent<TMP_Text>();
                Assert.That(length.text, Is.Not.Empty);

                var gridButton = screen.transform.Find("TopBar/Controls/ViewMode/Grid").GetComponent<Button>();
                gridButton.onClick.Invoke();
                Assert.That(screen.transform.Find("Main/ListColumn/ListArea/Tiles").gameObject.activeInHierarchy, Is.True);
                Assert.That(audio.clip, Is.SameAs(initialClip), "changing the view keeps the selected preview");

                screen.transform.Find("TopBar/Search").GetComponent<TMP_InputField>().text = "ZZZ";
                yield return WaitFor(() => audio.clip == null, BootTimeoutSeconds, "ending the preview fade");
                Assert.That(preview.gameObject.activeInHierarchy, Is.False);
                Assert.That(screen.transform.Find("Main/DetailColumn/Detail/Actions/Start").GetComponent<Button>().interactable, Is.False);
            }
            finally
            {
                if (screen != null)
                {
                    Object.DestroyImmediate(screen.gameObject);
                }
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [UnityTest]
        public IEnumerator BaselineTapAutoplaysAndReturnsToTheLibrary()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            var originalLibrary = Object.FindFirstObjectByType<LibraryScreen>();
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/fixtures/baseline_tap"));
            StartThroughLibraryRetry(fixture);

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
        public IEnumerator NextRushOffsetKeepsThePlaybackClockThroughPauseRetryAndResult()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            string directory = Path.Combine(Path.GetTempPath(), "OpenSekai-NextRushTiming-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                WriteNextRushTimingPackage(directory);
                StartThroughLibraryRetry(directory);

                const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var panelResultField = typeof(LibraryResultPanel).GetField("_current", Fields);
                SoloLiveController controller = null;
                yield return WaitFor(
                    () =>
                    {
                        var activePanel = Object.FindFirstObjectByType<LibraryResultPanel>();
                        var loadResult = activePanel == null ? null : (PlayResult)panelResultField.GetValue(activePanel);
                        if (loadResult?.Kind == PlayResultKind.LoadError)
                            Assert.Fail(loadResult.Message);
                        controller = Object.FindFirstObjectByType<SoloLiveController>();
                        return controller != null && controller.JustPlayingState;
                    },
                    BootTimeoutSeconds, "reaching the signed run-in");

                var clockField = typeof(BaseLiveController).GetField("currentMusicTimeMs", Fields);
                var spanField = typeof(BaseLiveController).GetField("timelineSpan", Fields);
                var resultPendingField = typeof(SoloLiveController).GetField("_resultEffectPending", Fields);
                long ClockMs() => (long)clockField.GetValue(controller);
                var span = (LiveTimelineSpan)spanField.GetValue(controller);
                Assert.That(span.PlaybackEndSeconds, Is.EqualTo(4f).Within(0.002f));
                Assert.That(span.MusicStartSeconds, Is.EqualTo(0.5f).Within(0.002f));
                Assert.That(span.MusicEndSeconds, Is.EqualTo(3f).Within(0.002f));
                Assert.That(span.FirstNoteSeconds, Is.LessThan(span.MusicStartSeconds));
                Assert.That(span.LastNoteSeconds, Is.GreaterThan(span.PlaybackEndSeconds));
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.FirstNoteSeconds - 3).Within(0.20));

                controller.PauseLive();
                long beforeMusic = ClockMs();
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.That(ClockMs(), Is.EqualTo(beforeMusic));
                Assert.That(SoundManager.Instance.GetAudioSyncedUnityTimer(), Is.LessThanOrEqualTo(1L));
                controller.ResumeNoCountDown();
                yield return WaitFor(() => ClockMs() > beforeMusic + 50, 2f, "resuming the silent run-in");

                controller.Retry();
                yield return WaitFor(() => controller.JustPlayingState, BootTimeoutSeconds, "restarting the signed run-in");
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.StartSeconds).Within(0.20));

                yield return WaitFor(() => ClockMs() >= 850L, BootTimeoutSeconds, "reaching the playing audio");
                controller.PauseLive();
                long duringMusic = ClockMs();
                yield return new WaitForSecondsRealtime(0.20f);
                Assert.That(ClockMs(), Is.EqualTo(duringMusic));
                controller.ResumeNoCountDown();
                yield return WaitFor(() => ClockMs() > duringMusic + 50L, 2f, "resuming the playing audio");
                Assert.That(ClockMs() - duringMusic, Is.LessThan(200L));

                yield return WaitFor(() => !controller.JustPlayingState, BootTimeoutSeconds, "judging the last note after the audio file");
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.LastNoteSeconds).Within(0.20));
                Assert.That((bool)resultPendingField.GetValue(controller), Is.True);
                yield return WaitFor(() => !(bool)resultPendingField.GetValue(controller), 6f, "starting the result effect");
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.LastNoteSeconds + 3).Within(0.15));

                yield return WaitFor(
                    () => SceneLoader.GetActiveScene().name == "Library" && Object.FindFirstObjectByType<LibraryResultPanel>() != null,
                    BootTimeoutSeconds, "showing the NextRUSH+ result");
                var panel = Object.FindFirstObjectByType<LibraryResultPanel>();
                var result = (PlayResult)panelResultField.GetValue(panel);
                Assert.That(result.Kind, Is.EqualTo(PlayResultKind.Completed));
                Assert.That(result.AutoCount, Is.EqualTo(3));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static void StartThroughLibraryRetry(string directory)
        {
            var library = Object.FindFirstObjectByType<LibraryScreen>();
            library.ShowResult(PlayResult.FromLoadError("Playback test", true, directory, "Retry smoke setup"));
            var panel = Object.FindFirstObjectByType<LibraryResultPanel>();
            Assert.That(panel, Is.Not.Null, "the library should display the retry panel");
            var retry = panel.GetComponentsInChildren<Button>(true).SingleOrDefault(button => button.name == "Retry");
            Assert.That(retry, Is.Not.Null, "the result panel should wire its retry button");
            retry.onClick.Invoke();
        }

        private static void WriteNextRushTimingPackage(string directory)
        {
            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                "{\"formatVersion\":1,\"id\":\"next-rush-p1-" + System.Guid.NewGuid().ToString("N") +
                "\",\"chartFormat\":\"next-rush\",\"title\":\"Native timing test\",\"scoreFileName\":\"level-data.json\",\"audioFileName\":\"audio.wav\"}");
            File.WriteAllText(Path.Combine(directory, "level-data.json"), @"{
                ""bgmOffset"": 0.51,
                ""entities"": [
                    { ""archetype"": ""Initialization"", ""data"": [] },
                    { ""archetype"": ""#BPM_CHANGE"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 0 }, { ""name"": ""#BPM"", ""value"": 120 }] },
                    { ""archetype"": ""NormalHeadTapNote"", ""name"": ""head"", ""data"": [{ ""name"": ""#BEAT"", ""value"": -2 }, { ""name"": ""lane"", ""value"": 0 }, { ""name"": ""size"", ""value"": 1 }, { ""name"": ""next"", ""ref"": ""anchor"" }, { ""name"": ""connectorEase"", ""value"": 2 }] },
                    { ""archetype"": ""AnchorNote"", ""name"": ""anchor"", ""data"": [{ ""name"": ""#BEAT"", ""value"": -1 }, { ""name"": ""lane"", ""value"": 2 }, { ""name"": ""size"", ""value"": 1 }, { ""name"": ""activeHead"", ""ref"": ""head"" }] },
                    { ""archetype"": ""Connector"", ""data"": [{ ""name"": ""head"", ""ref"": ""head"" }, { ""name"": ""tail"", ""ref"": ""anchor"" }, { ""name"": ""segmentHead"", ""ref"": ""head"" }, { ""name"": ""segmentTail"", ""ref"": ""anchor"" }, { ""name"": ""activeHead"", ""ref"": ""head"" }, { ""name"": ""activeTail"", ""ref"": ""anchor"" }] },
                    { ""archetype"": ""AnchorNote"", ""name"": ""guideStart"", ""data"": [{ ""name"": ""#BEAT"", ""value"": -1 }, { ""name"": ""lane"", ""value"": -2 }, { ""name"": ""size"", ""value"": 1 }, { ""name"": ""next"", ""ref"": ""guideEnd"" }, { ""name"": ""segmentKind"", ""value"": 103 }, { ""name"": ""segmentAlpha"", ""value"": 0 }] },
                    { ""archetype"": ""AnchorNote"", ""name"": ""guideEnd"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 1 }, { ""name"": ""lane"", ""value"": -4 }, { ""name"": ""size"", ""value"": 1 }] },
                    { ""archetype"": ""Connector"", ""data"": [{ ""name"": ""head"", ""ref"": ""guideStart"" }, { ""name"": ""tail"", ""ref"": ""guideEnd"" }, { ""name"": ""segmentHead"", ""ref"": ""guideStart"" }, { ""name"": ""segmentTail"", ""ref"": ""guideEnd"" }] },
                    { ""archetype"": ""NormalTapNote"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 1.98 }, { ""name"": ""lane"", ""value"": 0 }, { ""name"": ""size"", ""value"": 1 }] },
                    { ""archetype"": ""NormalTapNote"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 8 }, { ""name"": ""lane"", ""value"": 0 }, { ""name"": ""size"", ""value"": 1 }] }
                ]
            }");

            const int sampleRate = 16000;
            const int samples = 4 * sampleRate;
            using var wav = new BinaryWriter(File.Create(Path.Combine(directory, "audio.wav")));
            void Header(string value) => wav.Write(System.Text.Encoding.ASCII.GetBytes(value));
            Header("RIFF");
            wav.Write(36 + 2 * samples);
            Header("WAVEfmt ");
            wav.Write(16);
            wav.Write((short)1);
            wav.Write((short)1);
            wav.Write(sampleRate);
            wav.Write(2 * sampleRate);
            wav.Write((short)2);
            wav.Write((short)16);
            Header("data");
            wav.Write(2 * samples);
            for (int n = 0; n < samples; n++)
            {
                // Audible between 0.5 s and 3 s, with an impulse at 1.5 s.
                short sample = n == 3 * sampleRate / 2 ? (short)12000
                    : n >= sampleRate / 2 && n < 3 * sampleRate ? (short)(n % 16 < 8 ? 2000 : -2000)
                    : (short)0;
                wav.Write(sample);
            }
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

            // The transition into a live used to warn "SE_AREA_TRANSITION_SEKAI is not registered."
            // on device: the cue was referenced by LiveTransitioner but never shipped.
            uint transition = sound.PlaySE("SE_AREA_TRANSITION_SEKAI");
            Assert.That(transition, Is.Not.Zero, "the live transition cue can start playback");
            sound.StopSE(transition);

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
