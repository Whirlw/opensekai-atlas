using System;
using System.Collections.Generic;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 成就解锁: a card that drops in at the top of a page with the new medals, holds for a few
    /// seconds and lifts away. Goals reached together share one card (their medals fanned out and
    /// their names in a row), so a run that unlocks three says so once. It never takes a tap; the
    /// page under it stays usable. The whole card is one timeline (<see cref="LENGTH"/> seconds),
    /// so an owner can fold its entrance into its own opening (<see cref="Seek"/>) and let it run on
    /// from there (<see cref="Play"/>).
    /// </summary>
    internal sealed class AchievementUnlockPopup : IDisposable
    {
        /// <summary>Seconds until the card has landed and its medals have popped.</summary>
        internal const float ENTER = 0.7f;

        /// <summary>Seconds from dropping in to gone.</summary>
        internal const float LENGTH = 4.6f;

        private const float EXIT = 0.4f;
        private const float HEIGHT = 132f;
        private const float MEDAL = 92f;
        private const float MEDAL_STEP = 40f;
        private const int MAX_MEDALS = 3;
        private const float PAD = 24f;
        private const float MIN_WIDTH = 620f;
        private const float MAX_WIDTH = 980f;

        private readonly RectTransform _card;
        private readonly CanvasGroup _group;
        private readonly float _top;
        private readonly float _x;
        private readonly Image _glow;
        private readonly Image _outline;
        private readonly RectTransform _shine;
        private readonly RectTransform _medalRow;
        private readonly AchievementMedal[] _medals = new AchievementMedal[MAX_MEDALS];
        private readonly TextMeshProUGUI _eyebrow;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _line;
        private int _count;
        private MotionHandle _motion;

        /// <param name="parent">Full-screen rect the card sits in, centered at the top.</param>
        /// <param name="top">Distance from the top edge to the card at rest.</param>
        /// <param name="right">
        /// When set, the card sits this far from the right edge instead of in the middle, for a page
        /// whose top center is taken (the result page's award mark).
        /// </param>
        internal AchievementUnlockPopup(RectTransform parent, float top, float? right = null)
        {
            _top = top;
            _x = right.HasValue ? -right.Value : 0f;
            _card = LibraryUi.CreateRect("AchievementUnlock", parent);
            Vector2 anchor = right.HasValue ? Vector2.one : new Vector2(0.5f, 1f);
            LibraryWidgets.Place(_card, anchor, anchor, new Vector2(_x, -top), new Vector2(MIN_WIDTH, HEIGHT));
            _group = _card.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Color32 gold = AchievementCopy.FullComboTint;
            _glow = LibraryUi.CreateImage("UnlockGlow", _card, HomeTheme.Colors.WithAlpha(gold, 60));
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            LibraryWidgets.Place(_glow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PAD + MEDAL * 0.5f, 0f),
                new Vector2(360f, 360f));
            LibraryWidgets.Shadow(_card, HEIGHT * 0.5f, 36f, 12f, 170);
            Image surface = LibraryWidgets.Panel("UnlockSurface", _card, HEIGHT * 0.5f, LibraryTheme.Colors.SurfaceRaised);
            LibraryUi.Stretch(surface.rectTransform);
            surface.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image wash = LibraryWidgets.Shape("UnlockWash", surface.transform, HomeShapes.FadeRight(), HomeTheme.Colors.WithAlpha(gold, 34));
            wash.type = Image.Type.Simple;
            LibraryUi.Stretch(wash.rectTransform);
            Image shine = LibraryUi.CreateImage("UnlockShine", surface.transform, new Color32(255, 255, 255, 40));
            shine.raycastTarget = false;
            LibraryWidgets.Place(shine.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, HEIGHT * 2.2f));
            shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 24f);
            _shine = shine.rectTransform;
            _outline = LibraryWidgets.Shape("UnlockOutline", _card, LibraryShapes.Ring(HEIGHT * 0.5f, 1.5f), HomeTheme.Colors.WithAlpha(gold, 150));
            LibraryUi.Stretch(_outline.rectTransform);

            _medalRow = LibraryUi.CreateRect("UnlockMedals", _card);
            LibraryWidgets.Place(_medalRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PAD, 0f), new Vector2(MEDAL, MEDAL));
            // Built back to front: the first medal ends up on top.
            for (int i = MAX_MEDALS - 1; i >= 0; i--)
            {
                _medals[i] = new AchievementMedal(_medalRow, "UnlockMedal" + i, MEDAL, withRing: false);
                LibraryWidgets.Place(_medals[i].Root, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(MEDAL * 0.5f + i * MEDAL_STEP, 0f), new Vector2(MEDAL, MEDAL));
            }

            _eyebrow = LibraryWidgets.Label("UnlockEyebrow", _card, "成就解锁  ·  ACHIEVEMENT UNLOCKED", 17f, gold, FontStyles.Bold);
            LibraryUi.SetLetterSpacing(_eyebrow, 2f);
            _title = PackWidgets.UserLabel("UnlockTitle", _card, string.Empty, 30f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _line = LibraryWidgets.Label("UnlockLine", _card, string.Empty, 18f, LibraryTheme.Colors.TextSecondary);
            _line.richText = false;
            _card.gameObject.SetActive(false);
        }

        /// <summary>True while the card is on screen.</summary>
        internal bool IsShowing => _card.gameObject.activeSelf;

        /// <summary>
        /// Fills the card for <paramref name="ids"/> and keeps it hidden until <see cref="Play"/> or
        /// <see cref="Seek"/>. False (and nothing to show) when there are none.
        /// </summary>
        /// <param name="context">Where the goals came from (合并备份后解锁); null for a run.</param>
        internal bool Bind(IReadOnlyList<string> ids, string context)
        {
            Hide();
            _count = ids?.Count ?? 0;
            if (_count == 0) return false;

            int shown = Mathf.Min(MAX_MEDALS, _count);
            for (int i = 0; i < MAX_MEDALS; i++)
            {
                bool used = i < shown;
                _medals[i].Root.gameObject.SetActive(used);
                if (used) _medals[i].Bind(AchievementCopy.For(ids[i]), true, 1f);
            }

            AchievementLook first = AchievementCopy.For(ids[0]);
            _glow.color = HomeTheme.Colors.WithAlpha(first.Tint, 70);
            if (_count == 1)
            {
                _title.text = first.Title;
                _line.text = context != null ? first.Goal + "  ·  " + context : first.Goal;
            }
            else
            {
                var names = new List<string>();
                for (int i = 0; i < ids.Count; i++) names.Add(AchievementCopy.For(ids[i]).Title);
                _title.text = string.Join("、", names);
                _line.text = "同时解锁 " + _count + " 项成就" + (context != null ? "  ·  " + context : string.Empty);
            }

            // The card grows with its text, within a band that keeps it clear of the page's corners.
            float textLeft = PAD + MEDAL + (shown - 1) * MEDAL_STEP + 22f;
            float textWidth = Mathf.Max(Mathf.Ceil(_title.GetPreferredValues(_title.text).x), Mathf.Ceil(_line.GetPreferredValues(_line.text).x),
                Mathf.Ceil(_eyebrow.GetPreferredValues(_eyebrow.text).x));
            RectTransform parent = (RectTransform)_card.parent;
            float limit = parent.rect.width > 0f ? Mathf.Min(MAX_WIDTH, parent.rect.width - 96f) : MAX_WIDTH;
            // Beside the page's own heading it keeps to the right part of the screen.
            if (_x != 0f && parent.rect.width > 0f) limit = Mathf.Max(MIN_WIDTH, Mathf.Min(limit, parent.rect.width * 0.42f));
            float width = Mathf.Clamp(textLeft + textWidth + PAD + 18f, MIN_WIDTH, limit);
            _card.sizeDelta = new Vector2(width, HEIGHT);
            LibraryWidgets.TopStrip(_eyebrow.rectTransform, textLeft, PAD + 8f, 22f, 24f);
            LibraryWidgets.TopStrip(_title.rectTransform, textLeft, PAD + 8f, 46f, 42f);
            LibraryWidgets.TopStrip(_line.rectTransform, textLeft, PAD + 8f, 90f, 26f);
            _title.overflowMode = TextOverflowModes.Ellipsis;
            _line.overflowMode = TextOverflowModes.Ellipsis;
            return true;
        }

        /// <summary>Runs the card from <paramref name="from"/> seconds in to its end; <paramref name="sound"/> plays the unlock cue first.</summary>
        internal void Play(float from = 0f, bool sound = true)
        {
            if (_count == 0) return;
            _motion.TryCancel();
            from = Mathf.Clamp(from, 0f, LENGTH);
            if (sound) UiSound.Play(UiCue.Achievement);
            Apply(from);
            _motion = LMotion.Create(from, LENGTH, LENGTH - from)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() =>
                {
                    _motion = default;
                    _card.gameObject.SetActive(false);
                })
                .Bind(this, static (time, popup) => popup.Apply(time));
        }

        /// <summary>Holds the card at <paramref name="time"/> seconds in; before 0 or after its end it is hidden.</summary>
        internal void Seek(float time)
        {
            _motion.TryCancel();
            _motion = default;
            if (_count == 0) return;
            Apply(time);
        }

        internal void Hide()
        {
            _motion.TryCancel();
            _motion = default;
            _card.gameObject.SetActive(false);
        }

        public void Dispose() => _motion.TryCancel();

        private void Apply(float time)
        {
            bool shown = time >= 0f && time < LENGTH;
            if (_card.gameObject.activeSelf != shown)
            {
                _card.gameObject.SetActive(shown);
                if (shown) _card.SetAsLastSibling();
            }

            if (!shown) return;

            float arrive = HomeMotion.Span(time, 0f, 0.45f);
            float leave = HomeMotion.Span(time, LENGTH - EXIT, EXIT, Ease.InCubic);
            _group.alpha = HomeMotion.Span(time, 0f, 0.2f, Ease.OutQuad) * (1f - leave);
            float drop = Mathf.LerpUnclamped(-56f, 0f, EaseUtility.Evaluate(arrive, Ease.OutBack)) + 30f * leave;
            _card.anchoredPosition = new Vector2(_x, -_top - drop);
            float grow = Mathf.LerpUnclamped(0.9f, 1f, EaseUtility.Evaluate(arrive, Ease.OutCubic));
            _card.localScale = new Vector3(grow, grow, 1f);

            int medals = Mathf.Min(MAX_MEDALS, _count);
            for (int i = 0; i < medals; i++)
            {
                float pop = HomeMotion.Span(time, 0.12f + i * 0.09f, 0.42f);
                float scale = Mathf.Max(0f, Mathf.LerpUnclamped(0.2f, 1f, EaseUtility.Evaluate(pop, Ease.OutBack)));
                _medals[i].SetMotion(scale, HomeMotion.Span(time, 0.45f + i * 0.09f, 0.5f, Ease.InOutQuad));
                _medals[i].Root.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-24f, 0f, EaseUtility.Evaluate(pop, Ease.OutCubic)));
            }

            // A glow that flares as the medals land, then settles; a light that crosses the card once.
            float flare = HomeMotion.Span(time, 0.15f, 0.35f, Ease.OutQuad) * (1f - 0.55f * HomeMotion.Span(time, 0.5f, 0.6f, Ease.OutQuad));
            Color glow = _glow.color;
            glow.a = 0.55f * flare;
            _glow.color = glow;
            float sweep = HomeMotion.Span(time, 0.55f, 0.8f, Ease.InOutQuad);
            _shine.gameObject.SetActive(sweep > 0f && sweep < 1f);
            _shine.anchoredPosition = new Vector2(Mathf.Lerp(-80f, _card.sizeDelta.x + 80f, sweep), 0f);
        }
    }
}
