using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using Sekai;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The rest screen a running 段位 course shows between songs. It lives across scene loads and
    /// follows <see cref="DanChallengeService.Local"/>: for up to
    /// <see cref="DanChallengeService.REST_SECONDS"/> it shows the next song (jacket, title,
    /// difficulty), the life left and what the last song cost, every song of the course, 立即开始,
    /// 设置 (changes apply from the next song) and 放弃挑战. It stays up while the next song loads and
    /// fades once the new live scene is in. During a song it draws nothing: the live HUD's own life
    /// gauge already counts down the course's life. When the course ends it gets out of the way;
    /// the 段位 page shows the result.
    /// </summary>
    internal sealed class DanCourseOverlay : MonoBehaviour
    {
        private const float FADE = 0.35f;
        private const float QUIT_WINDOW = 3f;
        private const string THUMBNAIL_DIRECTORY = "DanJackets";

        private static DanCourseOverlay _instance;

        private readonly List<ProgressNode> _nodes = new List<ProgressNode>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private RectTransform _canvasRoot;
        private RectTransform _rest;
        private CanvasGroup _restGroup;
        private Image _restGlow;
        private Image _restAccent;
        private MedalPlate _plate;
        private TextMeshProUGUI _songCaption;
        private Image _jacketFrame;
        private Image _jacket;
        private TextMeshProUGUI _jacketMark;
        private TextMeshProUGUI _title;
        private RectTransform _difficulty;
        private TextMeshProUGUI _meta;
        private TextMeshProUGUI _life;
        private TextMeshProUGUI _lifeMax;
        private Image _lifeLost;
        private Image _lifeFill;
        private TextMeshProUGUI _lastSong;
        private RectTransform _progress;
        private Button _start;
        private TextMeshProUGUI _countdown;
        private Image _countdownFill;
        private Button _quit;
        private Button _settingsButton;
        private LibrarySettingsPanel _settings;
        private JacketThumbnails _thumbnails;
        private MotionHandle _fade;
        private DanChallengeService _service;
        private DanPhase _phase = DanPhase.Idle;
        private bool _restShown;
        private bool _leaving;
        private bool _preview;
        private int _jacketRequest;
        private float _quitArmedUntil = -1f;
        private float _backArmedUntil = -1f;

        /// <summary>The overlay, created the first time a course starts; it then follows every course.</summary>
        internal static DanCourseOverlay Ensure(DanChallengeService service = null)
        {
            if (_instance == null)
            {
                var host = new GameObject("DanCourseOverlay");
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<DanCourseOverlay>();
                _instance.Build();
            }
            _instance.Follow(service ?? DanChallengeService.Local);
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => _instance = null;

        internal bool IsRestShown => _restShown;

        // ---------------------------------------------------------------- tools (screenshots)

        /// <summary>Shows the rest screen with made-up state, detached from the service, for screenshots.</summary>
        internal void PreviewRest(DanDefinition definition, DanAttempt attempt, int songNumber, int life, IReadOnlyList<DanSongInfo> songs,
            float remaining)
        {
            _preview = true;
            BindRest(definition, attempt, songNumber, life, songs);
            SetCountdown(remaining);
            ShowRest(false);
        }

        /// <summary>Ends a preview and goes back to following the service.</summary>
        internal void EndPreview()
        {
            _preview = false;
            HideRest(false);
            Refresh();
        }

        // ---------------------------------------------------------------- following the course

        private void OnEnable()
        {
            UnitySceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnitySceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Follow(DanChallengeService service)
        {
            if (_service == service) return;
            if (_service != null) _service.Changed -= Refresh;
            _service = service;
            _service.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (_preview) return;
            DanChallengeService service = _service;
            DanPhase previous = _phase;
            _phase = service?.Phase ?? DanPhase.Idle;
            switch (_phase)
            {
                case DanPhase.BetweenSongs:
                {
                    _leaving = false;
                    var songs = new List<DanSongInfo>();
                    for (int i = 0; i < service.Definition.Songs.Count; i++)
                    {
                        songs.Add(DanSongInfo.FromDirectory(service.SongDirectory(i + 1), service.Definition.Songs[i]));
                    }

                    if (previous != DanPhase.BetweenSongs)
                    {
                        BindRest(service.Definition, service.Attempt, service.SongNumber, service.RemainingLife, songs);
                        ShowRest(true);
                        UiSound.Play(UiCue.Open);
                    }

                    break;
                }
                case DanPhase.StartingSong:
                    if (previous == DanPhase.Playing && service.SongNumber == 1)
                    {
                        var song = DanSongInfo.FromDirectory(service.SongDirectory(1), service.Definition.Songs[0]);
                        // Owned by the outgoing Core scene while the first song is prepared again.
                        var loading = LibraryLoadingOverlay.Create(null, service.Quit);
                        loading.Show(song.Title, "正在载入第 1 首…", null, DanStyles.ParseColor(service.Definition.Color));
                        RequestJacket(song, thumbnail =>
                        {
                            if (loading != null) loading.SetArtwork(thumbnail.Sprite, thumbnail.Palette.Accent);
                        });
                    }
                    else if (_restShown) SetLoading("正在载入第 " + service.SongNumber + " 首…");
                    break;
                case DanPhase.Playing:
                    if (_restShown) SetLoading("正在进入第 " + service.SongNumber + " 首…");
                    break;
                case DanPhase.Finished:
                case DanPhase.Error:
                    _settings?.Hide();
                    if (_restShown)
                    {
                        // Hold the cover over the old live until the library scene replaces it.
                        _leaving = true;
                        SetLoading("正在结算…");
                    }

                    break;
                default:
                    HideRest(false);
                    break;
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (_preview) return;
            if (scene.name == "Core")
            {
                if (_restShown && (_phase == DanPhase.Playing || _phase == DanPhase.StartingSong)) HideRest(true);
                return;
            }

            if (scene.name == "Library" || scene.name == "TransitionBlank")
            {
                if (scene.name == "Library" || !_leaving) HideRest(scene.name == "Library");
            }
        }

        private void Update()
        {
            if (_preview || !_restShown) return;
            DanChallengeService service = _service;
            if (service == null) return;
            if (service.Phase == DanPhase.BetweenSongs)
            {
                SetCountdown(service.RestRemaining);
                if (_quitArmedUntil > 0f && Time.unscaledTime > _quitArmedUntil) DisarmQuit();
            }

            if (!NativeInput.BackPressedThisFrame || DialogBase.OwnsHardwareBack) return;
            if (_settings != null && _settings.IsOpen)
            {
                _settings.Back();
                return;
            }

            if (service.Phase != DanPhase.BetweenSongs) return;
            if (Time.unscaledTime < _backArmedUntil)
            {
                service.Quit();
                return;
            }

            _backArmedUntil = Time.unscaledTime + QUIT_WINDOW;
            ArmQuit();
        }

        // ---------------------------------------------------------------- actions

        private void StartNow()
        {
            if (_service == null || _service.Phase != DanPhase.BetweenSongs) return;
            _settings?.Hide();
            UiSound.Play(UiCue.Start);
            _service.ContinueNow();
            SetLoading("正在载入第 " + _service.SongNumber + " 首…");
        }

        private void OnQuitClicked()
        {
            if (_service == null || _service.Phase != DanPhase.BetweenSongs) return;
            if (Time.unscaledTime < _quitArmedUntil)
            {
                UiSound.Play(UiCue.Back);
                _settings?.Hide();
                _service.Quit();
                return;
            }

            ArmQuit();
        }

        private void ArmQuit()
        {
            UiSound.Play(UiCue.Warning);
            _quitArmedUntil = Time.unscaledTime + QUIT_WINDOW;
            ServerWidgets.SetLabelledButton(_quit, "再点一次放弃", LibraryIcon.Close, LibraryTheme.Colors.DangerSurface, LibraryTheme.Colors.Danger);
        }

        private void DisarmQuit()
        {
            _quitArmedUntil = -1f;
            ServerWidgets.SetLabelledButton(_quit, "放弃挑战", LibraryIcon.Close, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary);
        }

        private void OpenSettings()
        {
            if (_settings == null) _settings = LibrarySettingsPanel.Create(_canvasRoot, null);
            _settings.transform.SetAsLastSibling();
            _settings.Show();
        }

        // ---------------------------------------------------------------- binding

        private void BindRest(DanDefinition definition, DanAttempt attempt, int songNumber, int life, IReadOnlyList<DanSongInfo> songs)
        {
            DisarmQuit();
            _backArmedUntil = -1f;
            Color32 tint = DanStyles.ParseColor(definition.Color);
            _restGlow.color = HomeTheme.Colors.WithAlpha(tint, 54);
            _plate.SetLook(MedalLook.Of(definition));
            _plate.SetLocked(false);
            int count = definition.Songs.Count;
            _songCaption.text = "下一首  ·  第 " + songNumber + " / " + count + " 首";

            DanSongInfo next = songNumber >= 1 && songNumber <= songs.Count ? songs[songNumber - 1] : null;
            _title.text = next?.Title ?? "第 " + songNumber + " 首";
            bool hasDifficulty = next?.Difficulty != null;
            _difficulty.gameObject.SetActive(hasDifficulty);
            if (hasDifficulty) PackWidgets.SetTag(_difficulty, next.Difficulty, LibraryTheme.Colors.Difficulty(next.DifficultyType));
            _meta.text = next?.Charter != null ? "谱师  " + next.Charter : next?.Host ?? string.Empty;
            _meta.rectTransform.anchoredPosition = new Vector2(hasDifficulty ? _difficulty.sizeDelta.x + 16f : 0f, _meta.rectTransform.anchoredPosition.y);
            _jacketMark.text = songNumber.ToString(CultureInfo.InvariantCulture);
            _jacket.sprite = null;
            _jacket.enabled = false;
            _jacketMark.enabled = true;
            _jacketFrame.color = Color.Lerp(HomeTheme.Colors.Card, tint, 0.2f);
            _restAccent.color = HomeTheme.Colors.WithAlpha(tint, 30);
            RequestJacket(next, thumbnail =>
            {
                _jacket.sprite = thumbnail.Sprite;
                _jacket.enabled = true;
                _jacketMark.enabled = false;
                _restAccent.color = HomeTheme.Colors.WithAlpha(thumbnail.Palette.Glow, 46);
            });

            _life.text = DanCopy.Number(life);
            _life.color = life * 5 <= definition.InitialLife ? DanCopy.LifeLow : LibraryTheme.Colors.TextPrimary;
            _lifeMax.text = "/ " + DanCopy.Number(definition.InitialLife);
            float width = Mathf.Ceil(_life.GetPreferredValues(_life.text).x);
            _lifeMax.rectTransform.anchoredPosition = new Vector2(_life.rectTransform.anchoredPosition.x + width + 12f, _lifeMax.rectTransform.anchoredPosition.y);
            float initial = Mathf.Max(1, definition.InitialLife);
            DanSongResult last = attempt != null && attempt.Songs.Count > 0 ? attempt.Songs[attempt.Songs.Count - 1] : null;
            float before = last != null ? last.StartingLife / initial : life / initial;
            _lifeLost.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(before), 1f);
            _lifeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(life / initial), 1f);
            _lifeFill.color = DanCopy.LifeColor(life, definition.InitialLife);
            if (last != null)
            {
                int lost = last.StartingLife - last.Score.life;
                _lastSong.text = "上一首「" + last.Title + "」" + (lost > 0 ? "  −" + DanCopy.Number(lost) : "  未扣血")
                    + "  ·  " + DanCopy.Number(last.Score.totalScore) + " 分  ·  MISS " + DanCopy.Number(last.Score.missCount);
            }
            else
            {
                _lastSong.text = string.Empty;
            }

            BindProgress(definition, attempt, songNumber, songs);
            PackWidgets.SetEnabled(_start, true);
            PackWidgets.SetEnabled(_quit, true);
            PackWidgets.SetEnabled(_settingsButton, true);
        }

        private void BindProgress(DanDefinition definition, DanAttempt attempt, int songNumber, IReadOnlyList<DanSongInfo> songs)
        {
            int count = definition.Songs.Count;
            while (_nodes.Count < count) _nodes.Add(new ProgressNode(_progress, _nodes.Count));
            float width = _progress.rect.width > 0f ? _progress.rect.width : 1000f;
            float step = count > 1 ? Mathf.Min(220f, width / (count - 1)) : 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                bool used = i < count;
                _nodes[i].Root.gameObject.SetActive(used);
                if (!used) continue;
                int number = i + 1;
                DanSongResult song = attempt != null && i < attempt.Songs.Count ? attempt.Songs[i] : null;
                string title = song?.Title ?? (i < songs.Count ? songs[i].Title : null);
                _nodes[i].Root.anchoredPosition = new Vector2(i * step, 0f);
                _nodes[i].Bind(number, title, song, number == songNumber, DanStyles.ParseColor(definition.Color),
                    i < count - 1 ? step : 0f, count > 1 ? Mathf.Min(200f, step - 10f) : 200f);
            }
        }

        private void SetCountdown(float remaining)
        {
            float seconds = Mathf.Max(0f, remaining);
            _countdown.text = Mathf.CeilToInt(seconds).ToString(CultureInfo.InvariantCulture) + " 秒后自动开始";
            _countdownFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(seconds / DanChallengeService.REST_SECONDS), 1f);
        }

        private void SetLoading(string message)
        {
            _countdown.text = message;
            _countdownFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            PackWidgets.SetEnabled(_start, false);
            PackWidgets.SetEnabled(_quit, false);
            PackWidgets.SetEnabled(_settingsButton, false);
            _settings?.Hide();
        }

        private void RequestJacket(DanSongInfo info, Action<JacketThumbnail> apply)
        {
            int request = ++_jacketRequest;
            if (info?.JacketPath == null) return;
            LoadJacketAsync(info.JacketPath, info.JacketVersion, request, apply).Forget();
        }

        private async UniTaskVoid LoadJacketAsync(string path, long version, int request, Action<JacketThumbnail> apply)
        {
            JacketThumbnail thumbnail;
            try
            {
                thumbnail = await _thumbnails.LoadAsync(path, version, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Dan] Could not load the next jacket: " + exception.Message);
                return;
            }

            if (this == null || request != _jacketRequest || thumbnail.Sprite == null) return;
            apply(thumbnail);
        }

        // ---------------------------------------------------------------- showing

        private void ShowRest(bool animate)
        {
            _restShown = true;
            _fade.TryCancel();
            _rest.gameObject.SetActive(true);
            _restGroup.blocksRaycasts = true;
            if (!animate)
            {
                _restGroup.alpha = 1f;
                return;
            }

            _fade = LMotion.Create(_restGroup.alpha, 1f, FADE)
                .WithEase(Ease.OutQuad)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(alpha => _restGroup.alpha = alpha);
        }

        private void HideRest(bool animate)
        {
            _settings?.Hide();
            _fade.TryCancel();
            if (!_restShown)
            {
                _rest.gameObject.SetActive(false);
                return;
            }

            _restShown = false;
            _leaving = false;
            _restGroup.blocksRaycasts = false;
            if (!animate)
            {
                _restGroup.alpha = 0f;
                _rest.gameObject.SetActive(false);
                return;
            }

            // Let the new scene draw its first frames under the cover before it lifts.
            _fade = LMotion.Create(_restGroup.alpha, 0f, FADE)
                .WithDelay(0.25f)
                .WithEase(Ease.InQuad)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _rest.gameObject.SetActive(false))
                .Bind(alpha => _restGroup.alpha = alpha);
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            var canvasObject = new GameObject("DanCourseCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Over the live scene left behind between songs; it is never up at the same time as the pause card (200).
            canvas.sortingOrder = 150;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            _canvasRoot = (RectTransform)canvasObject.transform;
            _thumbnails = new JacketThumbnails(Path.Combine(Application.temporaryCachePath, THUMBNAIL_DIRECTORY), JacketThumbnails.DETAIL_SIZE);

            BuildRest();
            _rest.gameObject.SetActive(false);
        }

        private void BuildRest()
        {
            _rest = LibraryUi.CreateRect("Rest", _canvasRoot);
            LibraryUi.Stretch(_rest);
            _restGroup = _rest.gameObject.AddComponent<CanvasGroup>();
            Image ground = LibraryUi.CreateImage("Ground", _rest, LibraryTheme.Colors.Ground);
            ground.raycastTarget = true;
            LibraryUi.Stretch(ground.rectTransform);
            _restGlow = Glow(_rest, "GlowPlate", new Vector2(0f, 1f), new Vector2(380f, -300f), 1800f);
            _restAccent = Glow(_rest, "GlowJacket", new Vector2(1f, 0f), new Vector2(-300f, 200f), 1700f);
            Glow(_rest, "GlowViolet", new Vector2(0.6f, 1f), new Vector2(0f, 80f), 1100f).color = HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Violet, 18);

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            Vector2 topLeft = new Vector2(0f, 1f);
            RectTransform bar = LibraryUi.CreateRect("TopBar", _rest);
            LibraryWidgets.TopStrip(bar, margin, margin, 28f, 84f);
            _plate = new MedalPlate(bar, "Plate", new Vector2(360f, 72f));
            LibraryWidgets.Place(_plate.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, _plate.Root.sizeDelta);
            var eyebrow = LibraryWidgets.Label("Eyebrow", bar, "休息  ·  REST", 22f, LibraryTheme.Colors.TextSecondary);
            LibraryUi.UseSpacedCondensedFont(eyebrow, 18f);
            LibraryWidgets.Place(eyebrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(392f, 0f), new Vector2(520f, 36f));
            float control = LibraryTheme.Layout.CONTROL_HEIGHT;
            _settingsButton = ServerWidgets.LabelledButton(bar, "Settings", "设置", LibraryIcon.Settings, OpenSettings, 168f, control,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Open);
            LibraryWidgets.Outline((Image)_settingsButton.targetGraphic, control * 0.5f);
            LibraryWidgets.Place((RectTransform)_settingsButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(168f, control));
            TextMeshProUGUI settingsNote = LibraryWidgets.Label("SettingsNote", bar, "设置从下一首开始生效", 17f, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Place(settingsNote.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-188f, 0f), new Vector2(300f, 30f));

            // ---------------------------------------------------------------- next song
            RectTransform main = LibraryUi.CreateRect("Main", _rest);
            LibraryWidgets.Inset(main, margin, 250f, margin, 150f);
            const float JACKET = 360f;
            _jacketFrame = LibraryWidgets.Panel("JacketFrame", main, 24f, HomeTheme.Colors.Card);
            LibraryWidgets.Place(_jacketFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(JACKET, JACKET));
            LibraryWidgets.Shadow(_jacketFrame.rectTransform, 24f, LibraryTheme.CoverShadow.BLUR, 8f, 110);
            _jacketFrame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _jacketMark = LibraryWidgets.Label("Mark", _jacketFrame.transform, "2", 160f, HomeTheme.Colors.WithAlpha(Color.white, 60),
                FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_jacketMark);
            _jacketMark.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(_jacketMark.rectTransform);
            _jacket = LibraryUi.CreateImage("Jacket", _jacketFrame.transform, Color.white);
            _jacket.raycastTarget = false;
            _jacket.preserveAspect = false;
            LibraryUi.Stretch(_jacket.rectTransform);
            Image rim = LibraryWidgets.Shape("Rim", _jacketFrame.transform, LibraryShapes.Ring(24f, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(rim.rectTransform);

            RectTransform copy = LibraryUi.CreateRect("Copy", main);
            copy.anchorMin = new Vector2(0f, 0.5f);
            copy.anchorMax = new Vector2(1f, 0.5f);
            copy.pivot = new Vector2(0f, 0.5f);
            copy.offsetMin = new Vector2(JACKET + 52f, -JACKET * 0.5f);
            copy.offsetMax = new Vector2(-620f, JACKET * 0.5f);
            _songCaption = LibraryWidgets.Label("SongCaption", copy, string.Empty, 24f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold);
            LibraryWidgets.TopStrip(_songCaption.rectTransform, 0f, 0f, 40f, 36f);
            _title = PackWidgets.UserLabel("Title", copy, string.Empty, 60f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.maxVisibleLines = 2;
            _title.alignment = TextAlignmentOptions.TopLeft;
            LibraryWidgets.TopStrip(_title.rectTransform, 0f, 0f, 90f, 160f);
            _difficulty = PackWidgets.Tag(copy, "MASTER", LibraryTheme.Colors.TextSecondary, out _, 40f);
            LibraryWidgets.Place(_difficulty, topLeft, topLeft, new Vector2(0f, -268f), _difficulty.sizeDelta);
            _meta = PackWidgets.UserLabel("Meta", copy, string.Empty, 21f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(_meta.rectTransform, topLeft, topLeft, new Vector2(0f, -272f), new Vector2(600f, 32f));

            // ---------------------------------------------------------------- life
            Image lifeCard = LibraryWidgets.Panel("LifeCard", main, 30f, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Card, 230));
            LibraryWidgets.Outline(lifeCard, 30f);
            LibraryWidgets.Place(lifeCard.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(580f, 300f));
            Transform life = lifeCard.transform;
            TextMeshProUGUI lifeCaption = LibraryWidgets.Label("Caption", life, "剩余生命", 20f, LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            LibraryWidgets.TopStrip(lifeCaption.rectTransform, 36f, 36f, 30f, 30f);
            _life = LibraryWidgets.Label("Life", life, "0", 104f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(_life);
            _life.overflowMode = TextOverflowModes.Overflow;
            _life.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.Place(_life.rectTransform, topLeft, topLeft, new Vector2(32f, -56f), new Vector2(300f, 110f));
            _lifeMax = LibraryWidgets.Label("Max", life, string.Empty, 36f, LibraryTheme.Colors.TextMuted);
            LibraryUi.UseCondensedFont(_lifeMax);
            _lifeMax.overflowMode = TextOverflowModes.Overflow;
            _lifeMax.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.Place(_lifeMax.rectTransform, topLeft, topLeft, new Vector2(32f, -76f), new Vector2(300f, 88f));
            Image track = LibraryWidgets.Panel("Track", life, 8f, HomeTheme.Colors.Chip);
            LibraryWidgets.TopStrip(track.rectTransform, 36f, 36f, 186f, 16f);
            _lifeLost = LibraryWidgets.Panel("Lost", track.transform, 8f, HomeTheme.Colors.WithAlpha(LibraryTheme.Colors.Danger, 130));
            _lifeFill = LibraryWidgets.Panel("Fill", track.transform, 8f, DanCopy.Life);
            foreach (Image part in new[] { _lifeLost, _lifeFill })
            {
                part.rectTransform.anchorMin = Vector2.zero;
                part.rectTransform.anchorMax = Vector2.one;
                part.rectTransform.offsetMin = Vector2.zero;
                part.rectTransform.offsetMax = Vector2.zero;
            }

            _lastSong = PackWidgets.Paragraph(life, "LastSong", string.Empty, 18f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(_lastSong.rectTransform, 36f, 36f, 220f, 56f);

            // ---------------------------------------------------------------- progress and buttons
            RectTransform bottom = LibraryUi.CreateRect("Bottom", _rest);
            LibraryWidgets.BottomStrip(bottom, margin, margin, 56f, 150f);
            _progress = LibraryUi.CreateRect("Progress", bottom);
            _progress.anchorMin = new Vector2(0f, 0f);
            _progress.anchorMax = new Vector2(1f, 1f);
            _progress.offsetMin = new Vector2(30f, 0f);
            _progress.offsetMax = new Vector2(-760f, 0f);

            const float BUTTON_HEIGHT = 92f;
            _start = ServerWidgets.LabelledButton(bottom, "Start", "立即开始", LibraryIcon.Play, StartNow, 380f, BUTTON_HEIGHT,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None, 26f);
            LibraryWidgets.Place((RectTransform)_start.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 38f), new Vector2(380f, BUTTON_HEIGHT));
            _countdown = LibraryWidgets.Label("Countdown", bottom, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryWidgets.Place(_countdown.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(380f, 28f));
            Image countdownTrack = LibraryWidgets.Panel("CountdownTrack", _start.transform, 3f, new Color32(12, 15, 20, 40));
            LibraryWidgets.BottomStrip(countdownTrack.rectTransform, 46f, 46f, 12f, 6f);
            _countdownFill = LibraryWidgets.Panel("Fill", countdownTrack.transform, 3f, new Color32(12, 15, 20, 140));
            _countdownFill.rectTransform.anchorMin = Vector2.zero;
            _countdownFill.rectTransform.anchorMax = Vector2.one;
            _countdownFill.rectTransform.offsetMin = Vector2.zero;
            _countdownFill.rectTransform.offsetMax = Vector2.zero;
            _quit = ServerWidgets.LabelledButton(bottom, "Quit", "放弃挑战", LibraryIcon.Close, OnQuitClicked, 300f, BUTTON_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None, 22f);
            LibraryWidgets.Outline((Image)_quit.targetGraphic, BUTTON_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)_quit.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-400f, 38f), new Vector2(300f, BUTTON_HEIGHT));
        }

        private static Image Glow(RectTransform root, string name, Vector2 anchor, Vector2 position, float size)
        {
            Image glow = LibraryUi.CreateImage(name, root, Color.clear);
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            return glow;
        }

        private void OnDestroy()
        {
            if (_service != null) _service.Changed -= Refresh;
            _fade.TryCancel();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _plate?.Dispose();
            _thumbnails?.Dispose();
            if (_instance == this) _instance = null;
        }

        /// <summary>One song of the course on the rest screen's strip: done, next or still to come.</summary>
        private sealed class ProgressNode
        {
            private const float SIZE = 46f;

            private readonly Image _line;
            private readonly Image _disc;
            private readonly Image _ring;
            private readonly TextMeshProUGUI _number;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _detail;

            internal ProgressNode(RectTransform parent, int index)
            {
                Root = LibraryUi.CreateRect("Node" + index, parent);
                LibraryWidgets.Place(Root, new Vector2(0f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(SIZE, SIZE));
                _line = LibraryUi.CreateImage("Line", Root, HomeTheme.Colors.Chip);
                _line.raycastTarget = false;
                LibraryWidgets.Place(_line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(SIZE * 0.5f + 8f, 0f), new Vector2(10f, 3f));
                _ring = LibraryWidgets.Shape("Ring", Root, LibraryShapes.Ring((SIZE + 14f) * 0.5f, 2.5f), Color.clear);
                LibraryWidgets.Place(_ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SIZE + 14f, SIZE + 14f));
                _disc = LibraryWidgets.Panel("Disc", Root, SIZE * 0.5f, HomeTheme.Colors.Chip);
                LibraryUi.Stretch(_disc.rectTransform);
                _number = LibraryWidgets.Label("Number", _disc.transform, "1", 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                    TextAlignmentOptions.Center);
                LibraryUi.UseCondensedFont(_number);
                _number.overflowMode = TextOverflowModes.Overflow;
                LibraryUi.Stretch(_number.rectTransform);
                _title = PackWidgets.UserLabel("Title", Root, string.Empty, 17f, LibraryTheme.Colors.TextSecondary, FontStyles.Bold,
                    TextAlignmentOptions.Center);
                LibraryWidgets.Place(_title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(200f, 26f));
                _detail = LibraryWidgets.Label("Detail", Root, string.Empty, 15f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                    TextAlignmentOptions.Center);
                LibraryWidgets.Place(_detail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(200f, 22f));
            }

            internal RectTransform Root { get; }

            /// <param name="gap">Distance to the next node, for the line between them; 0 for the last.</param>
            /// <param name="labelWidth">Room for the title and detail under the node, so neighbours never overlap.</param>
            internal void Bind(int number, string title, DanSongResult song, bool next, Color32 tint, float gap, float labelWidth)
            {
                _number.text = number.ToString(CultureInfo.InvariantCulture);
                _title.text = title ?? string.Empty;
                _title.rectTransform.sizeDelta = new Vector2(labelWidth, 26f);
                _detail.rectTransform.sizeDelta = new Vector2(labelWidth, 22f);
                _line.gameObject.SetActive(gap > 0f);
                _line.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, gap - SIZE - 16f), 3f);
                bool done = song != null;
                _line.color = done ? HomeTheme.Colors.WithAlpha(DanCopy.Life, 140) : HomeTheme.Colors.Chip;
                _disc.color = done ? HomeTheme.Colors.WithAlpha(DanCopy.Life, 70) : next ? (Color32)tint : HomeTheme.Colors.Chip;
                _ring.color = next ? (Color)HomeTheme.Colors.WithAlpha(DanStyles.Light(tint), 200) : Color.clear;
                _number.color = next || done ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextMuted;
                _title.color = next ? LibraryTheme.Colors.TextPrimary : done ? LibraryTheme.Colors.TextSecondary : LibraryTheme.Colors.TextMuted;
                _detail.text = done ? DanCopy.Number(song.Score.totalScore) + " 分" : next ? "下一首" : string.Empty;
                _detail.color = next ? DanStyles.Light(tint) : LibraryTheme.Colors.TextMuted;
            }
        }
    }
}
