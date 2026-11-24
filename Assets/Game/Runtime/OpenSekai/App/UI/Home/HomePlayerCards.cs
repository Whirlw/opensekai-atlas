using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using OpenSekai.App.Library;
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

        /// <param name="medals">The worn medals, at most two; null or empty when none is worn.</param>
        internal HomeProfileSummary(string nickname, string avatarId, long total, int tierId, int chartCount, int playCount,
            IReadOnlyList<MedalLook> medals, bool unreadable)
        {
            Nickname = nickname;
            AvatarId = avatarId;
            Total = total;
            TierId = tierId;
            ChartCount = chartCount;
            PlayCount = playCount;
            Medals = medals ?? Array.Empty<MedalLook>();
            Unreadable = unreadable;
        }

        internal string Nickname { get; }
        internal string AvatarId { get; }
        internal long Total { get; }
        internal int TierId { get; }

        /// <summary>Charts in the Best 30.</summary>
        internal int ChartCount { get; }

        internal int PlayCount { get; }

        /// <summary>The worn medals in slot order, without the empty slots.</summary>
        internal IReadOnlyList<MedalLook> Medals { get; }

        /// <summary>The profile could not be read; the card says so instead of showing zeros as fact.</summary>
        internal bool Unreadable { get; }
    }

    /// <summary>
    /// The second page's big card, the way into 个人信息: the player's avatar and nickname, the
    /// worn medals beside it, the Ranking Score with its tier, and how many charts and plays stand behind
    /// it. On the right the tier badge floats in its own color in a sky that grows richer with the
    /// tier (<see cref="HomeRankingSky"/>). Like the library card it grows over the page when tapped.
    /// </summary>
    internal sealed class HomeProfileCard : IHomeCard
    {
        private const float TEXT_WIDTH = 560f;
        private const float FADE_WIDTH = 240f;
        private const float CTA_WIDTH = 252f;
        private const float CTA_HEIGHT = 80f;
        private const float AVATAR_SIZE = 104f;
        private const float BADGE_SIZE = 260f;

        /// <summary>Both medals together with the gap between them; long ones shrink to fit.</summary>
        private const float MEDALS_WIDTH = 424f;
        private const float MEDAL_GAP = 24f;

        private readonly HomeCardFrame _frame;
        private readonly Image _glow;
        private readonly CanvasGroup _art;
        private readonly CanvasGroup _badgeLayer;
        private readonly RectTransform _badgeAnchor;
        private readonly RankingBadge _badge;
        private readonly Image _badgeGlow;
        private readonly HomeRankingSky _sky;
        private readonly Image _cover;
        private readonly Image _coverFade;
        private readonly CanvasGroup _copy;
        private readonly TextMeshProUGUI _eyebrow;
        private readonly ProfileAvatarView _avatar;
        private readonly TextMeshProUGUI _nickname;
        private readonly MedalChip[] _medals = new MedalChip[Medal.SLOTS];
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
            _sky = new HomeRankingSky(artRoot);

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

            // Who: avatar, nickname and the worn medals.
            float identityTop = pad + 44f;
            _avatar = new ProfileAvatarView(copyRoot, "Avatar", AVATAR_SIZE);
            LibraryWidgets.Place(_avatar.Root, topLeft, topLeft, new Vector2(pad, -identityTop), new Vector2(AVATAR_SIZE, AVATAR_SIZE));
            float nameLeft = pad + AVATAR_SIZE + 26f;
            _nickname = LibraryWidgets.Label("Nickname", copyRoot, string.Empty, 50f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _nickname.richText = false;
            LibraryWidgets.Place(_nickname.rectTransform, topLeft, topLeft, new Vector2(nameLeft, -identityTop + 4f),
                new Vector2(TEXT_WIDTH - nameLeft - 12f, 62f));
            for (int i = 0; i < _medals.Length; i++)
            {
                _medals[i] = new MedalChip(copyRoot, "Medal" + i);
                LibraryWidgets.Place(_medals[i].Root, topLeft, topLeft, new Vector2(nameLeft, -identityTop - 67f), _medals[i].Root.sizeDelta);
            }

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
            if (_summary.Unreadable)
            {
                _tier.enableVertexGradient = false;
                _tier.color = LibraryTheme.Colors.TextMuted;
            }
            else
            {
                RankingTiers.Paint(_tier, _summary.TierId);
            }
            _charts.text = _summary.ChartCount.ToString(CultureInfo.InvariantCulture);
            _plays.text = _summary.PlayCount.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < _medals.Length; i++)
            {
                bool worn = i < _summary.Medals.Count;
                // With none worn, one empty chip holds the place.
                _medals[i].Root.gameObject.SetActive(worn || i == 0);
                if (worn) _medals[i].SetLook(_summary.Medals[i]);
                else _medals[i].SetEmpty("未佩戴奖牌");
            }

            LayOutMedals(_summary.Medals.Count);
            ApplyAccent(_summary.Unreadable ? LibraryTheme.Colors.TextMuted : RankingTiers.Color(_summary.TierId));
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
            _sky.Tick(time);
            _badge.Tick(time);
        }

        /// <summary>Puts the worn medals side by side; a short one leaves its room to the other.</summary>
        private void LayOutMedals(int worn)
        {
            if (worn < _medals.Length)
            {
                _medals[0].SetWidth(Mathf.Min(_medals[0].FitWidth, MEDALS_WIDTH));
                return;
            }

            float room = MEDALS_WIDTH - MEDAL_GAP;
            float first = Mathf.Min(_medals[0].FitWidth, Mathf.Max(room * 0.5f, room - _medals[1].FitWidth));
            _medals[0].SetWidth(first);
            _medals[1].SetWidth(Mathf.Min(_medals[1].FitWidth, room - first));
            Vector2 start = _medals[0].Root.anchoredPosition;
            _medals[1].Root.anchoredPosition = new Vector2(start.x + first + MEDAL_GAP, start.y);
        }

        private void ApplyAccent(Color32 accent)
        {
            _eyebrow.color = accent;
            _cta.color = accent;
            _ctaGlow.color = HomeTheme.Colors.WithAlpha(accent, 46);
            _glow.color = HomeTheme.Colors.WithAlpha(accent, 70);
            _badgeGlow.color = HomeTheme.Colors.WithAlpha(accent, 60);
            _sky.SetTier(_summary.Unreadable ? 0 : _summary.TierId, accent);
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
    /// 成就: a medal on two ribbons inside a slowly turning crown of rays, all of it floating as one,
    /// with sparkles that come and go. While an achievement can be unlocked the medal is lit: its face
    /// turns solid gold, the rays reach out and take turns, and every few seconds it swings once while
    /// a light crosses its face and rings go out from its rim. A tap flips the medal over once.
    /// </summary>
    internal sealed class HomeMedalArt : IHomeCardArt
    {
        /// <summary>Seconds from one swing of a lit medal to the next.</summary>
        internal const float BEAT = 3.2f;

        private const int RAYS = 12;
        private const float MEDAL_SIZE = 150f;

        /// <summary>The medal sits above the middle of the art, so that it and its ribbons fill it evenly.</summary>
        private const float MEDAL_RISE = 14f;
        private const float FLOAT = 4f;
        private const float FLOAT_SECONDS = 4.8f;
        private const float TURN_SECONDS = 40f;
        private const float RAY_START = 98f;
        private const float RAY_WIDTH = 8f;
        private const float RAY_SECONDS = 1.6f;
        private const float SWING_SECONDS = 1.1f;
        private const float SWING_DEGREES = 11f;
        private const float LIGHT_SECONDS = 0.6f;

        private static readonly Vector2[] _sparklePositions =
        {
            new Vector2(-104f, 70f), new Vector2(96f, 96f), new Vector2(118f, -30f), new Vector2(-120f, -50f),
            new Vector2(10f, 128f), new Vector2(70f, -110f)
        };

        private readonly RectTransform _body;
        private readonly RectTransform _rays;
        private readonly Image[] _rayImages = new Image[RAYS];
        private readonly Image _glow;
        private readonly Image[] _pulses = new Image[2];
        private readonly RectTransform[] _ribbons = new RectTransform[2];
        private readonly RectTransform _medal;
        private readonly Image _disc;
        private readonly Image _ring;
        private readonly Image _inner;
        private readonly Image _trophy;
        private readonly RectTransform _shine;
        private readonly Image[] _sparkles = new Image[_sparklePositions.Length];
        private readonly Image[] _stars = new Image[_sparklePositions.Length];
        private float _flip;
        private MotionHandle _flipMotion;
        private float _lit;
        private MotionHandle _lightMotion;

        internal HomeMedalArt(RectTransform root)
        {
            Color32 gold = HomeTheme.Colors.Gold;
            Vector2 center = new Vector2(0.5f, 0.5f);

            // Everything but the sparkles hangs from the medal's center and floats with it.
            _body = LibraryUi.CreateRect("Body", root);
            LibraryWidgets.Place(_body, center, center, new Vector2(0f, MEDAL_RISE), new Vector2(MEDAL_SIZE, MEDAL_SIZE));
            _rays = LibraryUi.CreateRect("Rays", _body);
            LibraryUi.Stretch(_rays);
            for (int i = 0; i < RAYS; i++)
            {
                RectTransform spoke = LibraryUi.CreateRect("Spoke" + i, _rays);
                LibraryUi.Stretch(spoke);
                spoke.localEulerAngles = new Vector3(0f, 0f, i * 360f / RAYS);
                _rayImages[i] = LibraryWidgets.Panel("Ray", spoke, 4f, gold);
                LibraryWidgets.Place(_rayImages[i].rectTransform, center, new Vector2(0.5f, 0f), new Vector2(0f, RAY_START),
                    new Vector2(RAY_WIDTH, RAY_WIDTH));
            }

            _glow = LibraryUi.CreateImage("Glow", _body, gold);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            LibraryWidgets.Place(_glow.rectTransform, center, center, Vector2.zero, new Vector2(300f, 300f));
            for (int i = 0; i < _pulses.Length; i++)
            {
                _pulses[i] = LibraryWidgets.Shape("Pulse" + i, _body, LibraryShapes.Ring(MEDAL_SIZE * 0.5f, i == 0 ? 3f : 2f), gold);
                LibraryUi.Stretch(_pulses[i].rectTransform);
            }

            Color32[] ribbons = { HomeTheme.Colors.Rose, HomeTheme.Colors.Violet };
            for (int i = 0; i < _ribbons.Length; i++)
            {
                Image ribbon = LibraryWidgets.Panel("Ribbon" + i, _body, 6f, Color.Lerp(HomeTheme.Colors.Card, ribbons[i], 0.75f));
                LibraryWidgets.Place(ribbon.rectTransform, center, new Vector2(0.5f, 1f),
                    new Vector2(i == 0 ? -24f : 24f, -34f - MEDAL_RISE), new Vector2(38f, 104f));
                _ribbons[i] = ribbon.rectTransform;
            }

            _medal = LibraryUi.CreateRect("Medal", _body);
            LibraryUi.Stretch(_medal);
            LibraryWidgets.Shadow(_medal, MEDAL_SIZE * 0.5f, 28f, 10f, 120);
            _disc = LibraryWidgets.Panel("Disc", _medal, MEDAL_SIZE * 0.5f, gold);
            LibraryUi.Stretch(_disc.rectTransform);

            // A mask has a hard edge: this one ends under the ring, so that the disc keeps its smooth one.
            const float FACE = MEDAL_SIZE - 5f;
            Image face = LibraryWidgets.Panel("Face", _medal, FACE * 0.5f, Color.white);
            LibraryWidgets.Place(face.rectTransform, center, center, Vector2.zero, new Vector2(FACE, FACE));
            face.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image shine = LibraryUi.CreateImage("Shine", face.transform, HomeTheme.Colors.WithAlpha(Color.white, 120));
            shine.raycastTarget = false;
            LibraryWidgets.Place(shine.rectTransform, center, center, Vector2.zero, new Vector2(34f, MEDAL_SIZE * 1.6f));
            shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 28f);
            _shine = shine.rectTransform;
            _ring = LibraryWidgets.Shape("Ring", _medal, LibraryShapes.Ring(MEDAL_SIZE * 0.5f, 5f), gold);
            LibraryUi.Stretch(_ring.rectTransform);
            _inner = LibraryWidgets.Shape("Inner", _medal, LibraryShapes.Ring(MEDAL_SIZE * 0.39f, 2f), gold);
            LibraryWidgets.Place(_inner.rectTransform, center, center, Vector2.zero, new Vector2(MEDAL_SIZE * 0.78f, MEDAL_SIZE * 0.78f));
            _trophy = LibraryWidgets.Icon(_medal, LibraryIcon.Trophy, gold, 66f);

            for (int i = 0; i < _sparkles.Length; i++)
            {
                Image sparkle = LibraryUi.CreateImage("Sparkle" + i, root, Color.white);
                sparkle.sprite = LibraryShapes.Glow();
                sparkle.raycastTarget = false;
                float size = i % 2 == 0 ? 30f : 20f;
                LibraryWidgets.Place(sparkle.rectTransform, center, center, _sparklePositions[i], new Vector2(size, size));
                _sparkles[i] = sparkle;
                // Lit, each sparkle has a star at its heart.
                _stars[i] = LibraryWidgets.Icon(sparkle.transform, LibraryIcon.Star, Color.Lerp(gold, Color.white, 0.55f), size);
            }

            ApplyLight();
            Tick(0d);
        }

        /// <summary>Whether there is an achievement to unlock: the medal is lit, or about to be.</summary>
        internal bool IsClaimable { get; private set; }

        /// <summary>Lights the medal while there is an achievement to unlock, and puts it out after the last one.</summary>
        internal void SetClaimable(bool claimable)
        {
            if (IsClaimable == claimable) return;
            IsClaimable = claimable;
            _lightMotion.TryCancel();
            _lightMotion = LMotion.Create(_lit, claimable ? 1f : 0f, LIGHT_SECONDS)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(this, static (value, art) =>
                {
                    art._lit = value;
                    art.ApplyLight();
                });
        }

        public void Tick(double time)
        {
            _rays.localEulerAngles = new Vector3(0f, 0f, -(float)(time % TURN_SECONDS / TURN_SECONDS) * 360f);
            float bob = Mathf.Sin((float)(time % FLOAT_SECONDS / FLOAT_SECONDS) * Mathf.PI * 2f);
            _body.anchoredPosition = new Vector2(0f, MEDAL_RISE + FLOAT * bob);
            float turn = Mathf.Cos(_flip * Mathf.PI * 2f);
            _medal.localScale = new Vector3(Mathf.Abs(turn) < 0.04f ? 0.04f : turn, 1f, 1f);
            TickLight(time);
            for (int i = 0; i < _sparkles.Length; i++)
            {
                float p = (float)((time + i * 0.53d) % 2.4d / 2.4d);
                float shown = Mathf.Sin(p * Mathf.PI);
                Color color = _sparkles[i].color;
                color.a = shown * shown * Mathf.Lerp(0.85f, 0.45f, _lit);
                _sparkles[i].color = color;
                Color star = _stars[i].color;
                star.a = shown * shown * _lit;
                _stars[i].color = star;
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

        public void Dispose()
        {
            _flipMotion.TryCancel();
            _lightMotion.TryCancel();
        }

        /// <summary>The colors of the medal as far as it is lit; they change with the state, not with time.</summary>
        private void ApplyLight()
        {
            Color gold = HomeTheme.Colors.Gold;
            Color ink = Color.Lerp(HomeTheme.Colors.Card, gold, 0.1f);
            _disc.color = Color.Lerp(HomeTheme.Colors.Card, gold, Mathf.Lerp(0.28f, 0.94f, _lit));
            _ring.color = Color.Lerp(gold, Color.white, 0.5f * _lit);
            _inner.color = Color.Lerp(HomeTheme.Colors.WithAlpha(gold, 130), new Color(ink.r, ink.g, ink.b, 0.45f), _lit);
            _trophy.color = Color.Lerp(Color.Lerp(gold, Color.white, 0.45f), ink, _lit);
            for (int i = 0; i < RAYS; i++)
            {
                float alpha = i % 2 == 0 ? Mathf.Lerp(80f, 235f, _lit) : Mathf.Lerp(40f, 150f, _lit);
                _rayImages[i].color = HomeTheme.Colors.WithAlpha(gold, (byte)Mathf.RoundToInt(alpha));
            }
        }

        /// <summary>What moves on a lit medal; unlit, all of it rests.</summary>
        private void TickLight(double time)
        {
            float wave = Mathf.Sin((float)(time % RAY_SECONDS / RAY_SECONDS) * Mathf.PI * 2f);
            for (int i = 0; i < RAYS; i++)
            {
                // Long and short rays take turns reaching out.
                float rest = i % 2 == 0 ? 34f : 22f;
                float reach = i % 2 == 0 ? 6f + 5f * wave : 5f - 5f * wave;
                _rayImages[i].rectTransform.sizeDelta = new Vector2(RAY_WIDTH, rest + reach * _lit);
            }

            float beat = (float)(time % BEAT);
            float glow = Mathf.Lerp(60f, 105f + 25f * wave, _lit);
            _glow.color = HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, (byte)Mathf.RoundToInt(glow));
            for (int i = 0; i < _pulses.Length; i++)
            {
                float away = HomeMotion.Span(beat, 0.12f + i * 0.22f, 1.3f);
                float eased = 1f - (1f - away) * (1f - away);
                float scale = Mathf.Lerp(1f, i == 0 ? 1.85f : 1.6f, eased);
                _pulses[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                float alpha = away > 0f ? (1f - away) * _lit : 0f;
                _pulses[i].color = HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, (byte)Mathf.RoundToInt(150f * alpha));
            }

            // A pendulum dying down: the medal goes one way, its ribbons trail the other. On a round
            // medal only what is drawn on it has to turn; its edge and its shadow stay put.
            float swing = HomeMotion.Span(beat, 0f, SWING_SECONDS);
            float angle = Mathf.Sin(swing * Mathf.PI * 5f) * (1f - swing) * (1f - swing) * SWING_DEGREES * _lit;
            _trophy.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            for (int i = 0; i < _ribbons.Length; i++)
            {
                _ribbons[i].localEulerAngles = new Vector3(0f, 0f, (i == 0 ? -18f : 18f) - angle * 0.45f);
            }

            float sweep = HomeMotion.Span(beat, 0.15f, 0.85f, Ease.InOutQuad);
            bool crossing = _lit > 0f && sweep > 0f && sweep < 1f;
            if (_shine.gameObject.activeSelf != crossing) _shine.gameObject.SetActive(crossing);
            if (crossing) _shine.anchoredPosition = new Vector2(Mathf.Lerp(-MEDAL_SIZE * 0.8f, MEDAL_SIZE * 0.8f, sweep), 0f);
        }
    }
}
