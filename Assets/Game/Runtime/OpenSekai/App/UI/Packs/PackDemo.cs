using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.App.Playback;
using OpenSekai.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Live.Conversion;
using OpenSekai.MaterialPacks;
using OpenSekai.Shared.Settings;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Runs the resource pack page's demo (<see cref="LiveDemoController"/>) for one configuration at
    /// a time and shares its texture with whichever page shows it. A new configuration is prepared
    /// first (pack files read and decoded) while the old demo plays on, then the two swap.
    /// </summary>
    internal sealed class PackDemo : IDisposable
    {
        /// <summary>Far from the page cameras, which draw every layer around the origin.</summary>
        private static readonly Vector3 STAGE_POSITION = new Vector3(0f, -4000f, 0f);

        /// <summary>The texture's height; its width follows the screen's shape, as the live lays out for it.</summary>
        private const int TEXTURE_HEIGHT = 720;

        /// <summary>Changes inside this window start one demo, not one each; it also lets a page slide in first.</summary>
        private const int SETTLE_MILLISECONDS = 280;

        private readonly Transform _parent;
        private readonly Func<MaterialPackStore> _store;
        private RenderTexture _texture;
        private LiveDemoController _current;
        private MaterialPackConfiguration _wanted;
        private CancellationTokenSource _pending;
        private bool _audible;

        internal PackDemo(Transform parent, Func<MaterialPackStore> store)
        {
            _parent = parent;
            _store = store;
        }

        /// <summary>The loading state, the error or the first frame changed.</summary>
        internal event Action Changed;

        /// <summary>What the demo draws into; null until the first demo starts.</summary>
        internal Texture Texture => _texture;

        /// <summary>The texture's width over its height.</summary>
        internal float Aspect => _texture != null ? (float)_texture.width / _texture.height : ScreenAspect();

        /// <summary>True while a demo for the wanted configuration is being prepared.</summary>
        internal bool IsLoading { get; private set; }

        /// <summary>True once a demo draws into the texture.</summary>
        internal bool IsRunning => _current != null;

        /// <summary>Why the last demo could not start; null when it did.</summary>
        internal string Error { get; private set; }

        /// <summary>Some pack files could not be read; the demo uses the built-in ones there, like a live.</summary>
        internal int MissingFiles { get; private set; }

        /// <summary>Whether the demo's hit sounds and result voices are heard. Off until switched on.</summary>
        internal bool Audible
        {
            get => _audible;
            set
            {
                _audible = value;
                if (_current != null) _current.Audible = value;
                Changed?.Invoke();
            }
        }

        /// <summary>Plays <paramref name="configuration"/>; the same choices as now change nothing.</summary>
        internal void Show(MaterialPackConfiguration configuration)
        {
            if (configuration == null)
            {
                Stop();
                return;
            }

            if (_wanted != null && _wanted.SameChoicesAs(configuration) && (_current != null || _pending != null))
            {
                return;
            }

            _wanted = configuration.Copy();
            _pending?.Cancel();
            var cancellation = new CancellationTokenSource();
            _pending = cancellation;
            StartAsync(_wanted, cancellation).Forget();
        }

        /// <summary>Ends the demo (the page left it); the texture keeps its last frame for the next one.</summary>
        internal void Stop()
        {
            _pending?.Cancel();
            _pending = null;
            _wanted = null;
            IsLoading = false;
            if (_current != null)
            {
                UnityEngine.Object.Destroy(_current.gameObject);
                _current = null;
            }
        }

        public void Dispose()
        {
            Stop();
            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
        }

        private async UniTaskVoid StartAsync(MaterialPackConfiguration configuration, CancellationTokenSource cancellation)
        {
            CancellationToken token = cancellation.Token;
            IsLoading = true;
            Changed?.Invoke();
            MaterialAudioPreparation material = null;
            FreeLiveBootData boot = null;
            try
            {
                await UniTask.Delay(SETTLE_MILLISECONDS, DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, token);
                MaterialPackStore store = _store() ?? throw new InvalidDataException("资源包尚未就绪。");
                material = new MaterialAudioPreparation(store.Bind(configuration), null);
                var diagnostics = new List<string>();
                await material.PrepareAsync(diagnostics, token);
                token.ThrowIfCancellationRequested();
                boot = CreateBoot(material);
                material = null;
                EnsureTexture();
                FreeLiveBootData owned = boot;
                // The demo owns the boot from here on, and releases it even when it fails to start.
                boot = null;
                LiveDemoController next = LiveDemoController.Create(_parent, STAGE_POSITION, owned, _texture);
                next.Audible = _audible;
                if (_current != null) UnityEngine.Object.Destroy(_current.gameObject);
                _current = next;
                MissingFiles = diagnostics.Count;
                Error = null;
                if (diagnostics.Count > 0) Debug.LogWarning("[PackDemo] " + string.Join("\n", diagnostics));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException
                or UnityException or ArgumentException or NullReferenceException)
            {
                // A demo is only a demo: say why, and keep the page working.
                Debug.LogWarning("[PackDemo] " + e);
                Error = e is InvalidDataException or IOException ? e.Message : "演示暂时无法播放。";
            }
            finally
            {
                if (boot != null) Release(boot);
                else if (material != null) Release(material);
                if (_pending == cancellation)
                {
                    _pending = null;
                    IsLoading = false;
                }

                cancellation.Dispose();
                Changed?.Invoke();
            }
        }

        private void EnsureTexture()
        {
            int height = TEXTURE_HEIGHT;
            int width = Mathf.Max(1, Mathf.RoundToInt(height * ScreenAspect()));
            if (_texture != null && _texture.width == width && _texture.height == height) return;
            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
            }

            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "PackDemo", antiAliasing = 1 };
            _texture.Create();
        }

        private static float ScreenAspect() =>
            UnityEngine.Screen.height > 0 ? (float)UnityEngine.Screen.width / UnityEngine.Screen.height : 16f / 9f;

        /// <summary>
        /// A live's boot for the demo chart: autoplay shown as PERFECT with combo, no info page, the
        /// jacket background, the player's own settings otherwise, and the configuration's built-in
        /// sets and pack skins.
        /// </summary>
        private static FreeLiveBootData CreateBoot(MaterialAudioPreparation material)
        {
            MaterialBuiltinSelection builtin = material.Builtin;
            LiveSettingData settings = LiveSettingsStore.Shared.CaptureSnapshot()
                .WithBuiltinSets(builtin.Notes - 1, builtin.HitFx - 1, builtin.Audio - 1)
                .ToLegacyData();
            settings.CustomMusicScoreMusicInfoDisplayMode = LiveSettingData.MUSIC_INFO_DISPLAY_MODE_SKIP;
            settings.CustomMusicScoreAutoFakePerfectMode = LiveSettingData.AUTO_FAKE_PERFECT_MODE_FAKE_PERFECT;
            settings.CustomMusicScoreLiveBackgroundMode = LiveSettingData.CUSTOM_MUSIC_SCORE_BACKGROUND_MODE_JACKET;

            LiveBundleBuildData config = ResourceCatalog.LiveBundleConfig.Load();
            MusicScore score = RuntimeChartConverter.Convert(PackDemoChart.Build(), config);
            var difficulty = new MasterMusicDifficulty
            {
                id = MasterMusicDifficulty.INVALID_ID,
                musicId = 0,
                musicDifficulty = "master",
                playLevel = PackDemoChart.LEVEL,
                totalNoteCount = LiveUtility.CalculateTotalComboCount(score)
            };
            var boot = new FreeLiveBootData(0, difficulty.musicDifficulty, 0, 0, LivePlayMode.Free,
                LiveMusicData.CollaborationModeState.Off, MusicCategory.image)
            {
                LiveEventData = new LiveEventData(Array.Empty<IngameLotterySkill>(), Array.Empty<IngameComboCutin>(), 0, true),
                LiveSettingData = settings,
                MaterialAudio = material.Audio,
                ComboSkin = material.Combo,
                JudgmentSkin = material.Judgment,
                HitFxSkin = material.HitFx,
                BackgroundSkin = material.Background,
                ResultFxSkin = material.ResultFx,
                HudSkin = material.Hud,
                LaneSkin = material.Lane,
                NoteSkin = material.Notes,
                MVQualityType = settings.QualityType,
                MusicCategory = MusicCategory.image,
                IsAuto = true,
                IsCustomMusicScore = true,
                IsOfficialMusicScore = false,
                canSkipDisplayMusicInfo = true,
                CustomMusicScoreTitle = PackDemoChart.TITLE
            };
            boot.MusicData.Music = new MasterMusic { id = 0, title = PackDemoChart.TITLE, fillerSec = 0f };
            boot.MusicData.Difficulty = difficulty;
            boot.MusicData.Score = new MasterPlayLevelScore { liveType = LiveType.solo.ToString(), playLevel = PackDemoChart.LEVEL };
            boot.MusicData.IsTestPlay = false;
            boot.MusicData.IsUseCustomScore = true;
            boot.MusicData.CustomPlayLevel = PackDemoChart.LEVEL;
            boot.MusicData.MusicScore = score;
            boot.MusicData.StartMusicTimeMs = 0L;
            boot.MusicData.PlayStartEffectEnabled = false;
            return boot;
        }

        private static void Release(FreeLiveBootData boot)
        {
            Dispose(boot.BackgroundSkin, boot.ResultFxSkin, boot.HudSkin, boot.LaneSkin, boot.NoteSkin, boot.HitFxSkin,
                boot.JudgmentSkin, boot.ComboSkin, boot.MaterialAudio);
        }

        private static void Release(MaterialAudioPreparation material)
        {
            Dispose(material.Background, material.ResultFx, material.Hud, material.Lane, material.Notes, material.HitFx,
                material.Judgment, material.Combo, material.Audio);
        }

        private static void Dispose(params IDisposable[] items)
        {
            foreach (IDisposable item in items) item?.Dispose();
        }
    }

    /// <summary>
    /// The demo's chart: a few bars of the note kinds a skin draws (taps, critical taps, flicks in
    /// three directions, holds with a relay tick and a flick end, trace notes), on 12 lanes at 4/4.
    /// </summary>
    internal static class PackDemoChart
    {
        internal const string TITLE = "资源包演示";
        internal const int LEVEL = 26;
        private const double BPM = 150d;
        private const int BEAT = 480;
        private const int BAR = BEAT * 4;

        internal static ChartDocument Build()
        {
            var notes = new List<ChartNote>();
            int id = 100;

            // Bar 1: taps, left and right.
            Tap(0, 2, 4);
            Tap(BEAT, 7, 9);
            Tap(BEAT * 2, 2, 4);
            Tap(BEAT * 3, 7, 9);

            // Bar 2: critical taps between chords.
            Tap(BAR, 4, 7, true);
            Tap(BAR + BEAT, 0, 2);
            Tap(BAR + BEAT, 9, 11);
            Tap(BAR + BEAT * 2, 4, 7, true);
            Tap(BAR + BEAT * 5 / 2, 5, 6);
            Tap(BAR + BEAT * 3, 1, 3);
            Tap(BAR + BEAT * 3, 8, 10);

            // Bar 3: flicks up, left, right, and a critical one.
            Flick(BAR * 2, 4, 7, NoteDirection.Default);
            Flick(BAR * 2 + BEAT, 1, 4, NoteDirection.Left);
            Flick(BAR * 2 + BEAT * 2, 7, 10, NoteDirection.Right);
            Flick(BAR * 2 + BEAT * 3, 4, 7, NoteDirection.Default, true);

            // Bars 4 and 5: a hold drifting right over a relay tick, then a critical hold ending in a flick.
            Hold(BAR * 3, 1, 3, new[] { (BAR * 3 + BEAT * 2, 4, 6) }, BAR * 4, 7, 9);
            Tap(BAR * 3 + BEAT, 9, 11);
            Tap(BAR * 3 + BEAT * 3, 0, 2);
            Hold(BAR * 4, 5, 8, Array.Empty<(int, int, int)>(), BAR * 4 + BEAT * 3, 5, 8, true, NoteDirection.Default);
            Tap(BAR * 4 + BEAT, 0, 2);
            Tap(BAR * 4 + BEAT * 2, 9, 11);

            // Bar 6: a line of trace notes, a critical one in the middle.
            for (int i = 0; i < 6; i++) Trace(BAR * 5 + i * BEAT / 2, 2 + i, 4 + i, i == 4);
            Flick(BAR * 5 + BEAT * 3, 8, 10, NoteDirection.Right);

            // Bar 7: everything at once.
            Tap(BAR * 6, 2, 4, true);
            Tap(BAR * 6, 7, 9, true);
            Flick(BAR * 6 + BEAT, 0, 3, NoteDirection.Left);
            Flick(BAR * 6 + BEAT * 3 / 2, 8, 11, NoteDirection.Right);
            Tap(BAR * 6 + BEAT * 2, 4, 7);
            Tap(BAR * 6 + BEAT * 5 / 2, 4, 7);
            Flick(BAR * 6 + BEAT * 3, 3, 8, NoteDirection.Default, true);

            // Bar 8: a long hold with two relay ticks under taps, and a closing chord.
            Hold(BAR * 7, 0, 3, new[] { (BAR * 7 + BEAT, 3, 6), (BAR * 7 + BEAT * 2, 6, 9) }, BAR * 7 + BEAT * 3, 8, 11);
            Tap(BAR * 7, 8, 11);
            Tap(BAR * 7 + BEAT * 3, 0, 3, true);
            Tap(BAR * 8, 1, 4, true);
            Tap(BAR * 8, 7, 10, true);

            var document = new ChartDocument
            {
                NoteList = notes,
                MusicScoreTicksMax = BAR * 9,
                MusicScoreEventDataList = new List<ChartEvent>
                {
                    new ChartEvent { id = 1, eventType = ChartEventType.BPM, ticks = 0, changeValue = BPM },
                    new ChartEvent { id = 2, eventType = ChartEventType.HighSpeed, ticks = 0, changeValue = 1d },
                    new ChartEvent { id = 3, eventType = ChartEventType.SeVolume, ticks = 0, changeValue = 1d },
                    new ChartEvent { id = 4, eventType = ChartEventType.TimeSignature, ticks = 0, changeValue = "4/4" },
                }
            };
            return document;

            ChartNote Add(long ticks, int left, int right, NoteCategory category, ChartNote.NoteBaseType kind, bool critical,
                NoteDirection direction = NoteDirection.Default, ChartNote previous = null, bool skip = false)
            {
                var note = new ChartNote(id++, ticks, left, right, category, critical ? NoteType.Critical : NoteType.Default,
                    noteBaseType: kind, isSkip: skip, direction: direction, previousConnectionId: previous?.id ?? -1);
                if (previous != null) previous.nextConnectionId = note.id;
                notes.Add(note);
                return note;
            }

            void Tap(long ticks, int left, int right, bool critical = false) =>
                Add(ticks, left, right, NoteCategory.Normal, ChartNote.NoteBaseType.Normal, critical);

            void Flick(long ticks, int left, int right, NoteDirection direction, bool critical = false) =>
                Add(ticks, left, right, NoteCategory.Flick, ChartNote.NoteBaseType.Flick, critical, direction);

            void Trace(long ticks, int left, int right, bool critical = false) =>
                Add(ticks, left, right, NoteCategory.Friction, ChartNote.NoteBaseType.Friction, critical);

            void Hold(long start, int left, int right, (int ticks, int left, int right)[] relays, long end, int endLeft, int endRight,
                bool critical = false, NoteDirection? flickEnd = null)
            {
                ChartNote previous = Add(start, left, right, NoteCategory.Long, ChartNote.NoteBaseType.Long, critical);
                foreach (var relay in relays)
                    previous = Add(relay.ticks, relay.left, relay.right, NoteCategory.Connection, ChartNote.NoteBaseType.Connection, critical,
                        previous: previous);
                if (flickEnd.HasValue)
                    Add(end, endLeft, endRight, NoteCategory.Flick, ChartNote.NoteBaseType.Flick, critical, flickEnd.Value, previous);
                else
                    Add(end, endLeft, endRight, NoteCategory.Long, ChartNote.NoteBaseType.Normal, critical, previous: previous);
            }
        }
    }
}
