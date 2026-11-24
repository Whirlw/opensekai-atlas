using System;
using System.Globalization;
using LitMotion;
using OpenSekai.App.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One level of an achievement inside its row: the medal with its ring, what it asks for and how
    /// it is counted, where it stands, and at the end of the bar a box with what it gives. A row
    /// slides two of these side by side.
    /// </summary>
    internal sealed class AchievementPane
    {
        internal const float MEDAL = 108f;
        internal const float LEFT = 22f;

        /// <summary>How far the medal moves up to make room for the stars.</summary>
        internal const float MEDAL_LIFT = 15f;

        private const float TEXT_LEFT = LEFT + MEDAL + 28f;
        private const float STATUS_WIDTH = 250f;
        private const float EDGE = 24f;
        private const float REWARD_GAP = 24f;
        private const float TEXT_GAP = 28f;

        private readonly AchievementMedal _medal;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _latin;
        private readonly TextMeshProUGUI _goal;
        private readonly TextMeshProUGUI _rule;
        private readonly TextMeshProUGUI _percent;
        private readonly TextMeshProUGUI _pending;
        private readonly RectTransform _tag;
        private readonly CanvasGroup _tagGroup;
        private readonly RectTransform _bar;
        private readonly Image _barFill;
        private readonly TextMeshProUGUI _value;
        private readonly TextMeshProUGUI _extra;
        private readonly AchievementRewardBox _reward;

        internal AchievementPane(RectTransform parent, string name, Action<AchievementProgress> onReward)
        {
            Root = LibraryUi.CreateRect(name, parent);
            LibraryUi.Stretch(Root);
            _medal = new AchievementMedal(Root, "Medal", MEDAL);

            _title = LibraryWidgets.Label("Title", Root, string.Empty, 25f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _latin = LibraryWidgets.Label("Latin", Root, string.Empty, 15f, LibraryTheme.Colors.TextMuted);
            LibraryUi.UseSpacedCondensedFont(_latin, 3f);
            _goal = LibraryWidgets.Label("Goal", Root, string.Empty, 19f, LibraryTheme.Colors.TextSecondary);
            _rule = PackWidgets.Paragraph(Root, "Rule", string.Empty, 16f, LibraryTheme.Colors.TextMuted);
            _rule.lineSpacing = 0f;

            _percent = LibraryWidgets.Label("Percent", Root, string.Empty, 44f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                TextAlignmentOptions.TopRight);
            LibraryUi.UseCondensedFont(_percent);
            _percent.overflowMode = TextOverflowModes.Overflow;
            _pending = LibraryWidgets.Label("Pending", Root, "未达成", 21f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.TopRight);
            _tag = PackWidgets.Tag(Root, "已解锁", AchievementCopy.FullComboTint, out _, 34f, true);
            _tag.name = "StateTag";
            _tagGroup = _tag.gameObject.AddComponent<CanvasGroup>();
            _tagGroup.blocksRaycasts = false;

            Image bar = LibraryWidgets.Panel("Bar", Root, 5f, HomeTheme.Colors.Chip);
            _bar = bar.rectTransform;
            _barFill = LibraryWidgets.Panel("Fill", _bar, 5f, Color.white);
            _barFill.rectTransform.anchorMin = Vector2.zero;
            _barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _barFill.rectTransform.offsetMin = Vector2.zero;
            _barFill.rectTransform.offsetMax = Vector2.zero;
            _value = LibraryWidgets.Label("Value", Root, string.Empty, 19f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                TextAlignmentOptions.TopRight);
            _extra = LibraryWidgets.Label("Extra", Root, string.Empty, 16f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                TextAlignmentOptions.TopRight);

            _reward = new AchievementRewardBox(Root, "RewardBox", () => onReward(Progress));
            Vector2 right = new Vector2(1f, 0.5f);
            LibraryWidgets.Place(_reward.Root, right, new Vector2(0.5f, 0.5f), new Vector2(-EDGE - AchievementRewardBox.SIZE * 0.5f, 0f),
                _reward.Root.sizeDelta);
        }

        internal RectTransform Root { get; }

        /// <summary>The level it shows; null before the first <see cref="Bind"/>.</summary>
        internal AchievementProgress Progress { get; private set; }

        /// <param name="withStars">Leaves room under the medal for the stars.</param>
        internal void Bind(AchievementProgress progress, bool withStars)
        {
            Progress = progress;
            AchievementLook look = AchievementCopy.For(progress);
            AchievementState state = progress.State;
            bool lit = state != AchievementState.InProgress;
            bool oneShot = AchievementCopy.IsOneShot(progress);
            float fraction = AchievementCopy.Fraction(progress);
            Color32 tint = look.Tint;
            LibraryWidgets.Place(_medal.Root, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(LEFT + MEDAL * 0.5f, withStars ? MEDAL_LIFT : 0f), new Vector2(MEDAL, MEDAL));
            _medal.Bind(look, state, oneShot && !lit ? 0f : fraction);
            _reward.Bind(progress, look);
            LayOut(_reward.Root.gameObject.activeSelf);

            _title.text = look.Title;
            _title.color = lit ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextSecondary;
            _latin.text = look.Latin;
            _latin.color = lit ? HomeTheme.Colors.WithAlpha(tint, 220) : LibraryTheme.Colors.TextMuted;
            _goal.text = look.Goal;
            _rule.text = look.Rule;

            _tag.gameObject.SetActive(lit);
            PackWidgets.SetTag(_tag, state == AchievementState.Claimable ? "可解锁" : "已解锁", tint, true);
            _percent.gameObject.SetActive(!lit && !oneShot);
            _pending.gameObject.SetActive(!lit && oneShot);
            _bar.gameObject.SetActive(lit || !oneShot);
            _barFill.rectTransform.anchorMax = new Vector2(fraction, 1f);
            _barFill.color = lit ? tint : HomeTheme.Colors.WithAlpha(tint, 200);
            _barFill.enabled = fraction > 0f;
            _percent.text = AchievementCopy.Percent(progress).ToString(CultureInfo.InvariantCulture) + "<size=55%>%</size>";
            _percent.color = progress.Current > 0 ? (Color32)Color.Lerp(tint, Color.white, 0.25f) : LibraryTheme.Colors.TextMuted;
            switch (state)
            {
                case AchievementState.Unlocked: _value.text = AchievementCopy.When(progress.UnlockedAtUtc.Value) + " 解锁"; break;
                case AchievementState.Claimable: _value.text = "点击解锁"; break;
                default: _value.text = oneShot ? string.Empty : AchievementCopy.Count(progress); break;
            }

            _value.color = state == AchievementState.Claimable ? (Color)Color.Lerp(tint, Color.white, 0.25f) : (Color)LibraryTheme.Colors.TextSecondary;
            string total = lit ? AchievementCopy.Total(progress) : null;
            _extra.text = total ?? string.Empty;
            _extra.gameObject.SetActive(total != null);
            Rest();
        }

        /// <summary>The idle look: no motion, the glow of the state.</summary>
        internal void Rest()
        {
            _medal.SetMotion(1f, 0f);
            _medal.SetFlash(0f);
            _medal.SetCheck(1f);
            _tag.localScale = Vector3.one;
            _tagGroup.alpha = 1f;
            _reward.SetMotion(1f, 0f, 1f);
        }

        /// <param name="wave">0 to 1 and back, over and over: a level that can be unlocked breathes.</param>
        internal void Pulse(float wave)
        {
            if (Progress == null || Progress.State != AchievementState.Claimable) return;
            _medal.SetGlow(Mathf.Lerp(0.12f, 0.45f, wave));
            float grow = Mathf.Lerp(0.985f, 1.02f, wave);
            _medal.SetMotion(grow, 0f);
            _tagGroup.alpha = Mathf.Lerp(0.6f, 1f, wave);
        }

        /// <summary>Draws in before the unlock: the medal shrinks and lights up.</summary>
        /// <param name="amount">0 to 1.</param>
        internal void Charge(float amount)
        {
            _medal.SetMotion(Mathf.Lerp(1f, 0.84f, amount), 0f);
            _medal.SetGlow(Mathf.Lerp(0.3f, 0.75f, amount));
            _medal.SetFlash(0.35f * amount);
        }

        /// <summary>The unlock itself, bound to the unlocked level: the medal bursts, the check, tag and reward pop in.</summary>
        /// <param name="time">Seconds since the level was unlocked.</param>
        internal void Release(float time)
        {
            float grow = time < 0.1f
                ? Mathf.Lerp(0.84f, 1.24f, HomeMotion.Span(time, 0f, 0.1f, Ease.OutQuad))
                : Mathf.LerpUnclamped(1.24f, 1f, HomeMotion.Span(time, 0.1f, 0.42f, Ease.OutBack));
            _medal.SetMotion(grow, HomeMotion.Span(time, 0.2f, 0.6f, Ease.InOutQuad));
            _medal.SetFlash(0.95f * (1f - HomeMotion.Span(time, 0f, 0.4f, Ease.OutQuad)));
            _medal.SetGlow(Mathf.Lerp(0.95f, 0.25f, HomeMotion.Span(time, 0.1f, 0.9f, Ease.OutQuad)));
            _medal.SetCheck(Mathf.LerpUnclamped(0f, 1f, HomeMotion.Span(time, 0.3f, 0.34f, Ease.OutBack)));
            float tag = Mathf.LerpUnclamped(0.5f, 1f, HomeMotion.Span(time, 0.1f, 0.32f, Ease.OutBack));
            _tag.localScale = new Vector3(tag, tag, 1f);
            _tagGroup.alpha = HomeMotion.Span(time, 0.1f, 0.12f);
            float reward = Mathf.Sin(HomeMotion.Span(time, 0.42f, 0.5f) * Mathf.PI);
            _reward.SetMotion(1f + 0.16f * reward, reward, Mathf.LerpUnclamped(0f, 1f, HomeMotion.Span(time, 0.55f, 0.34f, Ease.OutBack)));
        }

        private void LayOut(bool withReward)
        {
            float edge = EDGE + (withReward ? AchievementRewardBox.SIZE + REWARD_GAP : 0f);
            float textRight = edge + STATUS_WIDTH + TEXT_GAP;
            LibraryWidgets.TopStrip(_title.rectTransform, TEXT_LEFT, textRight, 26f, 36f);
            float titleWidth = Mathf.Ceil(_title.GetPreferredValues(AchievementCopy.For(Progress).Title).x);
            LibraryWidgets.TopStrip(_latin.rectTransform, TEXT_LEFT + titleWidth + 14f, textRight, 36f, 22f);
            LibraryWidgets.TopStrip(_goal.rectTransform, TEXT_LEFT, textRight, 66f, 28f);
            LibraryWidgets.Inset(_rule.rectTransform, TEXT_LEFT, 14f, textRight, 102f);

            Vector2 corner = Vector2.one;
            LibraryWidgets.Place(_percent.rectTransform, corner, corner, new Vector2(-edge, -20f), new Vector2(STATUS_WIDTH, 52f));
            LibraryWidgets.Place(_pending.rectTransform, corner, corner, new Vector2(-edge, -70f), new Vector2(STATUS_WIDTH, 30f));
            LibraryWidgets.Place(_tag, corner, corner, new Vector2(-edge, -30f), _tag.sizeDelta);
            LibraryWidgets.Place(_bar, corner, corner, new Vector2(-edge, -82f), new Vector2(STATUS_WIDTH, 10f));
            LibraryWidgets.Place(_value.rectTransform, corner, corner, new Vector2(-edge, -102f), new Vector2(STATUS_WIDTH, 28f));
            LibraryWidgets.Place(_extra.rectTransform, corner, corner, new Vector2(-edge, -130f), new Vector2(STATUS_WIDTH, 24f));
        }
    }
}
