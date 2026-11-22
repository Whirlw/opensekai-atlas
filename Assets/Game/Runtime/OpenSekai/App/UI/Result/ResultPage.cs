using System;
using System.Collections.Generic;
using System.IO;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The full-screen result page the library and the server part both open after a run. It
    /// covers its owner's page, so closing it leaves that page (the list position, the server's
    /// level page) exactly as the run left it. It reads one finished <see cref="PlayResult"/> and
    /// its <see cref="RankingSettlement"/>, and never changes either: the score, the award mark,
    /// the points the run earned and the Ranking Score come in on one timeline that a tap anywhere
    /// jumps to its end. A tier upgrade plays once per play id, however often the run is reopened,
    /// and so does the card for the achievements the run unlocked, which drops in after it.
    /// </summary>
    internal sealed partial class ResultPage : MonoBehaviour
    {
        // Timeline, in seconds from opening.
        private const float ENTER_DURATION = 0.4f;
        private const float ENTER_RISE = 28f;
        private const float SCORE_START = 0.2f;
        private const float SCORE_DURATION = 0.8f;
        private const float AWARD_AT = 0.95f;
        private const float AWARD_DURATION = 0.32f;
        private const float POINTS_START = 1.2f;
        private const float POINTS_DURATION = 0.5f;
        private const float TOTAL_START = 1.45f;
        private const float TOTAL_DURATION = 0.75f;
        private const float POP_DURATION = 0.3f;
        private const float DELTA_AT = 2.15f;
        private const float TIER_AT = 2.3f;
        private const float TIER_CHARGE = 0.28f;
        private const float TIER_SWAP = TIER_AT + TIER_CHARGE;
        private const float TIER_BURST = 0.6f;
        private const float SETTLED = 2.5f;
        private const float CELEBRATED = TIER_SWAP + TIER_BURST + 0.1f;
        private const float HINT_FADE = 0.35f;
        private const float UNLOCK_DELAY = 0.15f;

        /// <summary>Play ids whose tier upgrade has been shown in this run of the app.</summary>
        private static readonly HashSet<string> _celebrated = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Play ids whose unlocked achievements have been announced in this run of the app.</summary>
        private static readonly HashSet<string> _announced = new HashSet<string>(StringComparer.Ordinal);

        private Action<PlayResult> _onRetry;
        private PlayResult _current;
        private RankingSettlement _ranking;
        private Color32 _accent = LibraryTheme.Colors.DefaultAccent;
        private MotionHandle _timeline;
        private float _time;
        private float _end = SETTLED;
        private bool _celebrateTier;
        private bool _announceUnlocks;
        private float _unlockAt = SETTLED;
        private bool _silent;
        private long _shownScore;
        private long _shownPoints;
        private long _shownTotal;
        private int _shownTier;

        /// <summary>The back button or the back key closed the page.</summary>
        internal Action OnClose { private get; set; }

        internal bool IsOpen => gameObject.activeSelf;

        /// <summary>The run on the page; null before the first one.</summary>
        internal PlayResult Current => _current;

        /// <summary>What the run did to the ranking; null when it could not be read.</summary>
        internal RankingSettlement Ranking => _ranking;

        /// <summary>Seconds the opening timeline runs.</summary>
        internal float Duration => _end;

        /// <param name="parent">The owner's full-screen layer; the page stretches over it.</param>
        /// <param name="onRetry">再玩一次: start the same chart in the same mode.</param>
        /// <param name="backLabel">Names where the back button leads.</param>
        internal static ResultPage Create(Transform parent, Action<PlayResult> onRetry, string backLabel)
        {
            RectTransform root = LibraryUi.CreateRect("ResultPage", parent);
            LibraryUi.Stretch(root);
            ResultPage page = root.gameObject.AddComponent<ResultPage>();
            page._onRetry = onRetry;
            page.Build(backLabel);
            root.gameObject.SetActive(false);
            return page;
        }

        /// <param name="cover">The package's jacket, when one is already decoded.</param>
        /// <param name="accent">Follows the jacket: rank, glow and 再玩一次.</param>
        internal void Show(PlayResult result, Sprite cover, Color32 accent)
        {
            if (result == null)
            {
                Hide();
                return;
            }

            _current = result;
            _accent = accent;
            _ranking = RankingFor(result);
            _celebrateTier = ShouldCelebrate(_ranking);
            _announceUnlocks = ShouldAnnounce(_ranking);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SetCover(cover);
            Bind(result);
            BindRanking();
            BindUnlocks();
            ApplyAccent();
            if (!result.HasScore)
            {
                UiSound.Play(UiCue.Error);
            }

            Play(0f);
        }

        /// <summary>Fills in the jacket and its accent once they are decoded, usually just after opening.</summary>
        internal void SetArtwork(Sprite cover, Color32 accent)
        {
            if (!IsOpen || _current == null)
            {
                return;
            }

            SetCover(cover);
            _accent = accent;
            ApplyAccent();
        }

        /// <summary>Jumps the opening to its final state, silently. Nothing happens once it has finished.</summary>
        internal void Skip()
        {
            if (_timeline.IsActive())
            {
                Seek(_end);
                ContinueUnlocks();
            }
        }

        /// <summary>Shows the page as it is <paramref name="time"/> seconds after opening, and stops there.</summary>
        internal void Seek(float time)
        {
            _timeline.TryCancel();
            _timeline = default;
            if (_current == null)
            {
                return;
            }

            _silent = true;
            Apply(Mathf.Clamp(time, 0f, _end));
            _silent = false;
        }

        /// <summary>The Android back key: closes the page the way its back button does.</summary>
        internal void Dismiss()
        {
            UiSound.Play(UiCue.Back);
            Close();
        }

        internal void Hide()
        {
            _timeline.TryCancel();
            _timeline = default;
            _unlock.Hide();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // Taken off screen mid-opening (the owner's page went away): settle, so it comes back finished.
            if (_timeline.IsActive())
            {
                Seek(_end);
            }
        }

        private void OnDestroy()
        {
            _timeline.TryCancel();
            _unlock?.Dispose();
        }

        /// <summary>Lets every tier upgrade play again; a new run of the app starts this way.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ForgetCelebrations()
        {
            _celebrated.Clear();
            _announced.Clear();
        }

        private static RankingSettlement RankingFor(PlayResult result)
        {
            try
            {
                return CustomMusicScorePlayHistoryStorage.ForDisplay(result);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                || exception is IOException || exception is UnauthorizedAccessException)
            {
                // A run without a history recorder, or a history file that cannot be read: the page
                // still opens and says the run was not recorded.
                Debug.LogException(exception);
                return null;
            }
        }

        private static bool ShouldCelebrate(RankingSettlement ranking)
        {
            return ranking != null && ranking.TierUp && !string.IsNullOrEmpty(ranking.PlayId)
                && _celebrated.Add(ranking.PlayId);
        }

        /// <summary>True the first time a saved run with unlocks is shown; a reopened run does not announce them again.</summary>
        private static bool ShouldAnnounce(RankingSettlement ranking)
        {
            return ranking != null && ranking.SaveState == HistorySaveState.Saved && ranking.UnlockedAchievementIds.Count > 0
                && !string.IsNullOrEmpty(ranking.PlayId) && _announced.Add(ranking.PlayId);
        }

        private void BindUnlocks()
        {
            _unlock.Hide();
            if (_announceUnlocks && !_unlock.Bind(_ranking.UnlockedAchievementIds, null)) _announceUnlocks = false;
        }

        /// <summary>Once the opening has ended, the unlock card holds a while on its own, then lifts away.</summary>
        private void ContinueUnlocks()
        {
            if (_announceUnlocks && IsOpen) _unlock.Play(_end - _unlockAt, false);
        }

        private void Close()
        {
            Hide();
            OnClose?.Invoke();
        }

        private void Retry()
        {
            PlayResult result = _current;
            Hide();
            _onRetry?.Invoke(result);
        }

        /// <summary>重新保存: writes the run again and, once it is in, plays what it earned.</summary>
        private void RetrySave()
        {
            if (_ranking == null || string.IsNullOrEmpty(_ranking.PlayId))
            {
                return;
            }

            RankingSettlement saved = null;
            try
            {
                saved = CustomMusicScorePlayHistoryStorage.RetrySave(_ranking.PlayId);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogException(exception);
            }

            if (saved == null || saved.SaveState != HistorySaveState.Saved)
            {
                UiSound.Play(UiCue.Error);
                _status.text = "仍未保存，请稍后再试";
                LayoutStatus();
                return;
            }

            UiSound.Play(UiCue.Success);
            _ranking = saved;
            _celebrateTier = ShouldCelebrate(saved);
            _announceUnlocks = ShouldAnnounce(saved);
            BindRanking();
            BindUnlocks();
            ApplyAccent();
            Play(POINTS_START);
        }

        private void Play(float from)
        {
            _timeline.TryCancel();
            float settled = _celebrateTier ? CELEBRATED : SETTLED;
            _unlockAt = settled + UNLOCK_DELAY;
            _end = _announceUnlocks ? _unlockAt + AchievementUnlockPopup.ENTER : settled;
            _time = from;
            Apply(from);
            _timeline = LMotion.Create(from, _end, _end - from)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(ContinueUnlocks)
                .Bind(this, static (time, page) => page.Apply(time));
        }

        /// <summary>Puts every animated part where it is <paramref name="time"/> seconds in; the same time always draws the same page.</summary>
        private void Apply(float time)
        {
            float previous = _time;
            _time = time;
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Apply(time);
            }

            if (_current.HasScore)
            {
                float count = HomeMotion.Span(time, SCORE_START, SCORE_DURATION, Ease.OutCubic);
                ShowNumber(_score, ref _shownScore, Scale(_current.Score, count));
                float stamp = HomeMotion.Span(time, AWARD_AT, AWARD_DURATION);
                float grow = Mathf.LerpUnclamped(1.6f, 1f, EaseUtility.Evaluate(stamp, Ease.OutBack));
                _award.localScale = new Vector3(grow, grow, 1f);
                _awardGroup.alpha = Mathf.Clamp01(stamp * 3f);
                Cue(previous, time, AWARD_AT, AwardCue(_current));
            }

            ApplyRanking(previous, time);
            if (_announceUnlocks)
            {
                // After the tier upgrade: the card drops in as the page settles.
                _unlock.Seek(time - _unlockAt);
                Cue(previous, time, _unlockAt, UiCue.Achievement);
            }

            // The hint sits where the unlock card lands, so it is gone before the card arrives.
            float hintEnd = _announceUnlocks ? _unlockAt : _end;
            _skipHint.alpha = 1f - HomeMotion.Span(time, hintEnd - HINT_FADE, HINT_FADE);
        }

        private void ApplyRanking(float previous, float time)
        {
            if (_ranking == null)
            {
                return;
            }

            bool counted = _ranking.Eligibility == RankingEligibility.Eligible;
            if (counted)
            {
                float points = HomeMotion.Span(time, POINTS_START, POINTS_DURATION, Ease.OutCubic);
                ShowNumber(_points, ref _shownPoints, Scale(_ranking.Points, points));
                _recordTag.Pop(HomeMotion.Span(time, POINTS_START + POINTS_DURATION, POP_DURATION));
            }

            float total = HomeMotion.Span(time, TOTAL_START, TOTAL_DURATION, Ease.OutCubic);
            ShowNumber(_total, ref _shownTotal, _ranking.Before.Total + Scale(_ranking.Delta, total));
            _deltaTag.Pop(HomeMotion.Span(time, DELTA_AT, POP_DURATION));

            if (!_celebrateTier)
            {
                ShowTier(_ranking.After.TierId);
                _badge.SetMotion(1f, 0f, 0f);
                return;
            }

            bool swapped = time >= TIER_SWAP;
            ShowTier(swapped ? _ranking.After.TierId : _ranking.Before.TierId);
            if (swapped)
            {
                float settle = EaseUtility.Evaluate(HomeMotion.Span(time, TIER_SWAP, 0.45f), Ease.OutBack);
                _badge.SetMotion(Mathf.LerpUnclamped(1.3f, 1f, settle), 1f - HomeMotion.Span(time, TIER_SWAP, 0.35f),
                    HomeMotion.Span(time, TIER_SWAP, TIER_BURST, Ease.OutCubic));
            }
            else
            {
                float charge = HomeMotion.Span(time, TIER_AT, TIER_CHARGE, Ease.InQuad);
                _badge.SetMotion(1f + 0.12f * charge, charge, 0f);
            }

            _tierUpTag.Pop(HomeMotion.Span(time, TIER_SWAP + 0.12f, POP_DURATION));
            Cue(previous, time, TIER_SWAP, UiCue.Achievement);
        }

        private void ShowTier(int tier)
        {
            if (tier == _shownTier)
            {
                return;
            }

            _shownTier = tier;
            Color32 color = RankingTiers.Color(tier);
            _badge.SetTier(tier);
            _tierName.text = RankingTiers.Name(tier);
            _tierName.color = color;
            _tierLatin.text = RankingTiers.LatinName(tier);
            float nameWidth = Mathf.Ceil(_tierName.GetPreferredValues(_tierName.text).x);
            RectTransform latin = _tierLatin.rectTransform;
            latin.anchoredPosition = new Vector2(_tierName.rectTransform.anchoredPosition.x + nameWidth + 12f, latin.anchoredPosition.y);
            float latinWidth = Mathf.Ceil(_tierLatin.GetPreferredValues(_tierLatin.text).x);
            _tierUpTag.Layout(latin.anchoredPosition.x + latinWidth + 16f);
            _glowSecondary.color = new Color32(color.r, color.g, color.b, 30);
        }

        private void Cue(float previous, float time, float at, UiCue cue)
        {
            if (!_silent && cue != UiCue.None && previous < at && time >= at)
            {
                UiSound.Play(cue);
            }
        }

        private static UiCue AwardCue(PlayResult result)
        {
            if (result.IsAuto) return UiCue.None;
            switch (result.Award)
            {
                case ClearAward.AllPerfect:
                case ClearAward.FullCombo:
                    return UiCue.Achievement;
                case ClearAward.Clear:
                    return UiCue.Success;
                default:
                    return UiCue.Open;
            }
        }

        private static long Scale(long value, float progress) => (long)Math.Round(value * (double)progress);

        /// <summary>Sets a counter's text only when the shown number changes.</summary>
        private static void ShowNumber(TextMeshProUGUI label, ref long shown, long value)
        {
            if (value == shown)
            {
                return;
            }

            shown = value;
            label.text = ResultCopy.Number(value);
        }

        /// <summary>A part of the page that fades and rises in, a little after the one before it.</summary>
        private readonly struct Block
        {
            private readonly CanvasGroup _group;
            private readonly RectTransform _rect;
            private readonly Vector2 _home;
            private readonly float _delay;

            internal Block(RectTransform rect, float delay)
            {
                _rect = rect;
                _group = rect.gameObject.AddComponent<CanvasGroup>();
                _home = rect.anchoredPosition;
                _delay = delay;
            }

            internal void Apply(float time)
            {
                float progress = HomeMotion.Span(time, _delay, ENTER_DURATION, Ease.OutCubic);
                _group.alpha = progress;
                _rect.anchoredPosition = _home + new Vector2(0f, -ENTER_RISE * (1f - progress));
            }
        }

        /// <summary>Small rounded label whose width follows its text; tags pop in on the timeline.</summary>
        private sealed class Chip
        {
            private const float PADDING_X = 14f;
            private readonly Image _background;
            private readonly TextMeshProUGUI _label;
            private readonly float _top;
            private readonly float _height;

            internal Chip(RectTransform parent, string name, float top, float height, float fontSize = LibraryTheme.FontSize.CAPTION)
            {
                _top = top;
                _height = height;
                _background = LibraryWidgets.Panel(name, parent, LibraryTheme.Radius.CHIP, LibraryTheme.Colors.Hairline);
                _label = LibraryWidgets.Label(
                    "Label", _background.transform, string.Empty, fontSize, LibraryTheme.Colors.TextSecondary,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                LibraryWidgets.Inset(_label.rectTransform, PADDING_X, 0f, PADDING_X, 0f);
            }

            internal RectTransform Rect => _background.rectTransform;

            internal void Set(string text, Color32 color)
            {
                _label.text = text;
                _label.color = color;
                _background.color = new Color32(color.r, color.g, color.b, 34);
            }

            internal void SetShown(bool shown) => _background.gameObject.SetActive(shown);

            /// <returns>The x where the chip ends.</returns>
            internal float Layout(float x)
            {
                float width = Mathf.Ceil(_label.GetPreferredValues(_label.text).x) + PADDING_X * 2f;
                LibraryWidgets.Place(_background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(x, -_top - _height * 0.5f), new Vector2(width, _height));
                return x + width;
            }

            /// <summary>Springs the tag in: nothing at 0, full size by 1.</summary>
            internal void Pop(float progress)
            {
                float scale = Mathf.Max(0f, EaseUtility.Evaluate(progress, Ease.OutBack));
                _background.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
