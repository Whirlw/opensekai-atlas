using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenSekai.App;
using OpenSekai.App.Playback;
using OpenSekai.App.UI;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Live.Simulation;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using SceneLoader = UnityEngine.SceneManagement.SceneManager;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

namespace OpenSekai.Tests.PlayMode
{
    /// <summary>Exercises Library scene assembly and baseline package playback.</summary>
    public class LibrarySmokeTests
    {
        private const string LibraryScenePath = "Assets/Game/Scenes/Library.unity";
        private const float BootTimeoutSeconds = 15f;

        [Test]
        public void LiveBackgroundCoverFillsTheMasksAtAnySourceResolution()
        {
            var prefab = Resources.Load<GameObject>("live/view/Background2DView");
            Assert.That(prefab, Is.Not.Null);
            var view = Object.Instantiate(prefab).GetComponent<Background2DView>();
            var small = new Texture2D(160, 160);
            var wide = new Texture2D(760, 600);
            try
            {
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var jackets = (SpriteRenderer[])typeof(Background2DView).GetField("_jackets", fields).GetValue(view);
                var originalSizes = jackets.Select(jacket => jacket.size).ToArray();
                var camera = (Camera)typeof(Background2DView).GetField("_jacketCamera", fields).GetValue(view);
                Vector2[] firstSizes = null;
                var apply = typeof(Background2DView).GetMethod("ApplyJacketTexture", fields);
                foreach (Texture2D texture in new[] { small, wide })
                {
                    apply.Invoke(view, new object[] { texture });
                    for (int i = 0; i < jackets.Length; i++)
                    {
                        var jacket = jackets[i];
                        Assert.That(jacket.size.x, Is.GreaterThanOrEqualTo(originalSizes[i].x));
                        Assert.That(jacket.size.y, Is.GreaterThanOrEqualTo(originalSizes[i].y));
                        if (firstSizes != null)
                            Assert.That(Vector2.Distance(jacket.size, firstSizes[i]), Is.LessThan(0.001f));

                        var mask = jacket.transform.parent.GetComponentInChildren<SpriteMask>(true);
                        var plane = new Plane(jacket.transform.forward, jacket.transform.position);
                        foreach (Vector2 vertex in mask.sprite.vertices)
                        {
                            Vector3 world = mask.transform.TransformPoint(vertex);
                            Ray ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(world));
                            Assert.That(plane.Raycast(ray, out float distance), Is.True);
                            Vector3 local = jacket.transform.InverseTransformPoint(ray.GetPoint(distance));
                            Assert.That(Mathf.Abs(local.x), Is.LessThanOrEqualTo(jacket.size.x * 0.5f + 0.001f));
                            Assert.That(Mathf.Abs(local.y), Is.LessThanOrEqualTo(jacket.size.y * 0.5f + 0.001f));
                        }
                        int side = Mathf.Min(texture.width, texture.height);
                        Assert.That(jacket.sprite.rect,
                            Is.EqualTo(new Rect((texture.width - side) * 0.5f, (texture.height - side) * 0.5f, side, side)));
                    }

                    firstSizes ??= jackets.Select(jacket => jacket.size).ToArray();
                }

                Assert.That(jackets[0].size.x, Is.GreaterThan(originalSizes[0].x), "the central frame needs a larger sprite");
            }
            finally
            {
                Object.Destroy(view.gameObject);
                Object.Destroy(small);
                Object.Destroy(wide);
            }
        }

        [UnityTest]
        public IEnumerator SharedKeysMatchParticlesInBothEffectPacks()
        {
            var cases = new[]
            {
                (NoteCategory.Normal, NoteType.Default, NoteResult.Perfect, NoteDirection.Default, LivePrefabDefine.FX_NORMAL_PERFECT_AURA),
                (NoteCategory.Normal, NoteType.Default, NoteResult.Great, NoteDirection.Default, LivePrefabDefine.FX_NORMAL_GREAT_AURA),
                (NoteCategory.Normal, NoteType.Default, NoteResult.Good, NoteDirection.Default, LivePrefabDefine.FX_NORMAL_GOOD_AURA),
                (NoteCategory.Normal, NoteType.Default, NoteResult.Bad, NoteDirection.Default, null),
                (NoteCategory.Long, NoteType.Critical, NoteResult.Perfect, NoteDirection.Default, LivePrefabDefine.FX_CRITICAL_LONG_AURA),
                (NoteCategory.Flick, NoteType.Default, NoteResult.Perfect, NoteDirection.Left, LivePrefabDefine.FX_FLICK_AURA),
                (NoteCategory.Flick, NoteType.Critical, NoteResult.Perfect, NoteDirection.DownRight, LivePrefabDefine.FX_CRITICAL_FLICK_FLASH),
                (NoteCategory.FrictionFlick, NoteType.Default, NoteResult.Perfect, NoteDirection.Right, LivePrefabDefine.FX_FLICK_FLASH),
                (NoteCategory.Friction, NoteType.Default, NoteResult.Perfect, NoteDirection.Default, LivePrefabDefine.FX_FRICITION_AURA),
                (NoteCategory.Connection, NoteType.Critical, NoteResult.Perfect, NoteDirection.Default, LivePrefabDefine.FX_CRITICAL_CONNECT_AURA)
            };
            for (int pack = 0; pack < 2; pack++)
            {
                var host = new GameObject("Shared hit particles");
                var view = host.AddComponent<TapEffectView>();
                string previous = LiveConfig.EffectBundleName;
                try
                {
                    LiveConfig.SetNoteEffectName(pack);
                    view.Setup(null);
                    var particles = host.GetComponentsInChildren<ParticleSystemController>(true);
                    foreach (var (category, type, result, direction, expectedParticle) in cases)
                    {
                        var note = new NoteBase(default, 1, 5, 6, category, 1, 0, type) { Direction = direction };
                        note.SetJudgeInfoForDecoration(result);
                        view.Excute(note, () => true);
                        var standard = Snapshot();
                        if (expectedParticle != null)
                            Assert.That(standard.Any(particle => particle.name == expectedParticle + "(Clone)"), Is.True, category.ToString());
                        else
                            Assert.That(standard, Is.Empty, result.ToString());
                        StopParticles();

                        note.NativeEffectKind = NextRushEffectKind.None;
                        view.Excute(note, () => true);
                        Assert.That(Snapshot(), Is.EqualTo(standard), $"{category} {type} {result}");
                        Assert.That(host.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
                        StopParticles();
                    }

                    (string name, Vector3 position, Vector3 scale, Quaternion rotation)[] Snapshot() =>
                        particles.Where(p => p.IsPlaying).OrderBy(p => p.name).ThenBy(p => p.transform.localPosition.x)
                            .Select(p => (p.name, p.transform.localPosition, p.transform.localScale, p.transform.localRotation)).ToArray();

                    void StopParticles()
                    {
                        foreach (var particle in particles.Where(p => p.IsPlaying)) particle.Stop();
                    }
                }
                finally
                {
                    LiveConfig.SetNoteEffectName(previous == LiveConfig.GetEffectBundleName(1) ? 1 : 0);
                    view.Dispose();
                    Object.Destroy(host);
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator NextRushHoldParticlesFollowTheActiveSegmentAndOnlyFakeHoldsWait()
        {
            var host = new GameObject("NextRUSH hold effects");
            var view = host.AddComponent<LongTapEffectView>();
            string previous = LiveConfig.EffectBundleName;
            float previousLead = LiveConfig.ViewLeadSeconds;
            try
            {
                LiveConfig.SetNoteEffectName(0);
                LiveConfig.ViewLeadSeconds = 0;
                view.Setup();
                var timeline = new NextRushScrollTimeline(System.Array.Empty<NextRushScrollTimeline.Change>(), 0);
                MusicScoreInfo At(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);
                var config = Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData");
                var hold = new GuideNote(At(1f), 1, 3, 4, NoteCategory.Guide, NoteType.Default, config, 1);
                var joint = new HiddenConnectionNote(At(1.5f), 2, 5, 6, NoteCategory.Hidden, NoteType.Default, 1, 0);
                var tail = new HiddenConnectionNote(At(2f), 3, 7, 8, NoteCategory.Hidden, NoteType.Default, 1, 0);
                hold.SetNextRushScroll(timeline, 1d);
                joint.SetNextRushScroll(timeline, 1.5d);
                tail.SetNextRushScroll(timeline, 2d);
                hold.NativeConnector = new NextRushConnectorSegment(51, 1, 1, 1, hasActiveInterval: true);
                joint.NativeConnector = new NextRushConnectorSegment(52, 1, 1, 1, hasActiveInterval: true);
                hold.AddConnectionNote(joint);
                hold.AddNoteListAndSetupChildNote(tail);
                view.Add(hold);
                var particles = host.GetComponentsInChildren<ParticleSystemController>(true);
                bool Normal() => particles.Any(p => p.name == "fx_note_long_hold_gen(Clone)" && p.IsPlaying);
                bool Critical() => particles.Any(p => p.name == "fx_note_critical_long_hold_gen(Clone)" && p.IsPlaying);
                view.Excute(1f);
                view.Excute(1.04f);
                Assert.That(Normal() || Critical(), Is.False);
                view.Excute(1.06f);
                Assert.That(Normal() && !Critical(), Is.True);
                var first = particles.Single(p => p.name == "fx_note_long_hold_gen(Clone)" && p.IsPlaying).transform.localPosition;
                view.Excute(1.6f);
                Assert.That(!Normal() && Critical(), Is.True);
                var later = particles.Single(p => p.name == "fx_note_critical_long_hold_gen(Clone)" && p.IsPlaying).transform.localPosition;
                Assert.That(later.x, Is.GreaterThan(first.x));
                view.Excute(2f);
                Assert.That(Normal() || Critical(), Is.False);
                Assert.That(host.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);

                view.Remove(hold);
                hold.NativeConnector = null;
                hold.RecordHoldingInput(1f);
                view.Add(hold);
                view.Excute(1f);
                Assert.That(Normal(), Is.True, "the original hold lights as soon as it is held");

                view.Remove(hold);
                hold.NativeConnector = new NextRushConnectorSegment(1, 1, 1, 1, hasActiveInterval: true);
                view.Add(hold);
                view.Excute(1f);
                Assert.That(Normal(), Is.True, "a regular NextRUSH hold lights at the same time");
            }
            finally
            {
                LiveConfig.ViewLeadSeconds = previousLead;
                LiveConfig.SetNoteEffectName(previous == LiveConfig.GetEffectBundleName(1) ? 1 : 0);
                view.Clear();
                Object.Destroy(host);
            }
            yield return null;
        }

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
            string songCue = OpenSekai.Shared.Packages.SongPackageStorage.LoadEntry(fixture).AudioCueName;
            StartThroughLibraryRetry(fixture);

            yield return WaitFor(
                () => SceneLoader.GetActiveScene().name == "Core" && Object.FindFirstObjectByType<SoloLiveController>() != null,
                BootTimeoutSeconds, "loading Core and creating the live controller");
            Assert.That(SoundManager.Instance.IsLoadedSoundBundle(songCue), Is.True);

            yield return WaitFor(
                () => SceneLoader.GetActiveScene().name == "Library" && Object.FindFirstObjectByType<LibraryResultPanel>() != null,
                90f, "returning to Library and showing the result");
            var returnedLibrary = Object.FindFirstObjectByType<LibraryScreen>();
            Assert.That(returnedLibrary, Is.Not.Null);
            Assert.That(originalLibrary == null, Is.True, "the original Library scene should have unloaded");
            Assert.That(SoundManager.Instance.IsLoadedSoundBundle(songCue), Is.False,
                "returning to Library releases the completed song from the process-wide audio catalog");

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
        public IEnumerator ServerPlaybackRestoresTheSamePagesAndCaches()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            var originalHome = HomeScreen.Current;
            originalHome.ShowServerImmediately();
            var server = Object.FindFirstObjectByType<ServerScreen>();
            var page = server.Top;
            var covers = server.Covers;
            var installer = server.Installer;
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/fixtures/baseline_tap"));

            server.RequestPlay(fixture, "Server return test");
            yield return WaitFor(
                () =>
                {
                    if (originalHome != null)
                        Assert.That(Object.FindFirstObjectByType<LibraryScreen>().IsPresented, Is.False);
                    return SceneLoader.GetActiveScene().name == "Core" && Object.FindFirstObjectByType<SoloLiveController>() != null;
                },
                BootTimeoutSeconds, "starting the server chart");
            Assert.That(server.IsPresented, Is.False);
            Assert.That(originalHome == null, Is.True);

            LibraryResultPanel panel = null;
            yield return WaitFor(() =>
            {
                panel = Object.FindFirstObjectByType<LibraryResultPanel>();
                return SceneLoader.GetActiveScene().name == "Library" && panel != null && panel.IsOpen;
            }, 90f, "showing the server result");

            Assert.That(Object.FindFirstObjectByType<ServerScreen>(), Is.SameAs(server));
            Assert.That(server.gameObject.scene, Is.EqualTo(SceneLoader.GetActiveScene()));
            Assert.That(server.Top, Is.SameAs(page));
            Assert.That(server.Covers, Is.SameAs(covers));
            Assert.That(server.Installer, Is.SameAs(installer));
            Assert.That(Object.FindFirstObjectByType<LibraryScreen>().IsPresented, Is.False);
            Assert.That(panel.GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text == "返回服务器"), Is.True);
            Assert.That(server.TryHandleBack(), Is.True);
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(server.IsPresented, Is.True);
        }

        [UnityTest]
        public IEnumerator InterruptedSonolusUpdateLeavesTheOldPackagePlayableFromTheLibrary()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            var originalLibrary = Object.FindFirstObjectByType<LibraryScreen>();
            LibraryScreen recoveredLibrary = null;
            string root = Path.Combine(Path.GetTempPath(), "OpenSekai-SonolusRecovery-" + System.Guid.NewGuid().ToString("N"));
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/fixtures/baseline_tap"));
            string libraryRoot = Path.Combine(root, "library");
            string indexRoot = Path.Combine(root, "index");
            string stagingRoot = Path.Combine(root, "staging");
            try
            {
                var server = SonolusServerAddress.Parse("example.com");
                var oldLevel = BaselineSonolusLevel(1);
                var newLevel = BaselineSonolusLevel(2);
                var installer = new SonolusLevelInstaller(new SongPackageManager(libraryRoot), indexRoot, stagingRoot);
                var oldPackage = installer.PublishPackage(StageBaseline("old"), server, oldLevel);
                var unpublished = installer.PreparePublish(StageBaseline("update"), server, newLevel);
                Assert.That(unpublished.RootDirectory, Is.Not.EqualTo(oldPackage.RootDirectory));
                Assert.That(Directory.Exists(unpublished.RootDirectory), Is.True);
                Assert.That(Directory.Exists(oldPackage.RootDirectory), Is.True);

                var recoveredPackages = new SongPackageManager(libraryRoot);
                var recoveredInstaller = new SonolusLevelInstaller(recoveredPackages, indexRoot, stagingRoot);
                string recoveredDirectory = recoveredInstaller.FindInstalled(server, oldLevel.Name);
                Assert.That(recoveredDirectory, Is.EqualTo(oldPackage.RootDirectory));
                Assert.That(recoveredInstaller.StateOf(server, oldLevel), Is.EqualTo(SonolusInstallState.Installed));
                Assert.That(recoveredInstaller.StateOf(server, newLevel), Is.EqualTo(SonolusInstallState.Outdated));
                Assert.That(Directory.Exists(unpublished.RootDirectory), Is.False);
                Assert.That(recoveredPackages.LoadItems().Select(item => item.Entry.RootDirectory),
                    Is.EqualTo(new[] { recoveredDirectory }));

                recoveredLibrary = LibraryScreen.Create(new AppContext(recoveredPackages, PlayService.Instance));
                Transform actions = recoveredLibrary.transform.Find("Main/DetailColumn/Detail/Actions");
                Assert.That(actions, Is.Not.Null);
                var auto = actions.Find("Auto").GetComponent<Button>();
                var start = actions.Find("Start").GetComponent<Button>();
                Assert.That(start.interactable, Is.True, "the recovered package should be selected and playable");
                auto.onClick.Invoke();
                start.onClick.Invoke();

                yield return WaitFor(() => SceneLoader.GetActiveScene().name == "Core"
                    && Object.FindFirstObjectByType<SoloLiveController>() != null,
                    BootTimeoutSeconds, "starting the recovered package through the library");
                Assert.That(SoundManager.Instance.IsLoadedSoundBundle(oldPackage.AudioCueName), Is.True);
                Assert.That(SoundManager.Instance.IsLoadedSoundBundle(unpublished.AudioCueName), Is.False);

                yield return WaitFor(() => SceneLoader.GetActiveScene().name == "Library"
                    && Object.FindFirstObjectByType<LibraryResultPanel>() != null,
                    90f, "showing the recovered package's autoplay result");
                var panel = Object.FindFirstObjectByType<LibraryResultPanel>();
                var result = (PlayResult)typeof(LibraryResultPanel)
                    .GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.PackageDirectory, Is.EqualTo(recoveredDirectory));
                Assert.That(result.Kind, Is.EqualTo(PlayResultKind.Completed));
                Assert.That(result.IsAuto, Is.True);
                Assert.That(result.Score, Is.EqualTo(1_000_000));
            }
            finally
            {
                if (recoveredLibrary != null)
                {
                    Object.DestroyImmediate(recoveredLibrary.gameObject);
                    if (originalLibrary != null) LibraryPageRegistry.Register(originalLibrary);
                }
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }

            string StageBaseline(string revision)
            {
                string destination = Directory.CreateDirectory(Path.Combine(root, "sources", revision)).FullName;
                foreach (string file in Directory.EnumerateFiles(fixture))
                    File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
                string manifestPath = Path.Combine(destination, "manifest.json");
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                manifest["id"] = "sonolus-recovery-" + revision;
                File.WriteAllText(manifestPath, manifest.ToString());
                return destination;
            }
        }

        [UnityTest]
        public IEnumerator TemporaryServerChartPlaysThenInstallsFromMemoryAndReleasesCache()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            string root = Path.Combine(Path.GetTempPath(), "OpenSekai-TemporarySonolus-" + System.Guid.NewGuid().ToString("N"));
            string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            try
            {
                WriteNextRushTimingPackage(source);
                string chartPath = Path.Combine(source, "level-data.json");
                string audioPath = Path.Combine(source, "audio.wav");
                byte[] chartData = File.ReadAllBytes(chartPath);
                byte[] audioData = File.ReadAllBytes(audioPath);
                string extensionlessPath = Path.Combine(source, "audio-resource-hash");
                File.Move(audioPath, extensionlessPath);
                AudioType audioType = SonolusClient.AudioTypeOf(audioData);
                Assert.That(audioType, Is.EqualTo(AudioType.WAV));
                AudioClip clip;
                using (var request = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(extensionlessPath).AbsoluteUri, audioType))
                {
                    yield return request.SendWebRequest();
                    Assert.That(request.result, Is.EqualTo(UnityWebRequest.Result.Success), request.error);
                    Assert.That(request.downloadHandler.data, Is.EqualTo(audioData));
                    clip = DownloadHandlerAudioClip.GetContent(request);
                    Assert.That(clip, Is.Not.Null);
                }

                var address = SonolusServerAddress.Parse("https://example.invalid");
                var details = SonolusLevelDetails.From(new JObject
                {
                    ["item"] = new JObject
                    {
                        ["name"] = "cached-" + System.Guid.NewGuid().ToString("N"),
                        ["engine"] = new JObject { ["name"] = SonolusLevelInstaller.SUPPORTED_ENGINE },
                        ["data"] = new JObject { ["url"] = "/chart" },
                        ["bgm"] = new JObject { ["url"] = "/music" }
                    }
                });
                var installer = new SonolusLevelInstaller(new SongPackageManager(Path.Combine(root, "library")),
                    Path.Combine(root, "index"), Path.Combine(root, "staging"));
                var metadata = Newtonsoft.Json.JsonConvert.DeserializeObject<OpenSekai.Shared.Packages.SongPackageManifest>(
                    SonolusLevelInstaller.BuildManifest(details, ".wav", null));
                var entry = new OpenSekai.Shared.Packages.SongPackage(null, metadata);
                var chart = OpenSekai.Infrastructure.Charts.NextRushChartLoader.ReadFile(chartPath);
                var cached = new TemporarySongPackage(entry, chart, SongPackageHash.Compute(chartPath), clip, chartData, audioData, null);
                SonolusLevelInstaller.RememberTemporary(address, details.Level, cached);
                Directory.Delete(source, recursive: true);
                Assert.That(installer.FindInstalled(address, details.Level.Name), Is.Null);

                HomeScreen.Current.ShowServerImmediately();
                var screen = Object.FindFirstObjectByType<ServerScreen>();
                screen.RequestPlay(null, "Temporary server test", cached);
                yield return WaitFor(
                    () => SceneLoader.GetActiveScene().name == "Core" && Object.FindFirstObjectByType<SoloLiveController>() != null,
                    BootTimeoutSeconds, "playing the temporary chart without a package folder");
                yield return WaitFor(
                    () => SceneLoader.GetActiveScene().name == "Library" && screen.IsPresented,
                    90f, "returning to the server after temporary play");
                Assert.That(clip != null, Is.True, "releasing live audio must leave the cache usable");
                Assert.That(installer.FindInstalled(address, details.Level.Name), Is.Null);

                Assert.That(screen.TryHandleBack(), Is.True);
                var install = installer.InstallAsync(new SonolusClient(address), details, null, CancellationToken.None).ToCoroutine(
                    installed =>
                    {
                        Assert.That(File.ReadAllBytes(installed.ScorePath), Is.EqualTo(chartData));
                        Assert.That(File.ReadAllBytes(installed.AudioPath), Is.EqualTo(audioData));
                    });
                yield return install;
                Assert.That(installer.FindInstalled(address, details.Level.Name), Is.Not.Null);
                Assert.That(SonolusLevelInstaller.FindTemporary(address, details.Level), Is.Null);
                yield return null;
                Assert.That(clip == null, Is.True, "the promoted clip should be released");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static SonolusLevel BaselineSonolusLevel(int version) => SonolusLevel.From(new JObject
        {
            ["name"] = "baseline-tap",
            ["version"] = version,
            ["engine"] = new JObject { ["name"] = SonolusLevelInstaller.SUPPORTED_ENGINE },
            ["data"] = new JObject { ["url"] = "/chart" },
            ["bgm"] = new JObject { ["url"] = "/music" },
            ["cover"] = new JObject { ["url"] = "/cover" }
        });

        [UnityTest]
        public IEnumerator NextRushOffsetKeepsThePlaybackClockThroughPauseRetryAndResult()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            string directory = Path.Combine(Path.GetTempPath(), "OpenSekai-NextRushTiming-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var settings = OpenSekai.Shared.Settings.LiveSettingsStore.Shared.Load();
            int? previousInfoMode = settings.CustomMusicScoreMusicInfoDisplayMode;
            settings.CustomMusicScoreMusicInfoDisplayMode = LiveSettingData.MUSIC_INFO_DISPLAY_MODE_CUSTOM_SCORE;
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
                var logicField = typeof(SoloLiveController).GetField("_liveLogic", Fields);
                var front = Object.FindFirstObjectByType<LiveFrontView>();
                var lifeView = (LifeView)typeof(LiveFrontView).GetField("_lifeView", Fields).GetValue(front);
                var gauge = (SpriteRenderer)typeof(LifeView).GetField("_lifeDefaultGauge", Fields).GetValue(lifeView);
                var fullGauge = (Vector2)typeof(LifeView).GetField("_lifeDefaultGaugeSize", Fields).GetValue(lifeView);
                void AssertStartingLife()
                {
                    Assert.That(((LiveLogic)logicField.GetValue(controller)).Score.life, Is.EqualTo(325));
                    Assert.That(gauge.size.x, Is.EqualTo(fullGauge.x * 0.325f).Within(0.001f));
                }
                long ClockMs() => (long)clockField.GetValue(controller);
                AssertStartingLife();
                var span = (LiveTimelineSpan)spanField.GetValue(controller);
                Assert.That(span.PlaybackEndSeconds, Is.EqualTo(4f).Within(0.002f));
                Assert.That(span.MusicStartSeconds, Is.EqualTo(0.5f).Within(0.002f));
                Assert.That(span.MusicEndSeconds, Is.EqualTo(3f).Within(0.002f));
                Assert.That(span.FirstNoteSeconds, Is.LessThan(span.MusicStartSeconds));
                Assert.That(span.LastNoteSeconds, Is.GreaterThan(span.PlaybackEndSeconds));
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.StartSeconds).Within(0.20));

                controller.PauseLive();
                long beforeMusic = ClockMs();
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.That(ClockMs(), Is.EqualTo(beforeMusic));
                Assert.That(SoundManager.Instance.GetAudioSyncedUnityTimer(), Is.LessThanOrEqualTo(1L));
                controller.ResumeNoCountDown();
                yield return WaitFor(() => ClockMs() > beforeMusic + 50, 2f, "resuming the silent run-in");

                front.UpdateLife(new LiveScore { life = int.MaxValue });
                var digits = (NumberView)typeof(LifeView).GetField("_number", Fields).GetValue(lifeView);
                var renderers = (SpriteRenderer[])typeof(NumberView).GetField("_numberSpriteRenderers", Fields).GetValue(digits);
                var digitSprites = (Sprite[])typeof(NumberView).GetField("_numberSprites", Fields).GetValue(digits);
                Assert.That(renderers.Take(10).Select(renderer => renderer.sprite),
                    Is.EqualTo("7463847412".Select(digit => digitSprites[digit - '0'])));

                controller.Retry();
                yield return WaitFor(() => controller.JustPlayingState, BootTimeoutSeconds, "restarting the signed run-in");
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.StartSeconds).Within(0.20));
                AssertStartingLife();

                yield return WaitFor(() =>
                {
                    var line = GameObject.Find("NoteLineView");
                    return line != null && line.GetComponent<MeshRenderer>().enabled
                        && line.GetComponent<MeshFilter>().sharedMesh.vertexCount > 8;
                }, BootTimeoutSeconds, "drawing the hold with its attached separator after retry");

                MeshRenderer overlay = null;
                yield return WaitFor(() =>
                {
                    overlay = GameObject.Find("GuideLineView/Over")?.GetComponent<MeshRenderer>();
                    return overlay != null && overlay.enabled;
                }, BootTimeoutSeconds, "drawing the full-screen connector with independent geometry through the live camera");
                Assert.That(overlay.sortingOrder, Is.GreaterThan(31));
                Assert.That(overlay.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(8));

                yield return WaitFor(() => ClockMs() >= 850L, BootTimeoutSeconds, "reaching the playing audio");
                controller.PauseLive();
                long duringMusic = ClockMs();
                yield return new WaitForSecondsRealtime(0.20f);
                Assert.That(ClockMs(), Is.EqualTo(duringMusic));
                controller.ResumeNoCountDown();
                yield return WaitFor(() => ClockMs() > duringMusic + 50L, 2f, "resuming the playing audio");
                Assert.That(ClockMs() - duringMusic, Is.LessThan(200L));
                yield return WaitFor(() => !overlay.enabled, 2f, "removing the full-screen guide at the end of its chart interval");

                yield return WaitFor(() => !controller.JustPlayingState, BootTimeoutSeconds, "judging the last note after the audio file");
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.LastNoteSeconds).Within(0.20));
                Assert.That((bool)resultPendingField.GetValue(controller), Is.True);
                yield return WaitFor(() => !(bool)resultPendingField.GetValue(controller), 6f, "starting the result effect");
                Assert.That(ClockMs() / 1000d, Is.EqualTo(span.LastNoteSeconds + 1).Within(0.15));
                double resultStarted = Time.timeAsDouble;
                yield return WaitFor(() => SceneLoader.GetActiveScene().name != "Core", BootTimeoutSeconds,
                    "leaving gameplay after the common result animation and fade");
                Assert.That(Time.timeAsDouble - resultStarted, Is.GreaterThanOrEqualTo(5.9d));

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
                settings.CustomMusicScoreMusicInfoDisplayMode = previousInfoMode;
                Directory.Delete(directory, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator IntroAndOutroSkipBoundariesMatchInBothModes()
        {
            yield return LibrarySceneBootsAndShowsThePage();
            string directory = Path.Combine(Path.GetTempPath(), "OpenSekai-LiveSkips-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var settings = OpenSekai.Shared.Settings.LiveSettingsStore.Shared.Load();
            int? previousInfoMode = settings.CustomMusicScoreMusicInfoDisplayMode;
            settings.CustomMusicScoreMusicInfoDisplayMode = LiveSettingData.MUSIC_INFO_DISPLAY_MODE_SKIP;
            try
            {
                WriteNextRushTimingPackage(directory);
                File.WriteAllText(Path.Combine(directory, "level-data.json"), @"{
                    ""bgmOffset"": 0,
                    ""entities"": [
                        { ""archetype"": ""Initialization"", ""data"": [{ ""name"": ""initialLife"", ""value"": 1000 }] },
                        { ""archetype"": ""#BPM_CHANGE"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 0 }, { ""name"": ""#BPM"", ""value"": 120 }] },
                        { ""archetype"": ""NormalTapNote"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 4 }, { ""name"": ""lane"", ""value"": 0 }, { ""name"": ""size"", ""value"": 1 }] },
                        { ""archetype"": ""NormalTapNote"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 5 }, { ""name"": ""lane"", ""value"": 0 }, { ""name"": ""size"", ""value"": 1 }] }
                    ]
                }");

                const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var resultField = typeof(LibraryResultPanel).GetField("_current", Fields);
                var clock = typeof(BaseLiveController).GetField("currentMusicTimeMs", Fields);
                var spanField = typeof(BaseLiveController).GetField("timelineSpan", Fields);
                var skipShown = typeof(SoloLiveController).GetField("_skipShown", Fields);
                var pending = typeof(SoloLiveController).GetField("_resultEffectPending", Fields);
                var introSkip = typeof(SoloLiveController).GetMethod("SkipIntro", Fields);
                var outroSkip = typeof(SoloLiveController).GetMethod("SkipOutro", Fields);
                var resultAnimation = typeof(SoloLiveController).GetMethod("GetLiveResultAnimationType", Fields);
                foreach (bool isAuto in new[] { false, true })
                {
                    StartThroughLibraryRetry(directory, isAuto);
                    SoloLiveController controller = null;
                    yield return WaitFor(() =>
                    {
                        var panel = Object.FindFirstObjectByType<LibraryResultPanel>();
                        if (panel != null && resultField.GetValue(panel) is PlayResult { Kind: PlayResultKind.LoadError } error)
                            Assert.Fail(error.Message);
                        controller = Object.FindFirstObjectByType<SoloLiveController>();
                        return controller != null && controller.JustPlayingState;
                    }, BootTimeoutSeconds, "starting the chart with a skippable intro");

                    ((FreeLiveBootData)controller.BootData).CustomMusicScoreKey = null;
                    double ClockSeconds() => (long)clock.GetValue(controller) / 1000d;
                    var span = (LiveTimelineSpan)spanField.GetValue(controller);
                    Assert.That((bool)skipShown.GetValue(controller), Is.True);
                    introSkip.Invoke(controller, null);
                    Assert.That(ClockSeconds(), Is.EqualTo(span.FirstNoteSeconds - 2d).Within(0.002));
                    Assert.That((bool)skipShown.GetValue(controller), Is.False);

                    yield return WaitFor(() => (bool)skipShown.GetValue(controller), 6f, "showing the outro skip");
                    Assert.That(ClockSeconds(), Is.EqualTo(span.LastNoteSeconds + 1d).Within(0.12));
                    Assert.That((bool)pending.GetValue(controller), Is.True);
                    Assert.That(resultAnimation.Invoke(controller, null), Is.EqualTo(LiveResultAnimationType.Clear));
                    outroSkip.Invoke(controller, null);
                    Assert.That((bool)pending.GetValue(controller), Is.False, "the result effect starts immediately");
                    Assert.That((bool)skipShown.GetValue(controller), Is.False);

                    yield return WaitFor(() => SceneLoader.GetActiveScene().name == "Library"
                        && Object.FindFirstObjectByType<LibraryResultPanel>() != null,
                        BootTimeoutSeconds, "opening the result panel");
                }
            }
            finally
            {
                settings.CustomMusicScoreMusicInfoDisplayMode = previousInfoMode;
                Directory.Delete(directory, recursive: true);
            }
        }

        private static void StartThroughLibraryRetry(string directory, bool isAuto = true)
        {
            var library = Object.FindFirstObjectByType<LibraryScreen>();
            library.ShowResult(PlayResult.FromLoadError("Playback test", isAuto, directory, "Retry smoke setup"));
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
                    { ""archetype"": ""Initialization"", ""data"": [{ ""name"": ""initialLife"", ""value"": 325 }] },
                    { ""archetype"": ""#BPM_CHANGE"", ""data"": [{ ""name"": ""#BEAT"", ""value"": 0 }, { ""name"": ""#BPM"", ""value"": 120 }] },
                    { ""archetype"": ""NormalHeadTapNote"", ""name"": ""head"", ""data"": [{ ""name"": ""#BEAT"", ""value"": -2 }, { ""name"": ""lane"", ""value"": 0 }, { ""name"": ""size"", ""value"": 1 }, { ""name"": ""next"", ""ref"": ""anchor"" }, { ""name"": ""connectorEase"", ""value"": 2 }] },
                    { ""archetype"": ""AnchorNote"", ""name"": ""separator"", ""data"": [{ ""name"": ""#BEAT"", ""value"": -1.5 }, { ""name"": ""isAttached"", ""value"": 1 }, { ""name"": ""isSeparator"", ""value"": 1 }, { ""name"": ""attachHead"", ""ref"": ""head"" }, { ""name"": ""attachTail"", ""ref"": ""anchor"" }, { ""name"": ""activeHead"", ""ref"": ""head"" }] },
                    { ""archetype"": ""AnchorNote"", ""name"": ""anchor"", ""data"": [{ ""name"": ""#BEAT"", ""value"": -1 }, { ""name"": ""lane"", ""value"": 2 }, { ""name"": ""size"", ""value"": 1 }, { ""name"": ""activeHead"", ""ref"": ""head"" }] },
                    { ""archetype"": ""Connector"", ""data"": [{ ""name"": ""head"", ""ref"": ""head"" }, { ""name"": ""tail"", ""ref"": ""separator"" }, { ""name"": ""segmentHead"", ""ref"": ""head"" }, { ""name"": ""segmentTail"", ""ref"": ""separator"" }, { ""name"": ""activeHead"", ""ref"": ""head"" }, { ""name"": ""activeTail"", ""ref"": ""anchor"" }] },
                    { ""archetype"": ""Connector"", ""data"": [{ ""name"": ""head"", ""ref"": ""separator"" }, { ""name"": ""tail"", ""ref"": ""anchor"" }, { ""name"": ""segmentHead"", ""ref"": ""separator"" }, { ""name"": ""segmentTail"", ""ref"": ""anchor"" }, { ""name"": ""activeHead"", ""ref"": ""head"" }, { ""name"": ""activeTail"", ""ref"": ""anchor"" }] },
                    { ""archetype"": ""AnchorNote"", ""name"": ""guideStart"", ""data"": [{ ""name"": ""#BEAT"", ""value"": -1 }, { ""name"": ""lane"", ""value"": -2 }, { ""name"": ""size"", ""value"": 1 }, { ""name"": ""segmentKind"", ""value"": 103 }, { ""name"": ""segmentAlpha"", ""value"": 0 }, { ""name"": ""segmentLayer"", ""value"": 3 }, { ""name"": ""segmentPresentation"", ""value"": 1 }] },
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
