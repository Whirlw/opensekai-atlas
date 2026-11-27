using System;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// A medal at thumbnail size: its color, its frame and what its band shows (an emblem, or 10K and
    /// such), without the name, which would be too small to read. A 特殊 medal has its own face,
    /// outline and lights here too.
    /// </summary>
    internal sealed class MedalThumb
    {
        private readonly Vector2 _size;
        private readonly Image _face;
        private readonly Image _rim;
        private readonly Image _frame;
        private readonly TextMeshProUGUI _mark;
        private readonly Image _trophy;
        private readonly MedalEmblemView _art;
        private readonly SpecialMedalView _special;

        internal MedalThumb(Transform parent, string name, Vector2 size)
        {
            _size = size;
            float radius = size.y * MedalArt.COMPACT_RADIUS;
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = size;
            RectTransform back = LibraryUi.CreateRect("SpecialBack", Root);
            LibraryUi.Stretch(back);
            _face = LibraryWidgets.Panel("Face", Root, radius, Color.white);
            LibraryUi.Stretch(_face.rectTransform);
            _rim = LibraryWidgets.Shape("Rim", Root, LibraryShapes.Ring(radius, 1.5f), Color.white);
            LibraryUi.Stretch(_rim.rectTransform);
            _frame = LibraryWidgets.Shape("Frame", Root, null, Color.white);
            _frame.type = Image.Type.Simple;
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(_frame.rectTransform, center, center, Vector2.zero,
                new Vector2(size.x + MedalArt.MARGIN_X * 2f * size.y, size.y + MedalArt.MARGIN_Y * 2f * size.y));
            _mark = LibraryWidgets.Label("Mark", Root, string.Empty, size.y * 0.6f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                TextAlignmentOptions.MidlineGeoAligned);
            LibraryUi.UseCondensedFont(_mark);
            _mark.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(_mark.rectTransform);
            _trophy = LibraryWidgets.Icon(Root, LibraryIcon.Trophy, Color.white, size.y * 0.66f);
            _art = new MedalEmblemView("Art", Root);
            LibraryWidgets.Place(_art.Root, center, center, Vector2.zero, Vector2.zero);
            RectTransform front = LibraryUi.CreateRect("SpecialFront", Root);
            LibraryUi.Stretch(front);
            _special = new SpecialMedalView(back, front);
        }

        internal RectTransform Root { get; }

        internal void SetLook(MedalLook look)
        {
            Color32 light = DanStyles.Light(look.Color);
            bool special = _special.Set(look.Art);
            _face.enabled = !special;
            _face.color = Color.Lerp(HomeTheme.Colors.Card, look.Color, 0.3f);
            bool framed = look.Tier > MedalTiers.LOWEST && (!special || _special.Art.KeepsTierFrame);
            _rim.enabled = !framed && !special;
            _rim.color = HomeTheme.Colors.WithAlpha(light, 190);
            _frame.enabled = framed;
            if (framed) _frame.sprite = MedalFrames.Compact(look.Tier, _size);
            _frame.color = MedalArt.IsPrismatic(look.Tier) ? Color.white : Color.Lerp(look.Color, Color.white, 0.3f);
            bool drawn = look.Art != null;
            _art.Show(look.Art, light, _size.y * 0.74f);
            _mark.text = drawn ? string.Empty : look.Mark ?? string.Empty;
            _mark.color = light;
            _trophy.enabled = !drawn && look.Mark == null;
            _trophy.color = light;
            if (!special) return;
            float half = _size.x / _size.y * 0.5f;
            _special.Draw(_size, new PlateSpace(half * 2f, true, -half, half, half));
        }
    }

    /// <summary>
    /// What a goal gives, in a box at the end of its row: the medal, the avatar or both, dimmed with
    /// a lock until the goal is reached and checked once it is unlocked. A tap shows it large
    /// (<see cref="AchievementRewardPanel"/>).
    /// </summary>
    internal sealed class AchievementRewardBox
    {
        internal const float SIZE = 124f;

        private const float RADIUS = 18f;
        private const float CAPTION = 26f;
        private const float BADGE = 28f;

        private static readonly Vector2 _medalSize = new Vector2(84f, 42f);

        private readonly Image _background;
        private readonly Image _outline;
        private readonly Image _glow;
        private readonly CanvasGroup _content;
        private readonly MedalThumb _medal;
        private readonly RectTransform _avatarSlot;
        private readonly TextMeshProUGUI _caption;
        private readonly RectTransform _coin;
        private readonly Image _badge;
        private readonly Image _lock;
        private readonly Image _check;
        private ProfileAvatarView _avatar;
        private Color32 _tint;

        internal AchievementRewardBox(Transform parent, string name, Action onClick)
        {
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(SIZE, SIZE);
            Vector2 center = new Vector2(0.5f, 0.5f);
            _glow = LibraryUi.CreateImage("Glow", Root, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            LibraryWidgets.Place(_glow.rectTransform, center, center, Vector2.zero, new Vector2(SIZE * 1.9f, SIZE * 1.9f));
            _background = LibraryWidgets.Panel("Background", Root, RADIUS, LibraryTheme.Colors.Control);
            _background.raycastTarget = true;
            LibraryUi.Stretch(_background.rectTransform);
            _outline = LibraryWidgets.Shape("Outline", Root, LibraryShapes.Ring(RADIUS, 1.5f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(_outline.rectTransform);

            RectTransform content = LibraryUi.CreateRect("Reward", Root);
            LibraryUi.Stretch(content);
            _content = content.gameObject.AddComponent<CanvasGroup>();
            _content.blocksRaycasts = false;
            _medal = new MedalThumb(content, "Medal", _medalSize);
            _coin = LibraryWidgets.CoinIcon(content, 46f);
            _coin.anchoredPosition = new Vector2(0f, CAPTION * 0.5f);
            _avatarSlot = LibraryUi.CreateRect("AvatarSlot", content);
            _caption = LibraryWidgets.Label("RewardCaption", Root, string.Empty, 15f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryWidgets.BottomStrip(_caption.rectTransform, 4f, 4f, 6f, 22f);

            _badge = LibraryWidgets.Panel("Badge", Root, BADGE * 0.5f, Color.white);
            LibraryWidgets.Place(_badge.rectTransform, Vector2.one, center, new Vector2(-8f, -8f), new Vector2(BADGE, BADGE));
            _lock = LibraryWidgets.Icon(_badge.transform, LibraryIcon.Lock, LibraryTheme.Colors.TextMuted, BADGE * 0.6f);
            _check = LibraryWidgets.Icon(_badge.transform, LibraryIcon.Check, LibraryTheme.Colors.TextOnAccent, BADGE * 0.6f);

            Button button = _background.gameObject.AddComponent<Button>();
            button.targetGraphic = _background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            LibraryPressFeedback.Attach(_background.gameObject, Root);
        }

        internal RectTransform Root { get; }

        internal void Bind(AchievementProgress progress, AchievementLook look)
        {
            string avatarId = progress.Goal.AvatarId;
            bool medal = progress.Goal.GivesMedal;
            bool avatar = avatarId != null;
            _coin.gameObject.SetActive(!medal && !avatar);

            _tint = look.Tint;
            Vector2 center = new Vector2(0.5f, 0.5f);
            float middle = CAPTION * 0.5f;
            _medal.Root.gameObject.SetActive(medal);
            if (medal)
            {
                _medal.SetLook(MedalLook.Of(look));
                LibraryWidgets.Place(_medal.Root, center, center, new Vector2(0f, middle + (avatar ? 21f : 0f)), _medalSize);
                float shrink = avatar ? 0.8f : 1f;
                _medal.Root.localScale = new Vector3(shrink, shrink, 1f);
            }

            _avatarSlot.gameObject.SetActive(avatar);
            if (avatar)
            {
                float size = medal ? 40f : 76f;
                if (_avatar == null)
                {
                    _avatar = new ProfileAvatarView(_avatarSlot, "Avatar", 76f, glow: false);
                    LibraryWidgets.Place(_avatar.Root, center, center, Vector2.zero, new Vector2(76f, 76f));
                }

                _avatar.SetAvatar(avatarId);
                LibraryWidgets.Place(_avatarSlot, center, center, new Vector2(0f, middle - (medal ? 25f : 0f)), new Vector2(76f, 76f));
                _avatarSlot.localScale = new Vector3(size / 76f, size / 76f, 1f);
            }

            _caption.text = "+" + ProfileCopy.Number(progress.Goal.CoinReward) + " 金币";
            AchievementState state = progress.State;
            bool unlocked = state == AchievementState.Unlocked;
            bool under = state == AchievementState.InProgress;
            _content.alpha = under ? 0.5f : 1f;
            _background.color = under ? LibraryTheme.Colors.Control : HomeTheme.Colors.WithAlpha(_tint, 26);
            _outline.color = under ? LibraryTheme.Colors.Hairline : HomeTheme.Colors.WithAlpha(_tint, (byte)(unlocked ? 110 : 190));
            _badge.gameObject.SetActive(under || unlocked);
            _badge.color = unlocked ? _tint : LibraryTheme.Colors.SurfaceSelected;
            _lock.enabled = under;
            _check.enabled = unlocked;
            SetMotion(1f, 0f, 1f);
        }

        /// <param name="scale">Size of the box.</param>
        /// <param name="glow">0 to 1: light behind it.</param>
        /// <param name="badge">Size of its check or lock.</param>
        internal void SetMotion(float scale, float glow, float badge)
        {
            Root.localScale = new Vector3(scale, scale, 1f);
            _glow.enabled = glow > 0f;
            _glow.color = HomeTheme.Colors.WithAlpha(_tint, (byte)Mathf.RoundToInt(150f * Mathf.Clamp01(glow)));
            _badge.rectTransform.localScale = new Vector3(badge, badge, 1f);
        }
    }
}
