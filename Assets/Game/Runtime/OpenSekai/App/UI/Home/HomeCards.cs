using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The frame every home card shares: a rounded surface that clips what is inside it, a soft
    /// shadow, a hairline, and press feedback on the whole card. <see cref="Root"/> is what the
    /// page lays out and animates in; <see cref="Body"/> is what shrinks under a finger, so the two
    /// motions never fight over one transform.
    /// </summary>
    internal sealed class HomeCardFrame
    {
        internal HomeCardFrame(Transform parent, string name, float radius, Action onClick, float pressScale)
        {
            Root = LibraryUi.CreateRect(name, parent);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            Body = LibraryUi.CreateRect("Body", Root);
            LibraryUi.Stretch(Body);

            Shadow = LibraryWidgets.Shadow(Body, radius, 48f, 18f, 150);
            Surface = LibraryWidgets.Panel("Surface", Body, radius, HomeTheme.Colors.Card);
            Surface.raycastTarget = true;
            LibraryUi.Stretch(Surface.rectTransform);
            Surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // Light from above: a soft wash down from the top edge, clipped to the card's corners.
            Image highlight = LibraryWidgets.Shape("Highlight", Surface.transform, HomeShapes.FadeUp(), HomeTheme.Colors.CardHighlight);
            highlight.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(highlight.rectTransform, 0f, 0f, 0f, 140f);

            Content = LibraryUi.CreateRect("Content", Surface.transform);
            LibraryUi.Stretch(Content);

            Outline = LibraryWidgets.Shape("Outline", Body, LibraryShapes.Ring(radius, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(Outline.rectTransform);

            Button = Surface.gameObject.AddComponent<Button>();
            Button.targetGraphic = Surface;
            Button.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                Button.onClick.AddListener(() => onClick());
            }

            LibraryPressFeedback.Attach(Surface.gameObject, Body, pressScale);
        }

        internal RectTransform Root { get; }
        internal CanvasGroup Group { get; }
        internal RectTransform Body { get; }
        internal Image Shadow { get; }
        internal Image Surface { get; }
        internal Image Outline { get; }
        internal RectTransform Content { get; }
        internal Button Button { get; }
    }

    /// <summary>
    /// The way into the library, the page's biggest card: a slow two-way marquee of the player's
    /// jackets behind the copy, how many packages and plays there are, the last song played, and a
    /// call to action in the accent of that song's jacket. The card grows over the whole page when
    /// tapped, which is how the page hands over to the library.
    /// </summary>
    internal sealed class HomeLibraryCard
    {
        private const int COLUMNS = 3;
        private const int ROWS = 6;
        private const float TEXT_WIDTH = 600f;
        private const float CTA_WIDTH = 252f;
        private const float CTA_HEIGHT = 80f;

        private readonly HomeCardFrame _frame;
        private readonly Image _glow;
        private readonly CanvasGroup _art;
        private readonly RectTransform _marquee;
        private readonly RectTransform[] _columns = new RectTransform[COLUMNS];
        private readonly Image[] _jackets = new Image[COLUMNS * ROWS];
        private readonly GameObject[] _placeholders = new GameObject[COLUMNS * ROWS];
        private readonly Image _cover;
        private readonly Image _coverFade;
        private readonly CanvasGroup _copy;
        private readonly TextMeshProUGUI _eyebrow;
        private readonly TextMeshProUGUI _description;
        private readonly TextMeshProUGUI _packageCount;
        private readonly TextMeshProUGUI _playCount;
        private readonly TextMeshProUGUI _recentCaption;
        private readonly TextMeshProUGUI _recentTitle;
        private readonly Image _recentJacket;
        private readonly RectTransform _recentJacketFrame;
        private readonly Image _cta;
        private readonly Image _ctaGlow;
        private HomeLibrarySummary _summary = HomeLibrarySummary.Empty;
        private float _counted = 1f;
        private int _shownPackages = -1;
        private int _shownPlays = -1;

        internal HomeLibraryCard(Transform parent, Action onClick)
        {
            _frame = new HomeCardFrame(parent, "LibraryCard", HomeTheme.Layout.HERO_RADIUS, onClick, 0.985f);
            RectTransform content = _frame.Content;

            _glow = LibraryUi.CreateImage("Glow", content, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            LibraryWidgets.Place(_glow.rectTransform, new Vector2(0.86f, 0.82f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1250f, 1250f));

            // Jacket marquee: three tilted columns drifting in alternate directions.
            RectTransform artRoot = LibraryUi.CreateRect("Art", content);
            LibraryUi.Stretch(artRoot);
            // Its own canvas: the tiles move every frame, and the rest of the page should not be
            // re-batched for it. Nested canvases keep the card's rounded mask.
            artRoot.gameObject.AddComponent<Canvas>();
            _art = artRoot.gameObject.AddComponent<CanvasGroup>();
            _art.blocksRaycasts = false;
            _marquee = LibraryUi.CreateRect("Marquee", artRoot);
            float pitch = HomeTheme.Layout.MARQUEE_TILE + HomeTheme.Layout.MARQUEE_GAP;
            LibraryWidgets.Place(_marquee, new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(pitch * COLUMNS, pitch * ROWS));
            _marquee.localEulerAngles = new Vector3(0f, 0f, HomeTheme.Layout.MARQUEE_TILT);
            for (int column = 0; column < COLUMNS; column++)
            {
                RectTransform strip = LibraryUi.CreateRect("Column" + column, _marquee);
                LibraryWidgets.Place(strip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((column - (COLUMNS - 1) * 0.5f) * pitch, 0f), new Vector2(pitch, pitch * ROWS));
                _columns[column] = strip;
                for (int row = 0; row < ROWS; row++)
                {
                    int index = column * ROWS + row;
                    Image tile = LibraryWidgets.Panel("Tile" + row, strip, 22f, LibraryTheme.Colors.CoverPlaceholder);
                    tile.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                    LibraryWidgets.Place(tile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(HomeTheme.Layout.MARQUEE_TILE, HomeTheme.Layout.MARQUEE_TILE));
                    _placeholders[index] = LibraryWidgets.Icon(tile.transform, LibraryIcon.Wave,
                        HomeTheme.Colors.WithAlpha(LibraryTheme.Colors.TextMuted, 90), 64f).gameObject;
                    Image jacket = LibraryUi.CreateImage("Jacket", tile.transform, Color.white);
                    jacket.raycastTarget = false;
                    jacket.enabled = false;
                    LibraryUi.Stretch(jacket.rectTransform);
                    _jackets[index] = jacket;
                }
            }

            // The copy sits on a solid band that fades out over the marquee.
            _cover = LibraryUi.CreateImage("CopyBand", content, HomeTheme.Colors.Card);
            _cover.raycastTarget = false;
            _cover.rectTransform.anchorMin = Vector2.zero;
            _cover.rectTransform.anchorMax = new Vector2(0f, 1f);
            _cover.rectTransform.pivot = new Vector2(0f, 0.5f);
            _cover.rectTransform.offsetMin = Vector2.zero;
            _cover.rectTransform.offsetMax = new Vector2(TEXT_WIDTH, 0f);
            _coverFade = LibraryUi.CreateImage("CopyFade", content, HomeTheme.Colors.Card);
            _coverFade.sprite = HomeShapes.FadeRight();
            _coverFade.raycastTarget = false;
            _coverFade.rectTransform.anchorMin = Vector2.zero;
            _coverFade.rectTransform.anchorMax = new Vector2(0f, 1f);
            _coverFade.rectTransform.pivot = new Vector2(0f, 0.5f);
            _coverFade.rectTransform.offsetMin = new Vector2(TEXT_WIDTH - 1f, 0f);
            _coverFade.rectTransform.offsetMax = new Vector2(TEXT_WIDTH + 300f, 0f);

            RectTransform copyRoot = LibraryUi.CreateRect("Copy", content);
            LibraryUi.Stretch(copyRoot);
            _copy = copyRoot.gameObject.AddComponent<CanvasGroup>();
            _copy.blocksRaycasts = false;
            float pad = HomeTheme.Layout.HERO_PADDING;
            Vector2 topLeft = new Vector2(0f, 1f);
            Vector2 bottomLeft = Vector2.zero;

            _eyebrow = LibraryWidgets.Label("Eyebrow", copyRoot, "LIBRARY", 24f, LibraryTheme.Colors.DefaultAccent);
            LibraryUi.UseCondensedFont(_eyebrow);
            _eyebrow.characterSpacing = 30f;
            LibraryWidgets.Place(_eyebrow.rectTransform, topLeft, topLeft, new Vector2(pad, -pad + 4f), new Vector2(TEXT_WIDTH - pad, 30f));

            TextMeshProUGUI title = LibraryWidgets.Label(
                "Title", copyRoot, "曲库", 96f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Place(title.rectTransform, topLeft, topLeft, new Vector2(pad - 4f, -pad - 34f), new Vector2(TEXT_WIDTH, 120f));

            _description = LibraryWidgets.Label(
                "Description", copyRoot, "导入曲包 · 试听预览 · 开始演奏", 23f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.Place(_description.rectTransform, topLeft, topLeft, new Vector2(pad, -pad - 160f),
                new Vector2(TEXT_WIDTH - pad - 24f, 34f));

            // Call to action: drawn only, the whole card takes the tap.
            _ctaGlow = LibraryUi.CreateImage("CtaGlow", copyRoot, Color.clear);
            _ctaGlow.sprite = LibraryShapes.Glow();
            _ctaGlow.raycastTarget = false;
            LibraryWidgets.Place(_ctaGlow.rectTransform, bottomLeft, new Vector2(0.5f, 0.5f),
                new Vector2(pad + CTA_WIDTH * 0.5f, pad + CTA_HEIGHT * 0.5f - 6f), new Vector2(CTA_WIDTH * 1.9f, CTA_HEIGHT * 2.6f));
            _cta = LibraryWidgets.Panel("Cta", copyRoot, CTA_HEIGHT * 0.5f, LibraryTheme.Colors.DefaultAccent);
            LibraryWidgets.Place(_cta.rectTransform, bottomLeft, bottomLeft, new Vector2(pad, pad), new Vector2(CTA_WIDTH, CTA_HEIGHT));
            TextMeshProUGUI ctaLabel = LibraryWidgets.Label(
                "Label", _cta.transform, "进入曲库", 26f, LibraryTheme.Colors.TextOnAccent, FontStyles.Bold, TextAlignmentOptions.Left);
            LibraryWidgets.Inset(ctaLabel.rectTransform, 44f, 0f, 60f, 0f);
            Image arrow = LibraryWidgets.Icon(_cta.transform, LibraryIcon.Forward, LibraryTheme.Colors.TextOnAccent, 28f);
            LibraryWidgets.Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-38f, 0f), new Vector2(28f, 28f));

            // Last song played.
            float recentBottom = pad + CTA_HEIGHT + 34f;
            _recentJacketFrame = LibraryUi.CreateRect("RecentJacket", copyRoot);
            LibraryWidgets.Place(_recentJacketFrame, bottomLeft, bottomLeft, new Vector2(pad, recentBottom), new Vector2(56f, 56f));
            Image recentFrame = LibraryWidgets.Panel("Frame", _recentJacketFrame, 12f, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(recentFrame.rectTransform);
            recentFrame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            LibraryWidgets.Icon(recentFrame.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 24f);
            _recentJacket = LibraryUi.CreateImage("Jacket", recentFrame.transform, Color.white);
            _recentJacket.raycastTarget = false;
            _recentJacket.enabled = false;
            LibraryUi.Stretch(_recentJacket.rectTransform);

            _recentCaption = LibraryWidgets.Label("RecentCaption", copyRoot, "最近游玩", 17f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(_recentCaption.rectTransform, bottomLeft, bottomLeft, new Vector2(pad + 72f, recentBottom + 30f),
                new Vector2(TEXT_WIDTH - pad - 96f, 24f));
            _recentTitle = LibraryWidgets.Label("RecentTitle", copyRoot, string.Empty, 22f, LibraryTheme.Colors.TextPrimary);
            LibraryWidgets.Place(_recentTitle.rectTransform, bottomLeft, bottomLeft, new Vector2(pad + 72f, recentBottom - 2f),
                new Vector2(TEXT_WIDTH - pad - 96f, 32f));

            // Two big numbers above it.
            float statsBottom = recentBottom + 56f + 34f;
            _packageCount = Stat(copyRoot, "Packages", "曲包", new Vector2(pad, statsBottom));
            _playCount = Stat(copyRoot, "Plays", "次游玩", new Vector2(pad + 220f, statsBottom));

            SetAccent(LibraryTheme.Colors.DefaultAccent, JacketPalette.Default.Glow);
            SetSummary(HomeLibrarySummary.Empty);
        }

        internal RectTransform Root => _frame.Root;
        internal CanvasGroup Group => _frame.Group;

        internal void SetSummary(HomeLibrarySummary summary)
        {
            _summary = summary ?? HomeLibrarySummary.Empty;
            ApplyCount(_counted);
            if (_summary.PackageCount == 0)
            {
                _description.text = "还没有曲包，进去导入第一首吧";
            }
            else
            {
                _description.text = "导入曲包 · 试听预览 · 开始演奏";
            }

            if (string.IsNullOrEmpty(_summary.RecentTitle))
            {
                _recentCaption.text = _summary.PackageCount == 0 ? "支持 ZIP / OSKP 曲包与文件夹" : "还没有游玩记录";
                _recentTitle.text = _summary.PackageCount == 0 ? "从右上角的“导入”开始" : "挑一首，开始第一次演奏";
                _recentTitle.color = LibraryTheme.Colors.TextSecondary;
            }
            else
            {
                _recentCaption.text = "最近游玩 · " + _summary.RecentWhen;
                _recentTitle.text = _summary.RecentTitle;
                _recentTitle.color = LibraryTheme.Colors.TextPrimary;
            }
        }

        /// <summary>Jackets for the marquee; the first one is the last song played.</summary>
        internal void SetJacket(int slot, Sprite sprite)
        {
            if (slot == 0)
            {
                _recentJacket.sprite = sprite;
                _recentJacket.enabled = sprite != null && !string.IsNullOrEmpty(_summary.RecentTitle);
            }
        }

        /// <summary>Fills the marquee from the loaded jackets, spreading them so neighbors differ.</summary>
        internal void SetMarquee(Sprite[] sprites)
        {
            int count = sprites?.Length ?? 0;
            for (int i = 0; i < _jackets.Length; i++)
            {
                // Down a column the jackets step by one; each column starts two further on.
                Sprite sprite = count > 0 ? sprites[(i % ROWS + i / ROWS * 2) % count] : null;
                _jackets[i].sprite = sprite;
                _jackets[i].enabled = sprite != null;
                _placeholders[i].SetActive(sprite == null);
            }
        }

        internal void SetAccent(Color32 accent, Color32 glow)
        {
            _eyebrow.color = accent;
            _cta.color = accent;
            _ctaGlow.color = HomeTheme.Colors.WithAlpha(accent, 46);
            _glow.color = new Color32(glow.r, glow.g, glow.b, 120);
        }

        /// <summary>The numbers count up from zero as the card comes in; 1 shows the real values.</summary>
        internal void ApplyCount(float progress)
        {
            _counted = progress;
            int packages = Mathf.RoundToInt(_summary.PackageCount * progress);
            int plays = Mathf.RoundToInt(_summary.PlayCount * progress);
            // Only when a number moves: no new strings on the frames it holds still.
            if (packages != _shownPackages)
            {
                _shownPackages = packages;
                _packageCount.text = packages.ToString();
            }

            if (plays != _shownPlays)
            {
                _shownPlays = plays;
                _playCount.text = plays.ToString();
            }
        }

        /// <summary>Copy and marquee opacity, for the page's entrance and exit.</summary>
        internal void SetInnerAlpha(float copy, float art)
        {
            _copy.alpha = copy;
            _art.alpha = art;
        }

        /// <summary>0 leaves the card in its slot's colors; 1 is the page ground it grows into.</summary>
        internal void SetCovering(float amount)
        {
            Color card = HomeTheme.Colors.Card;
            Color ground = LibraryTheme.Colors.Ground;
            Color fill = Color.Lerp(card, ground, amount);
            _frame.Surface.color = fill;
            _cover.color = fill;
            _coverFade.color = fill;
            Color shadow = _frame.Shadow.color;
            shadow.a = (1f - amount) * (150f / 255f);
            _frame.Shadow.color = shadow;
            Color outline = LibraryTheme.Colors.Hairline;
            outline.a *= 1f - amount;
            _frame.Outline.color = outline;
        }

        internal bool Interactable
        {
            set => _frame.Button.interactable = value;
        }

        internal void Tick(double time)
        {
            float pitch = HomeTheme.Layout.MARQUEE_TILE + HomeTheme.Layout.MARQUEE_GAP;
            float span = pitch * ROWS;
            for (int column = 0; column < COLUMNS; column++)
            {
                float direction = column % 2 == 0 ? 1f : -1f;
                float speed = HomeTheme.Motion.MARQUEE_SPEED * (column == 1 ? 0.8f : 1f);
                float offset = (float)((time * speed * direction + column * pitch * 0.5f) % span);
                RectTransform strip = _columns[column];
                for (int row = 0; row < ROWS; row++)
                {
                    float y = row * pitch + offset;
                    // Wrap into [-span/2, span/2) so every tile stays inside its strip.
                    y = Mathf.Repeat(y + span * 0.5f, span) - span * 0.5f;
                    ((RectTransform)strip.GetChild(row)).anchoredPosition = new Vector2(0f, y);
                }
            }
        }

        private static TextMeshProUGUI Stat(Transform parent, string name, string caption, Vector2 position)
        {
            RectTransform root = LibraryUi.CreateRect(name, parent);
            LibraryWidgets.Place(root, Vector2.zero, Vector2.zero, position, new Vector2(200f, 96f));
            TextMeshProUGUI value = LibraryWidgets.Label("Value", root, "0", 66f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(value);
            // The condensed face's line is taller than its digits; an ellipsising label would drop
            // the number entirely rather than let it touch the box's edge.
            value.overflowMode = TextOverflowModes.Overflow;
            value.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.TopStrip(value.rectTransform, 0f, 0f, -14f, 84f);
            TextMeshProUGUI label = LibraryWidgets.Label("Caption", root, caption, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.BottomStrip(label.rectTransform, 2f, 0f, 0f, 24f);
            return value;
        }
    }

    /// <summary>What the library card shows about the library.</summary>
    internal sealed class HomeLibrarySummary
    {
        internal static readonly HomeLibrarySummary Empty = new HomeLibrarySummary(0, 0, null, null);

        internal HomeLibrarySummary(int packageCount, int playCount, string recentTitle, string recentWhen)
        {
            PackageCount = packageCount;
            PlayCount = playCount;
            RecentTitle = recentTitle;
            RecentWhen = recentWhen;
        }

        internal int PackageCount { get; }
        internal int PlayCount { get; }

        /// <summary>Null when nothing has been played.</summary>
        internal string RecentTitle { get; }

        /// <summary>"3 小时前" and the like.</summary>
        internal string RecentWhen { get; }
    }

    /// <summary>
    /// A card for a part of the app that is not built yet (服务器, 资源包): a title block, a
    /// "即将推出" chip with a breathing status light, and an animated drawing on the right. A tap
    /// shakes it and lets the page explain.
    /// </summary>
    internal sealed class HomeFeatureCard
    {
        internal const float ART_SIZE = 280f;

        private readonly HomeCardFrame _frame;
        private readonly Image _statusDot;
        private readonly IHomeCardArt _art;
        private MotionHandle _shake;

        internal HomeFeatureCard(
            Transform parent, string name, string eyebrow, string title, string description, Color32 tint,
            Func<RectTransform, IHomeCardArt> createArt, Action onClick, bool available = false)
        {
            _frame = new HomeCardFrame(parent, name, HomeTheme.Layout.CARD_RADIUS, null, 0.975f);
            _frame.Button.onClick.AddListener(() =>
            {
                if (!available) Shake();
                _art?.Poke();
                onClick?.Invoke();
            });

            RectTransform content = _frame.Content;
            Image glow = LibraryUi.CreateImage("Glow", content, HomeTheme.Colors.WithAlpha(tint, 40));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-40f - ART_SIZE * 0.5f, 0f), new Vector2(ART_SIZE * 2.3f, ART_SIZE * 2.3f));

            RectTransform artRoot = LibraryUi.CreateRect("Art", content);
            LibraryWidgets.Place(artRoot, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-40f - ART_SIZE * 0.5f, 0f), new Vector2(ART_SIZE, ART_SIZE));
            // Animated every frame: a canvas of its own keeps the re-batching local.
            artRoot.gameObject.AddComponent<Canvas>();
            _art = createArt?.Invoke(artRoot);

            float pad = HomeTheme.Layout.CARD_PADDING;
            Vector2 topLeft = new Vector2(0f, 1f);
            TextMeshProUGUI eyebrowLabel = LibraryWidgets.Label("Eyebrow", content, eyebrow, 22f, tint);
            LibraryUi.UseCondensedFont(eyebrowLabel);
            eyebrowLabel.characterSpacing = 28f;
            Stretch(eyebrowLabel.rectTransform, pad, pad - 2f, 28f);

            TextMeshProUGUI titleLabel = LibraryWidgets.Label(
                "Title", content, title, 54f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            Stretch(titleLabel.rectTransform, pad - 2f, pad + 30f, 72f);

            TextMeshProUGUI descriptionLabel = LibraryWidgets.Label(
                "Description", content, description, 20f, LibraryTheme.Colors.TextSecondary);
            Stretch(descriptionLabel.rectTransform, pad, pad + 108f, 30f);

            // 即将推出 chip with a breathing light.
            const float chipHeight = 46f;
            const float chipWidth = 158f;
            Image chip = LibraryWidgets.Panel("Soon", content, chipHeight * 0.5f, HomeTheme.Colors.Chip);
            LibraryWidgets.Outline(chip, chipHeight * 0.5f);
            LibraryWidgets.Place(chip.rectTransform, Vector2.zero, Vector2.zero, new Vector2(pad, pad - 4f), new Vector2(chipWidth, chipHeight));
            _statusDot = LibraryUi.CreateImage("Dot", chip.transform, tint);
            _statusDot.sprite = LibraryShapes.RoundedRect(5f);
            _statusDot.type = Image.Type.Sliced;
            _statusDot.raycastTarget = false;
            LibraryWidgets.Place(_statusDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(10f, 10f));
            TextMeshProUGUI chipLabel = LibraryWidgets.Label(
                "Label", chip.transform, available ? "进入社区" : "即将推出", 19f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal, TextAlignmentOptions.Left);
            LibraryWidgets.Inset(chipLabel.rectTransform, 44f, 0f, 12f, 0f);

            void Stretch(RectTransform rect, float left, float top, float height)
            {
                // Copy runs from the padding to just short of the drawing on the right.
                rect.anchorMin = topLeft;
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = topLeft;
                rect.offsetMin = new Vector2(left, -top - height);
                rect.offsetMax = new Vector2(-(ART_SIZE + 64f), -top);
            }
        }

        internal RectTransform Root => _frame.Root;
        internal CanvasGroup Group => _frame.Group;

        internal bool Interactable
        {
            set => _frame.Button.interactable = value;
        }

        internal void Tick(double time)
        {
            float phase = (float)(time % 2.4d / 2.4d) * Mathf.PI * 2f;
            Color dot = _statusDot.color;
            dot.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(phase));
            _statusDot.color = dot;
            _art?.Tick(time);
        }

        internal void Shake()
        {
            _shake.TryCancel();
            RectTransform body = _frame.Body;
            _shake = LMotion.Shake.Create(0f, 14f, 0.42f)
                .WithFrequency(9)
                .WithDampingRatio(0.75f)
                .WithRandomSeed(1117)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => body.anchoredPosition = Vector2.zero)
                .WithOnCancel(() => body.anchoredPosition = Vector2.zero)
                .Bind(x => body.anchoredPosition = new Vector2(x, 0f));
        }

        internal void Dispose()
        {
            _shake.TryCancel();
            _art?.Dispose();
        }
    }

    /// <summary>The animated drawing on a feature card.</summary>
    internal interface IHomeCardArt : IDisposable
    {
        void Tick(double time);

        /// <summary>Reacts to a tap on the card.</summary>
        void Poke();
    }

    /// <summary>
    /// 服务器: a wireframe globe turning slowly (its meridians are rings squeezed by the cosine of
    /// their angle), points of light riding its surface, a satellite on a tilted orbit and a
    /// signal pulse going out every few seconds.
    /// </summary>
    internal sealed class HomeGlobeArt : IHomeCardArt
    {
        private const int MERIDIANS = 3;
        private const float TURN_SECONDS = 14f;
        private const float PULSE_SECONDS = 3f;
        private static readonly Vector2[] NodeCoordinates =
        {
            // longitude, latitude in radians
            new Vector2(0.3f, 0.5f), new Vector2(1.4f, -0.2f), new Vector2(2.5f, 0.75f),
            new Vector2(3.4f, 0.1f), new Vector2(4.4f, -0.6f), new Vector2(5.4f, 0.35f)
        };

        private readonly RectTransform _globe;
        private readonly Image[] _meridians = new Image[MERIDIANS];
        private readonly Image[] _nodes = new Image[NodeCoordinates.Length];
        private readonly Image[] _pulses = new Image[2];
        private readonly RectTransform _orbit;
        private readonly Image _satellite;
        private readonly Image _satelliteGlow;
        private float _poke;
        private MotionHandle _pokeMotion;
        private readonly float _radius;

        internal HomeGlobeArt(RectTransform root)
        {
            float size = HomeFeatureCard.ART_SIZE;
            _radius = size * 0.36f;
            Color32 line = HomeTheme.Colors.Periwinkle;
            Sprite ring = LibraryShapes.Ring(150f, 2f);

            _globe = LibraryUi.CreateRect("Globe", root);
            LibraryUi.Stretch(_globe);
            _globe.localEulerAngles = new Vector3(0f, 0f, -16f);

            for (int i = 0; i < _pulses.Length; i++)
            {
                _pulses[i] = Ring(_globe, "Pulse" + i, ring, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Violet, 0), _radius * 2f);
            }

            Image fill = LibraryUi.CreateImage("Fill", _globe, HomeTheme.Colors.WithAlpha(line, 22));
            fill.sprite = LibraryShapes.RoundedRect(_radius);
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = false;
            LibraryWidgets.Place(fill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(_radius * 2f, _radius * 2f));

            Ring(_globe, "Outline", ring, HomeTheme.Colors.WithAlpha(line, 150), _radius * 2f);
            // Equator and two parallels, squashed by the globe's tilt toward the viewer.
            Image equator = Ring(_globe, "Equator", ring, HomeTheme.Colors.WithAlpha(line, 70), _radius * 2f);
            equator.rectTransform.localScale = new Vector3(1f, 0.3f, 1f);
            for (int i = 0; i < 2; i++)
            {
                float latitude = (i == 0 ? 1f : -1f) * 0.62f;
                Image parallel = Ring(_globe, "Parallel" + i, ring, HomeTheme.Colors.WithAlpha(line, 50), _radius * 2f);
                parallel.rectTransform.localScale = new Vector3(Mathf.Cos(latitude), Mathf.Cos(latitude) * 0.3f, 1f);
                parallel.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(latitude) * _radius);
            }

            for (int i = 0; i < MERIDIANS; i++)
            {
                _meridians[i] = Ring(_globe, "Meridian" + i, ring, HomeTheme.Colors.WithAlpha(line, 70), _radius * 2f);
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                Image node = LibraryUi.CreateImage("Node" + i, _globe, i % 2 == 0 ? Color.white : (Color)HomeTheme.Colors.Rose);
                node.sprite = LibraryShapes.Glow();
                node.raycastTarget = false;
                LibraryWidgets.Place(node.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
                _nodes[i] = node;
            }

            _orbit = LibraryUi.CreateRect("Orbit", root);
            LibraryUi.Stretch(_orbit);
            _orbit.localEulerAngles = new Vector3(0f, 0f, 20f);
            Image orbitLine = Ring(_orbit, "Path", ring, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Violet, 110), size * 0.94f);
            orbitLine.rectTransform.localScale = new Vector3(1f, 0.28f, 1f);
            _satelliteGlow = LibraryUi.CreateImage("SatelliteGlow", _orbit, HomeTheme.Colors.Rose);
            _satelliteGlow.sprite = LibraryShapes.Glow();
            _satelliteGlow.raycastTarget = false;
            LibraryWidgets.Place(_satelliteGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            _satellite = LibraryUi.CreateImage("Satellite", _orbit, Color.white);
            _satellite.sprite = LibraryShapes.RoundedRect(6f);
            _satellite.type = Image.Type.Sliced;
            _satellite.raycastTarget = false;
            LibraryWidgets.Place(_satellite.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));

            Tick(0d);
        }

        public void Tick(double time)
        {
            float turn = (float)(time % TURN_SECONDS / TURN_SECONDS) * Mathf.PI * 2f * (1f + _poke * 0.6f);
            for (int i = 0; i < MERIDIANS; i++)
            {
                float angle = turn + i * Mathf.PI / MERIDIANS;
                _meridians[i].rectTransform.localScale = new Vector3(Mathf.Cos(angle), 1f, 1f);
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                Vector2 coordinate = NodeCoordinates[i];
                float longitude = coordinate.x + turn;
                float depth = Mathf.Cos(coordinate.y) * Mathf.Cos(longitude);
                float x = Mathf.Cos(coordinate.y) * Mathf.Sin(longitude) * _radius;
                // The globe leans toward the viewer: the far side rides a little higher.
                float y = Mathf.Sin(coordinate.y) * _radius * 0.95f + depth * _radius * 0.12f;
                RectTransform rect = _nodes[i].rectTransform;
                rect.anchoredPosition = new Vector2(x, y);
                float near = Mathf.Clamp01(depth * 0.5f + 0.5f);
                float scale = 0.55f + 0.6f * near;
                rect.localScale = new Vector3(scale, scale, 1f);
                Color color = _nodes[i].color;
                color.a = 0.15f + 0.85f * near * near;
                _nodes[i].color = color;
            }

            float orbitAngle = (float)(time % 7d / 7d) * Mathf.PI * 2f;
            float half = HomeFeatureCard.ART_SIZE * 0.47f;
            Vector2 position = new Vector2(Mathf.Cos(orbitAngle) * half, Mathf.Sin(orbitAngle) * half * 0.28f);
            _satellite.rectTransform.anchoredPosition = position;
            _satelliteGlow.rectTransform.anchoredPosition = position;
            // Behind the globe on the top half of the orbit.
            float front = Mathf.Sin(orbitAngle) < 0f ? 1f : 0.35f;
            _satellite.color = new Color(1f, 1f, 1f, front);
            Color glow = _satelliteGlow.color;
            glow.a = 0.6f * front;
            _satelliteGlow.color = glow;

            for (int i = 0; i < _pulses.Length; i++)
            {
                float p = (float)((time + i * PULSE_SECONDS * 0.5f) % PULSE_SECONDS / PULSE_SECONDS);
                float eased = 1f - (1f - p) * (1f - p);
                float scale = 1f + eased * 0.9f;
                _pulses[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                Color color = _pulses[i].color;
                color.a = (1f - p) * 0.4f;
                _pulses[i].color = color;
            }
        }

        public void Poke()
        {
            _pokeMotion.TryCancel();
            _pokeMotion = LMotion.Create(1f, 0f, 1.2f)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(value => _poke = value);
        }

        public void Dispose()
        {
            _pokeMotion.TryCancel();
        }

        private static Image Ring(Transform parent, string name, Sprite sprite, Color32 color, float diameter)
        {
            Image image = LibraryUi.CreateImage(name, parent, color);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            LibraryWidgets.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(diameter, diameter));
            return image;
        }
    }

    /// <summary>
    /// 资源包: three cards fanned like a hand, each a little picture of what a pack can change
    /// (a note skin, a sound set, a color theme). The fan breathes, and opens wider when tapped.
    /// </summary>
    internal sealed class HomeStackArt : IHomeCardArt
    {
        private const float CARD_WIDTH = 140f;
        private const float CARD_HEIGHT = 184f;

        private readonly RectTransform[] _cards = new RectTransform[3];
        private readonly float[] _angles = { 15f, 3f, -10f };
        private readonly Vector2[] _offsets = { new Vector2(58f, 16f), new Vector2(14f, 4f), new Vector2(-36f, -14f) };
        private float _poke;
        private MotionHandle _pokeMotion;

        internal HomeStackArt(RectTransform root)
        {
            Color32[] tints = { HomeTheme.Colors.Rose, HomeTheme.Colors.Violet, HomeTheme.Colors.Periwinkle };
            for (int i = 0; i < _cards.Length; i++)
            {
                RectTransform card = LibraryUi.CreateRect("Card" + i, root);
                LibraryWidgets.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CARD_WIDTH, CARD_HEIGHT));
                LibraryWidgets.Shadow(card, 20f, 28f, 10f, 120);
                Color tint = tints[i];
                Image face = LibraryWidgets.Panel("Face", card, 20f, Color.Lerp(HomeTheme.Colors.Card, tint, 0.2f));
                LibraryUi.Stretch(face.rectTransform);
                Image wash = LibraryWidgets.Shape("Wash", face.transform, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(tints[i], 46));
                wash.type = Image.Type.Simple;
                LibraryUi.Stretch(wash.rectTransform);
                Image rim = LibraryWidgets.Shape("Rim", card, LibraryShapes.Ring(20f, 1.5f), HomeTheme.Colors.WithAlpha(tints[i], 120));
                LibraryUi.Stretch(rim.rectTransform);
                _cards[i] = card;
            }

            DrawThemeCard(_cards[0]);
            DrawSoundCard(_cards[1]);
            DrawSkinCard(_cards[2]);
            Tick(0d);
        }

        public void Tick(double time)
        {
            float phase = (float)(time % 6d / 6d) * Mathf.PI * 2f;
            float spread = 1f + 0.07f * Mathf.Sin(phase) + _poke * 0.45f;
            for (int i = 0; i < _cards.Length; i++)
            {
                float bob = Mathf.Sin(phase + i * 1.3f) * 4f;
                _cards[i].anchoredPosition = _offsets[i] * spread + new Vector2(0f, bob);
                _cards[i].localEulerAngles = new Vector3(0f, 0f, _angles[i] * spread);
            }
        }

        public void Poke()
        {
            _pokeMotion.TryCancel();
            _pokeMotion = LSequence.Create()
                .Append(LMotion.Create(_poke, 1f, 0.16f).WithEase(Ease.OutCubic).WithScheduler(HomeMotion.Scheduler).Bind(v => _poke = v))
                .Append(LMotion.Create(1f, 0f, 0.7f).WithEase(Ease.OutBack).WithScheduler(HomeMotion.Scheduler).Bind(v => _poke = v))
                .Run();
        }

        public void Dispose()
        {
            _pokeMotion.TryCancel();
        }

        /// <summary>Back card: three swatches and two lines of "text".</summary>
        private static void DrawThemeCard(RectTransform card)
        {
            Color32[] swatches = { HomeTheme.Colors.Periwinkle, HomeTheme.Colors.Violet, HomeTheme.Colors.Rose };
            for (int i = 0; i < swatches.Length; i++)
            {
                Image swatch = LibraryWidgets.Panel("Swatch" + i, card, 13f, swatches[i]);
                LibraryWidgets.Place(swatch.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f + i * 34f, -22f), new Vector2(26f, 26f));
            }

            Lines(card, 0.5f);
        }

        /// <summary>Middle card: a sound wave.</summary>
        private static void DrawSoundCard(RectTransform card)
        {
            Image wave = LibraryWidgets.Icon(card, LibraryIcon.Wave, Color.white, 54f);
            wave.rectTransform.anchoredPosition = new Vector2(0f, 26f);
            Lines(card, 0.5f);
        }

        /// <summary>Front card: a tiny note highway with a note about to land.</summary>
        private static void DrawSkinCard(RectTransform card)
        {
            RectTransform lane = LibraryUi.CreateRect("Lane", card);
            LibraryWidgets.Inset(lane, 18f, 18f, 18f, 18f);
            for (int i = 0; i < 2; i++)
            {
                Image rail = LibraryUi.CreateImage("Rail" + i, lane, HomeTheme.Colors.WithAlpha(Color.white, 120));
                rail.sprite = HomeShapes.FadeDown();
                rail.raycastTarget = false;
                RectTransform rect = rail.rectTransform;
                LibraryWidgets.Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(i == 0 ? -10f : 10f, 0f), new Vector2(3f, 160f));
                rect.localEulerAngles = new Vector3(0f, 0f, i == 0 ? -12f : 12f);
            }

            Image judge = LibraryWidgets.Panel("Judge", lane, 3f, Color.white);
            LibraryWidgets.Place(judge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(92f, 6f));
            Image note = LibraryWidgets.Panel("Note", lane, 8f, HomeTheme.Colors.Rose);
            LibraryWidgets.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(56f, 16f));
            Image glow = LibraryUi.CreateImage("Glow", lane, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Rose, 90));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(120f, 70f));
            glow.transform.SetSiblingIndex(note.transform.GetSiblingIndex());
        }

        private static void Lines(RectTransform card, float alpha)
        {
            for (int i = 0; i < 2; i++)
            {
                Image line = LibraryWidgets.Panel("Line" + i, card, 4f, HomeTheme.Colors.WithAlpha(Color.white, (byte)(alpha * 120)));
                LibraryWidgets.Place(line.rectTransform, Vector2.zero, Vector2.zero, new Vector2(20f, 46f - i * 22f),
                    new Vector2(i == 0 ? 92f : 62f, 8f));
            }
        }
    }
}
