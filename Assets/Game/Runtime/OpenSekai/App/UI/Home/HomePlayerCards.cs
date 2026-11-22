using System;
using System.Globalization;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>What the 个人信息 card shows about the player.</summary>
    internal sealed class HomeProfileSummary
    {
        internal static readonly HomeProfileSummary Empty =
            new HomeProfileSummary("Player", ProfileAvatars.DEFAULT_ID, 0L, 0, 0, 0, null, false);

        /// <param name="danColor">The worn plate's "#RRGGBB"; ignored when no plate is worn.</param>
        internal HomeProfileSummary(string nickname, string avatarId, long total, int tierId, int chartCount, int playCount,
            string danLabel, bool unreadable, string danColor = null)
        {
            DanColor = danColor;
            Nickname = nickname;
            AvatarId = avatarId;
            Total = total;
            TierId = tierId;
            ChartCount = chartCount;
            PlayCount = playCount;
            DanLabel = danLabel;
            Unreadable = unreadable;
        }

        internal string Nickname { get; }
        internal string AvatarId { get; }
        internal long Total { get; }
        internal int TierId { get; }

        /// <summary>Charts in the Best 30.</summary>
        internal int ChartCount { get; }

        internal int PlayCount { get; }

        /// <summary>The equipped 段位 plate's text; null when none is worn.</summary>
        internal string DanLabel { get; }

        /// <summary>The equipped plate's color as "#RRGGBB"; null when none is worn.</summary>
        internal string DanColor { get; }

        /// <summary>The profile could not be read; the card says so instead of showing zeros as fact.</summary>
        internal bool Unreadable { get; }
    }

    /// <summary>
    /// The second page's big card, the way into 个人信息: the player's avatar and nickname, the
    /// worn 段位 plate, the Ranking Score with its tier, and how many charts and plays stand behind
    /// it. On the right the tier badge floats in its own color with two orbits around it. Like the
    /// library card it grows over the page when tapped.
    /// </summary>
    internal sealed class HomeProfileCard : IHomeCard
    {
        private const float TEXT_WIDTH = 560f;
        private const float FADE_WIDTH = 240f;
        private const float CTA_WIDTH = 252f;
        private const float CTA_HEIGHT = 80f;
        private const float AVATAR_SIZE = 104f;
        private const float BADGE_SIZE = 260f;
        private const float PLATE_HEIGHT = 36f;
        private const int STARS = 9;

        private static readonly Vector2[] _starPositions =
        {
            new Vector2(-250f, 210f), new Vector2(-120f, 290f), new Vector2(170f, 250f), new Vector2(280f, 120f),
            new Vector2(300f, -150f), new Vector2(150f, -280f), new Vector2(-200f, -240f), new Vector2(-300f, -40f),
            new Vector2(40f, 330f)
        };

        private readonly HomeCardFrame _frame;
        private readonly Image _glow;
        private readonly CanvasGroup _art;
        private readonly CanvasGroup _badgeLayer;
        private readonly RectTransform _badgeAnchor;
        private readonly RankingBadge _badge;
        private readonly Image _badgeGlow;
        private readonly RectTransform[] _orbits = new RectTransform[2];
        private readonly Image[] _orbitPaths = new Image[2];
        private readonly Image[] _satellites = new Image[2];
        private readonly Image[] _stars = new Image[STARS];
        private readonly Image _cover;
        private readonly Image _coverFade;
        private readonly CanvasGroup _copy;
        private readonly TextMeshProUGUI _eyebrow;
        private readonly ProfileAvatarView _avatar;
        private readonly TextMeshProUGUI _nickname;
        private readonly Image _plate;
        private readonly Image _plateOutline;
        private readonly TextMeshProUGUI _plateLabel;
        private readonly TextMeshProUGUI _score;
        private readonly TextMeshProUGUI _tier;
        private readonly TextMeshProUGUI _charts;
        private readonly TextMeshProUGUI _plays;
        private readonly Image _cta;
        private readonly Image _ctaGlow;
        private HomeProfileSummary _summary;

        internal HomeProfileCard(Transform parent, Action onClick)
        {
            _frame = new HomeCardFrame(parent, "ProfileCard", HomeTheme.Layout.HERO_RADIUS, onClick, 0.985f);
            RectTransform content = _frame.Content;

            // Art area: the part of the card right of the copy.
            RectTransform artArea = LibraryUi.CreateRect("ArtArea", content);
            LibraryWidgets.Inset(artArea, TEXT_WIDTH - 40f, 0f, 0f, 0f);

            RectTransform artRoot = LibraryUi.CreateRect("Art", artArea);
            LibraryUi.Stretch(artRoot);
            // Moves every frame: its own canvas keeps the rest of the page out of the re-batch.
            artRoot.gameObject.AddComponent<Canvas>();
            _art = artRoot.gameObject.AddComponent<CanvasGroup>();
            _art.blocksRaycasts = false;
            // In the art's group, so it fades with the rest of the drawing as the card grows over the page.
            _glow = LibraryUi.CreateImage("Glow", artRoot, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            LibraryWidgets.Place(_glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1100f, 1100f));
            for (int i = 0; i < _stars.Length; i++)
            {
                Image star = LibraryUi.CreateImage("Star" + i, artRoot, Color.white);
                star.sprite = LibraryShapes.Glow();
                star.raycastTarget = false;
                float size = i % 3 == 0 ? 26f : 16f;
                LibraryWidgets.Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _starPositions[i],
                    new Vector2(size, size));
                _stars[i] = star;
            }

            for (int i = 0; i < _orbits.Length; i++)
            {
                float diameter = i == 0 ? 470f : 660f;
                RectTransform orbit = LibraryUi.CreateRect("Orbit" + i, artRoot);
                LibraryWidgets.Place(orbit, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, diameter));
                orbit.localEulerAngles = new Vector3(0f, 0f, i == 0 ? -14f : 10f);
                RectTransform squash = LibraryUi.CreateRect("Squash", orbit);
                LibraryUi.Stretch(squash);
                squash.localScale = new Vector3(1f, i == 0 ? 0.34f : 0.26f, 1f);
                _orbitPaths[i] = LibraryWidgets.Shape("Path", squash, LibraryShapes.Ring(150f, 2f), Color.white);
                _orbitPaths[i].type = Image.Type.Simple;
                LibraryUi.Stretch(_orbitPaths[i].rectTransform);
                Image satellite = LibraryUi.CreateImage("Satellite", orbit, Color.white);
                satellite.sprite = LibraryShapes.Glow();
                satellite.raycastTarget = false;
                LibraryWidgets.Place(satellite.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(30f, 30f));
                _orbits[i] = orbit;
                _satellites[i] = satellite;
            }

            // The copy sits on a solid band that fades out toward the art, as on the library card.
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
            _coverFade.rectTransform.offsetMax = new Vector2(TEXT_WIDTH + FADE_WIDTH, 0f);

            // The badge draws over the fade, so the band never dims it on a narrow screen.
            RectTransform badgeArea = LibraryUi.CreateRect("BadgeLayer", content);
            LibraryWidgets.Inset(badgeArea, TEXT_WIDTH - 40f, 0f, 0f, 0f);
            badgeArea.gameObject.AddComponent<Canvas>();
            _badgeLayer = badgeArea.gameObject.AddComponent<CanvasGroup>();
            _badgeLayer.blocksRaycasts = false;
            _badgeAnchor = LibraryUi.CreateRect("Badge", badgeArea);
            LibraryWidgets.Place(_badgeAnchor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(BADGE_SIZE, BADGE_SIZE));
            _badgeGlow = LibraryUi.CreateImage("Halo", _badgeAnchor, Color.clear);
            _badgeGlow.sprite = LibraryShapes.Glow();
            _badgeGlow.raycastTarget = false;
            LibraryWidgets.Place(_badgeGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(BADGE_SIZE * 2.2f, BADGE_SIZE * 2.2f));
            _badge = new RankingBadge(_badgeAnchor, BADGE_SIZE);
            Vector2 center = new Vector2(0.5f, 0.5f);
            _badge.Root.anchorMin = center;
            _badge.Root.anchorMax = center;
            _badge.Root.anchoredPosition = Vector2.zero;

            RectTransform copyRoot = LibraryUi.CreateRect("Copy", content);
            LibraryUi.Stretch(copyRoot);
            _copy = copyRoot.gameObject.AddComponent<CanvasGroup>();
            _copy.blocksRaycasts = false;
            float pad = HomeTheme.Layout.HERO_PADDING;
            Vector2 topLeft = new Vector2(0f, 1f);
            Vector2 bottomLeft = Vector2.zero;

            _eyebrow = LibraryWidgets.Label("Eyebrow", copyRoot, "PLAYER", 24f, LibraryTheme.Colors.DefaultAccent);
            LibraryUi.UseSpacedCondensedFont(_eyebrow, 30f);
            LibraryWidgets.Place(_eyebrow.rectTransform, topLeft, topLeft, new Vector2(pad, -pad + 4f), new Vector2(TEXT_WIDTH - pad, 30f));

            // Who: avatar, nickname and the worn plate.
            float identityTop = pad + 44f;
            _avatar = new ProfileAvatarView(copyRoot, "Avatar", AVATAR_SIZE);
            LibraryWidgets.Place(_avatar.Root, topLeft, topLeft, new Vector2(pad, -identityTop), new Vector2(AVATAR_SIZE, AVATAR_SIZE));
            float nameLeft = pad + AVATAR_SIZE + 26f;
            _nickname = LibraryWidgets.Label("Nickname", copyRoot, string.Empty, 50f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _nickname.richText = false;
            LibraryWidgets.Place(_nickname.rectTransform, topLeft, topLeft, new Vector2(nameLeft, -identityTop + 4f),
                new Vector2(TEXT_WIDTH - nameLeft - 12f, 62f));
            _plate = LibraryWidgets.Panel("Plate", copyRoot, 8f, HomeTheme.Colors.Chip);
            LibraryWidgets.Place(_plate.rectTransform, topLeft, topLeft, new Vector2(nameLeft, -identityTop - 64f),
                new Vector2(180f, PLATE_HEIGHT));
            _plateOutline = LibraryWidgets.Shape("Outline", _plate.transform, LibraryShapes.Ring(8f, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(_plateOutline.rectTransform);
            _plateLabel = LibraryWidgets.Label("Label", _plate.transform, string.Empty, 18f, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Center);
            _plateLabel.richText = false;
            _plateLabel.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(_plateLabel.rectTransform);

            // Call to action: drawn only, the whole card takes the tap.
            _ctaGlow = LibraryUi.CreateImage("CtaGlow", copyRoot, Color.clear);
            _ctaGlow.sprite = LibraryShapes.Glow();
            _ctaGlow.raycastTarget = false;
            LibraryWidgets.Place(_ctaGlow.rectTransform, bottomLeft, new Vector2(0.5f, 0.5f),
                new Vector2(pad + CTA_WIDTH * 0.5f, pad + CTA_HEIGHT * 0.5f - 6f), new Vector2(CTA_WIDTH * 1.9f, CTA_HEIGHT * 2.6f));
            _cta = LibraryWidgets.Panel("Cta", copyRoot, CTA_HEIGHT * 0.5f, LibraryTheme.Colors.DefaultAccent);
            LibraryWidgets.Place(_cta.rectTransform, bottomLeft, bottomLeft, new Vector2(pad, pad), new Vector2(CTA_WIDTH, CTA_HEIGHT));
            TextMeshProUGUI ctaLabel = LibraryWidgets.Label(
                "Label", _cta.transform, "个人信息", 26f, LibraryTheme.Colors.TextOnAccent, FontStyles.Bold, TextAlignmentOptions.Left);
            LibraryWidgets.Inset(ctaLabel.rectTransform, 44f, 0f, 60f, 0f);
            Image arrow = LibraryWidgets.Icon(_cta.transform, LibraryIcon.Forward, LibraryTheme.Colors.TextOnAccent, 28f);
            LibraryWidgets.Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-38f, 0f), new Vector2(28f, 28f));

            // Charts and plays behind the score.
            float statsBottom = pad + CTA_HEIGHT + 38f;
            _charts = Stat(copyRoot, "Charts", "Best 30 谱面", new Vector2(pad, statsBottom));
            _plays = Stat(copyRoot, "Plays", "次游玩", new Vector2(pad + 230f, statsBottom));

            // The Ranking Score, from the bottom up: tier, number, caption.
            float rankBottom = statsBottom + 96f + 30f;
            _tier = LibraryWidgets.Label("Tier", copyRoot, string.Empty, 26f, LibraryTheme.Colors.DefaultAccent, FontStyles.Bold);
            LibraryWidgets.Place(_tier.rectTransform, bottomLeft, bottomLeft, new Vector2(pad, rankBottom), new Vector2(TEXT_WIDTH - pad, 36f));
            _score = LibraryWidgets.Label("Score", copyRoot, "0", 112f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(_score);
            _score.overflowMode = TextOverflowModes.Overflow;
            _score.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.Place(_score.rectTransform, bottomLeft, bottomLeft, new Vector2(pad - 4f, rankBottom + 26f),
                new Vector2(TEXT_WIDTH - pad, 124f));
            TextMeshProUGUI caption = LibraryWidgets.Label("ScoreCaption", copyRoot, "RANKING SCORE", 22f, LibraryTheme.Colors.TextMuted);
            LibraryUi.UseSpacedCondensedFont(caption, 18f);
            LibraryWidgets.Place(caption.rectTransform, bottomLeft, bottomLeft, new Vector2(pad, rankBottom + 156f),
                new Vector2(TEXT_WIDTH - pad, 28f));

            SetSummary(HomeProfileSummary.Empty);
        }

        public RectTransform Root => _frame.Root;
        public CanvasGroup Group => _frame.Group;
        public GameObject Surface => _frame.Surface.gameObject;

        public bool Interactable
        {
            set => _frame.Button.interactable = value;
        }

        internal void SetSummary(HomeProfileSummary summary)
        {
            _summary = summary ?? HomeProfileSummary.Empty;
            _avatar.SetAvatar(_summary.AvatarId);
            _nickname.text = _summary.Nickname;
            _badge.SetTier(_summary.TierId);
            _score.text = _summary.Unreadable ? "—" : _summary.Total.ToString("N0", CultureInfo.InvariantCulture);
            _tier.text = _summary.Unreadable
                ? "档案暂时无法读取"
                : RankingTiers.Name(_summary.TierId) + "  " + RankingTiers.LatinName(_summary.TierId);
            _charts.text = _summary.ChartCount.ToString(CultureInfo.InvariantCulture);
            _plays.text = _summary.PlayCount.ToString(CultureInfo.InvariantCulture);
            bool worn = !string.IsNullOrEmpty(_summary.DanLabel);
            _plateLabel.text = worn ? _summary.DanLabel : "未取得段位";
            float width = Mathf.Ceil(_plateLabel.GetPreferredValues(_plateLabel.text).x) + 36f;
            _plate.rectTransform.sizeDelta = new Vector2(Mathf.Max(120f, width), PLATE_HEIGHT);
            ApplyAccent(_summary.Unreadable ? LibraryTheme.Colors.TextMuted : RankingTiers.Color(_summary.TierId), worn);
        }

        public void SetInnerAlpha(float alpha)
        {
            _copy.alpha = alpha;
            _art.alpha = alpha;
            _badgeLayer.alpha = alpha;
        }

        /// <summary>0 leaves the card in its slot's colors; 1 is the page ground it grows into.</summary>
        public void SetCovering(float amount)
        {
            Color fill = Color.Lerp(HomeTheme.Colors.Card, LibraryTheme.Colors.Ground, amount);
            _frame.Surface.color = fill;
            _cover.color = fill;
            _coverFade.color = fill;
            Color shadow = _frame.Shadow.color;
            shadow.a = (1f - amount) * (LibraryTheme.CoverShadow.OPACITY / 255f);
            _frame.Shadow.color = shadow;
            Color outline = LibraryTheme.Colors.Hairline;
            outline.a *= 1f - amount;
            _frame.Outline.color = outline;
        }

        public void Tick(double time)
        {
            float bob = Mathf.Sin((float)(time % 6d / 6d) * Mathf.PI * 2f);
            _badgeAnchor.anchoredPosition = new Vector2(0f, 8f * bob);
            Color halo = _badgeGlow.color;
            halo.a = (0.22f + 0.08f * bob);
            _badgeGlow.color = halo;
            for (int i = 0; i < _orbits.Length; i++)
            {
                float period = i == 0 ? 9f : 14f;
                float angle = (float)(time % period / period) * Mathf.PI * 2f * (i == 0 ? 1f : -1f) + i * 2.1f;
                float half = _orbits[i].sizeDelta.x * 0.5f;
                float squash = i == 0 ? 0.34f : 0.26f;
                _satellites[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * half, Mathf.Sin(angle) * half * squash);
                // Behind the badge on the far half of the orbit.
                Color satellite = _satellites[i].color;
                satellite.a = Mathf.Sin(angle) > 0f ? 0.25f : 0.9f;
                _satellites[i].color = satellite;
            }

            for (int i = 0; i < _stars.Length; i++)
            {
                float twinkle = 0.5f + 0.5f * Mathf.Sin((float)(time * (0.6d + i * 0.07d)) + i * 1.7f);
                Color star = _stars[i].color;
                star.a = 0.12f + 0.5f * twinkle * twinkle;
                _stars[i].color = star;
            }
        }

        private void ApplyAccent(Color32 accent, bool worn)
        {
            _eyebrow.color = accent;
            _cta.color = accent;
            _ctaGlow.color = HomeTheme.Colors.WithAlpha(accent, 46);
            _glow.color = HomeTheme.Colors.WithAlpha(accent, 70);
            _badgeGlow.color = HomeTheme.Colors.WithAlpha(accent, 60);
            _tier.color = accent;
            for (int i = 0; i < _orbits.Length; i++)
            {
                _orbitPaths[i].color = HomeTheme.Colors.WithAlpha(accent, (byte)(i == 0 ? 90 : 50));
                _satellites[i].color = Color.Lerp(accent, Color.white, 0.5f);
            }

            Color32 plate = DanStyles.ParseColor(_summary?.DanColor, HomeTheme.Colors.Violet);
            _plate.color = worn ? HomeTheme.Colors.WithAlpha(plate, 56) : HomeTheme.Colors.Chip;
            _plateOutline.color = worn ? HomeTheme.Colors.WithAlpha(DanStyles.Light(plate), 170) : LibraryTheme.Colors.Hairline;
            _plateLabel.color = worn ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextMuted;
        }

        private static TextMeshProUGUI Stat(Transform parent, string name, string caption, Vector2 position)
        {
            RectTransform root = LibraryUi.CreateRect(name, parent);
            LibraryWidgets.Place(root, Vector2.zero, Vector2.zero, position, new Vector2(210f, 96f));
            TextMeshProUGUI value = LibraryWidgets.Label("Value", root, "0", 66f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(value);
            value.overflowMode = TextOverflowModes.Overflow;
            value.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.TopStrip(value.rectTransform, 0f, 0f, -14f, 84f);
            TextMeshProUGUI label = LibraryWidgets.Label("Caption", root, caption, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.BottomStrip(label.rectTransform, 2f, 0f, 0f, 24f);
            return value;
        }
    }

    /// <summary>
    /// 段位: three 段位 plates stacked like steps, each with its emblem and a band of text; a
    /// light sweeps across the front one now and then, and a tap fans them apart. Rectangles on
    /// purpose, unlike the hexagonal Ranking Score badge.
    /// </summary>
    internal sealed class HomePlateArt : IHomeCardArt
    {
        private const float PLATE_WIDTH = 216f;
        private const float PLATE_HEIGHT = 72f;
        private const float SHINE_PERIOD = 4.2f;

        private readonly RectTransform[] _plates = new RectTransform[3];
        private readonly Vector2[] _offsets = { new Vector2(-30f, 78f), new Vector2(0f, 0f), new Vector2(30f, -78f) };
        private readonly RectTransform _shine;
        private float _poke;
        private MotionHandle _pokeMotion;

        internal HomePlateArt(RectTransform root)
        {
            Color32[] tints = { HomeTheme.Colors.Rose, HomeTheme.Colors.Periwinkle, HomeTheme.Colors.Violet };
            string[] marks = { "I", "II", "III" };
            for (int i = 0; i < _plates.Length; i++)
            {
                Color32 tint = tints[i];
                RectTransform plate = LibraryUi.CreateRect("Plate" + i, root);
                LibraryWidgets.Place(plate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _offsets[i],
                    new Vector2(PLATE_WIDTH, PLATE_HEIGHT));
                plate.localEulerAngles = new Vector3(0f, 0f, -6f);
                LibraryWidgets.Shadow(plate, 12f, 24f, 8f, 120);
                Image face = LibraryWidgets.Panel("Face", plate, 12f, Color.Lerp(HomeTheme.Colors.Card, tint, 0.18f));
                LibraryUi.Stretch(face.rectTransform);
                face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                Image band = LibraryWidgets.Panel("Band", face.transform, 1f, HomeTheme.Colors.WithAlpha(tint, 70));
                band.rectTransform.anchorMin = Vector2.zero;
                band.rectTransform.anchorMax = new Vector2(0f, 1f);
                band.rectTransform.pivot = new Vector2(0f, 0.5f);
                band.rectTransform.offsetMin = Vector2.zero;
                band.rectTransform.offsetMax = new Vector2(PLATE_HEIGHT, 0f);
                Image diamond = LibraryWidgets.Panel("Emblem", face.transform, 4f, tint);
                LibraryWidgets.Place(diamond.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(PLATE_HEIGHT * 0.5f, 0f), new Vector2(26f, 26f));
                diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                TextMeshProUGUI mark = LibraryWidgets.Label("Mark", face.transform, marks[i], 30f, LibraryTheme.Colors.TextPrimary,
                    FontStyles.Normal, TextAlignmentOptions.Right);
                LibraryUi.UseCondensedFont(mark);
                mark.overflowMode = TextOverflowModes.Overflow;
                LibraryWidgets.Inset(mark.rectTransform, PLATE_HEIGHT + 10f, 0f, 18f, 0f);
                for (int line = 0; line < 2; line++)
                {
                    Image bar = LibraryWidgets.Panel("Line" + line, face.transform, 3f,
                        HomeTheme.Colors.WithAlpha(Color.white, (byte)(line == 0 ? 150 : 70)));
                    LibraryWidgets.Place(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(PLATE_HEIGHT + 16f, line == 0 ? 9f : -11f), new Vector2(line == 0 ? 74f : 48f, 7f));
                }

                if (i == _plates.Length - 1)
                {
                    Image shine = LibraryUi.CreateImage("Shine", face.transform, HomeTheme.Colors.WithAlpha(Color.white, 70));
                    shine.raycastTarget = false;
                    LibraryWidgets.Place(shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(30f, PLATE_HEIGHT * 2f));
                    shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 24f);
                    _shine = shine.rectTransform;
                }

                Image rim = LibraryWidgets.Shape("Rim", plate, LibraryShapes.Ring(12f, 1.5f), HomeTheme.Colors.WithAlpha(tint, 150));
                LibraryUi.Stretch(rim.rectTransform);
                _plates[i] = plate;
            }

            Tick(0d);
        }

        public void Tick(double time)
        {
            float phase = (float)(time % 5d / 5d) * Mathf.PI * 2f;
            float spread = 1f + _poke * 0.4f;
            for (int i = 0; i < _plates.Length; i++)
            {
                float bob = Mathf.Sin(phase + i * 1.1f) * 4f;
                _plates[i].anchoredPosition = _offsets[i] * spread + new Vector2(0f, bob);
                _plates[i].localEulerAngles = new Vector3(0f, 0f, -6f + (i - 1) * 3f * _poke);
            }

            float sweep = HomeMotion.Span((float)(time % SHINE_PERIOD), 0f, 1.1f, Ease.InOutQuad);
            _shine.anchoredPosition = new Vector2(Mathf.Lerp(-PLATE_WIDTH * 0.7f, PLATE_WIDTH * 0.7f, sweep), 0f);
        }

        public void Poke()
        {
            _pokeMotion.TryCancel();
            _pokeMotion = LSequence.Create()
                .Append(LMotion.Create(_poke, 1f, 0.16f).WithEase(Ease.OutCubic).WithScheduler(HomeMotion.Scheduler).Bind(v => _poke = v))
                .Append(LMotion.Create(1f, 0f, 0.7f).WithEase(Ease.OutBack).WithScheduler(HomeMotion.Scheduler).Bind(v => _poke = v))
                .Run();
        }

        public void Dispose() => _pokeMotion.TryCancel();
    }

    /// <summary>
    /// 成就: a medal on two ribbons in front of a slowly turning crown of rays, with sparkles
    /// that come and go. A tap flips the medal over once.
    /// </summary>
    internal sealed class HomeMedalArt : IHomeCardArt
    {
        private const int RAYS = 12;
        private const float MEDAL_SIZE = 150f;

        private static readonly Vector2[] _sparklePositions =
        {
            new Vector2(-104f, 70f), new Vector2(96f, 96f), new Vector2(118f, -30f), new Vector2(-120f, -50f),
            new Vector2(10f, 128f), new Vector2(70f, -110f)
        };

        private readonly RectTransform _rays;
        private readonly RectTransform _medal;
        private readonly Image[] _sparkles = new Image[_sparklePositions.Length];
        private float _flip;
        private MotionHandle _flipMotion;

        internal HomeMedalArt(RectTransform root)
        {
            Color32 gold = HomeTheme.Colors.Gold;
            _rays = LibraryUi.CreateRect("Rays", root);
            LibraryUi.Stretch(_rays);
            for (int i = 0; i < RAYS; i++)
            {
                RectTransform spoke = LibraryUi.CreateRect("Spoke" + i, _rays);
                LibraryUi.Stretch(spoke);
                spoke.localEulerAngles = new Vector3(0f, 0f, i * 360f / RAYS);
                Image ray = LibraryWidgets.Panel("Ray", spoke, 4f, HomeTheme.Colors.WithAlpha(gold, (byte)(i % 2 == 0 ? 80 : 40)));
                LibraryWidgets.Place(ray.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 98f),
                    new Vector2(8f, i % 2 == 0 ? 34f : 22f));
            }

            Image glow = LibraryUi.CreateImage("Glow", root, HomeTheme.Colors.WithAlpha(gold, 60));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));

            Color32[] ribbons = { HomeTheme.Colors.Rose, HomeTheme.Colors.Violet };
            for (int i = 0; i < 2; i++)
            {
                Image ribbon = LibraryWidgets.Panel("Ribbon" + i, root, 6f, Color.Lerp(HomeTheme.Colors.Card, ribbons[i], 0.75f));
                LibraryWidgets.Place(ribbon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                    new Vector2(i == 0 ? -24f : 24f, -34f), new Vector2(38f, 104f));
                ribbon.rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? -18f : 18f);
            }

            _medal = LibraryUi.CreateRect("Medal", root);
            LibraryWidgets.Place(_medal, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
                new Vector2(MEDAL_SIZE, MEDAL_SIZE));
            LibraryWidgets.Shadow(_medal, MEDAL_SIZE * 0.5f, 28f, 10f, 120);
            Image disc = LibraryWidgets.Panel("Disc", _medal, MEDAL_SIZE * 0.5f, Color.Lerp(HomeTheme.Colors.Card, gold, 0.28f));
            LibraryUi.Stretch(disc.rectTransform);
            Image ring = LibraryWidgets.Shape("Ring", _medal, LibraryShapes.Ring(MEDAL_SIZE * 0.5f, 5f), gold);
            LibraryUi.Stretch(ring.rectTransform);
            Image inner = LibraryWidgets.Shape("Inner", _medal, LibraryShapes.Ring(MEDAL_SIZE * 0.39f, 2f), HomeTheme.Colors.WithAlpha(gold, 130));
            LibraryWidgets.Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(MEDAL_SIZE * 0.78f, MEDAL_SIZE * 0.78f));
            LibraryWidgets.Icon(_medal, LibraryIcon.Trophy, Color.Lerp(gold, Color.white, 0.45f), 66f);

            for (int i = 0; i < _sparkles.Length; i++)
            {
                Image sparkle = LibraryUi.CreateImage("Sparkle" + i, root, Color.white);
                sparkle.sprite = LibraryShapes.Glow();
                sparkle.raycastTarget = false;
                float size = i % 2 == 0 ? 30f : 20f;
                LibraryWidgets.Place(sparkle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _sparklePositions[i],
                    new Vector2(size, size));
                _sparkles[i] = sparkle;
            }

            Tick(0d);
        }

        public void Tick(double time)
        {
            _rays.localEulerAngles = new Vector3(0f, 0f, -(float)(time % 40d / 40d) * 360f);
            float bob = Mathf.Sin((float)(time % 4.8d / 4.8d) * Mathf.PI * 2f);
            _medal.anchoredPosition = new Vector2(0f, 14f + 4f * bob);
            float turn = Mathf.Cos(_flip * Mathf.PI * 2f);
            _medal.localScale = new Vector3(Mathf.Abs(turn) < 0.04f ? 0.04f : turn, 1f, 1f);
            for (int i = 0; i < _sparkles.Length; i++)
            {
                float p = (float)((time + i * 0.53d) % 2.4d / 2.4d);
                float shown = Mathf.Sin(p * Mathf.PI);
                Color color = _sparkles[i].color;
                color.a = shown * shown * 0.85f;
                _sparkles[i].color = color;
                float scale = 0.6f + 0.5f * shown;
                _sparkles[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public void Poke()
        {
            _flipMotion.TryCancel();
            _flipMotion = LMotion.Create(0f, 1f, 0.9f)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(v => _flip = v);
        }

        public void Dispose() => _flipMotion.TryCancel();
    }
}
