using System;
using LitMotion;
using OpenSekai.App.Library;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One achievement in the list. It shows one level at a time; a swipe turns to the unlocked
    /// levels before it, and the stars under the medal say how many there are and which one shows.
    /// A level that can be unlocked breathes, and a tap unlocks it: the medal charges and bursts,
    /// its star lights, the reward pops, and when there is a next level its star appears and the row
    /// turns to it. The unlock is one timeline (<see cref="SeekUnlock"/> holds it anywhere).
    /// </summary>
    internal sealed class AchievementRow
    {
        internal const float HEIGHT = 172f;

        private const float SLIDE_GAP = 40f;
        private const float SLIDE = 0.34f;
        private const float RUBBER = 0.28f;

        // The unlock, in seconds.
        private const float RELEASE = 0.26f;
        private const float STAR = RELEASE + 0.16f;
        private const float STAR_POP = 0.5f;
        private const float SETTLED = RELEASE + 1.2f;
        private const float NEXT_STAR = SETTLED - 0.1f;
        private const float NEXT_STAR_GROW = 0.32f;
        private const float NEXT_SLIDE = NEXT_STAR + 0.26f;
        private const float NEXT_SLIDE_LENGTH = 0.55f;

        private readonly CanvasGroup _group;
        private readonly RectTransform _inner;
        private readonly Image _background;
        private readonly Image _accent;
        private readonly Image _outline;
        private readonly Button _hit;
        private readonly RectTransform _viewport;
        private readonly AchievementPane[] _panes = new AchievementPane[2];
        private readonly Action<AchievementProgress> _onReward;
        private readonly AchievementStars _stars;
        private readonly AchievementBurst _burst;
        private readonly Image _sweep;
        private readonly Action<AchievementRow> _onUnlock;

        private AchievementSeries _before;
        private AchievementSeries _after;
        private Action _onReleased;
        private Action _onDone;
        private bool _released;
        private float _position;
        private float _starCount;
        private float _blink;
        private float _swipeOrigin;
        private float _swipeStartX;
        private float _swipeLastX;
        private float _swipeLastTime;
        private float _swipeVelocity;
        private bool _swiping;
        private MotionHandle _slide;
        private MotionHandle _unlock;

        /// <param name="list">The list it scrolls with.</param>
        /// <param name="onUnlock">A level that can be unlocked was tapped.</param>
        /// <param name="onReward">A level's reward box was tapped.</param>
        internal AchievementRow(RectTransform content, int index, ScrollRect list, Action<AchievementRow> onUnlock,
            Action<AchievementProgress> onReward)
        {
            _onUnlock = onUnlock;
            _onReward = onReward;
            Root = LibraryUi.CreateRect("Achievement" + index, content);
            Root.anchorMin = new Vector2(0f, 1f);
            Root.anchorMax = new Vector2(1f, 1f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.sizeDelta = new Vector2(0f, HEIGHT);
            AchievementSwipe.Attach(Root.gameObject, this, list);
            _inner = LibraryUi.CreateRect("Inner", Root);
            LibraryUi.Stretch(_inner);
            _group = _inner.gameObject.AddComponent<CanvasGroup>();
            _background = LibraryWidgets.Panel("Background", _inner, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryUi.Stretch(_background.rectTransform);
            _accent = LibraryWidgets.Panel("Accent", _inner, 3f, Color.clear);
            LibraryWidgets.Place(_accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(6f, HEIGHT - 56f));
            _outline = LibraryWidgets.Shape("Outline", _inner, LibraryShapes.Ring(LibraryTheme.Radius.ROW, 2f), Color.clear);
            LibraryUi.Stretch(_outline.rectTransform);

            // Under the panes, so a reward box takes its own taps.
            Image hit = LibraryWidgets.Panel("Hit", _inner, LibraryTheme.Radius.ROW, Color.clear);
            hit.raycastTarget = true;
            LibraryUi.Stretch(hit.rectTransform);
            _hit = hit.gameObject.AddComponent<Button>();
            _hit.targetGraphic = hit;
            _hit.transition = Selectable.Transition.None;
            _hit.onClick.AddListener(Tap);
            LibraryPressFeedback.Attach(hit.gameObject, _inner, 0.985f);

            _viewport = LibraryUi.CreateRect("Levels", _inner);
            LibraryUi.Stretch(_viewport);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _panes[0] = new AchievementPane(_viewport, "Level0", onReward);
            _sweep = LibraryUi.CreateImage("Sweep", _viewport, Color.white);
            _sweep.raycastTarget = false;
            LibraryWidgets.Place(_sweep.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, HEIGHT * 2f));
            _sweep.rectTransform.localEulerAngles = new Vector3(0f, 0f, 22f);
            _sweep.enabled = false;

            Vector2 left = new Vector2(0f, 0.5f);
            Vector2 center = new Vector2(0.5f, 0.5f);
            float medal = AchievementPane.LEFT + AchievementPane.MEDAL * 0.5f;
            _stars = new AchievementStars(_inner, "Stars");
            LibraryWidgets.Place(_stars.Root, left, center, new Vector2(medal, AchievementPane.MEDAL_LIFT - AchievementPane.MEDAL * 0.5f - 22f),
                _stars.Root.sizeDelta);
            _burst = new AchievementBurst(_inner, "Burst", AchievementPane.MEDAL);
            LibraryWidgets.Place(_burst.Root, left, center, new Vector2(medal, 0f), _burst.Root.sizeDelta);
        }

        internal RectTransform Root { get; }

        internal AchievementSeries Series { get; private set; }

        /// <summary>The level on show.</summary>
        internal int Step => Series == null ? 0 : Mathf.Clamp(Mathf.RoundToInt(_position), 0, Series.OpenCount - 1);

        /// <summary>True from the tap until the row rests again.</summary>
        internal bool IsUnlocking { get; private set; }

        internal int StarCount => _stars.Root.gameObject.activeSelf ? _stars.Count : 0;

        private bool HasNext => _after.OpenCount > _before.OpenCount;

        private float UnlockLength => HasNext ? NEXT_SLIDE + NEXT_SLIDE_LENGTH : SETTLED;

        /// <summary>Shows <paramref name="series"/> at the level to work on, and drops any motion.</summary>
        internal void Bind(AchievementSeries series)
        {
            Stop();
            Series = series;
            _position = series.CurrentStep;
            _starCount = series.OpenCount;
            _blink = 1f;
            BindStars(series);
            Draw();
        }

        internal void SetTop(float top) => Root.anchoredPosition = new Vector2(0f, -top);

        internal float Top => -Root.anchoredPosition.y;

        /// <param name="shown">0 to 1: the row slides in from the right and fades in.</param>
        internal void Apply(float shown)
        {
            _group.alpha = shown;
            _inner.anchoredPosition = new Vector2(32f * (1f - shown), 0f);
        }

        /// <param name="wave">0 to 1 and back, over and over.</param>
        internal void Pulse(float wave)
        {
            if (Series == null || Series.State != AchievementState.Claimable || IsUnlocking) return;
            _blink = wave;
            foreach (AchievementPane pane in _panes) pane?.Pulse(wave);
            _stars.Apply(_position, _starCount, _blink);
            _outline.color = Outline(wave);
        }

        /// <summary>Turns to <paramref name="step"/>.</summary>
        internal void SlideTo(int step)
        {
            if (IsUnlocking) return;
            step = Mathf.Clamp(step, 0, Series.OpenCount - 1);
            if (step != Step) UiSound.Play(UiCue.Select);
            Settle(step);
        }

        /// <summary>
        /// Plays the unlock of the level on show. <paramref name="after"/> is the achievement with it
        /// unlocked; <paramref name="onReleased"/> runs as the medal bursts, <paramref name="onDone"/> once the row rests.
        /// </summary>
        internal void Unlock(AchievementSeries after, Action onReleased, Action onDone)
        {
            BeginUnlock(after, onReleased, onDone);
            float length = UnlockLength;
            ApplyUnlock(0f);
            _unlock = LMotion.Create(0f, length, length)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(EndUnlock)
                .Bind(this, static (time, row) => row.ApplyUnlock(time));
        }

        /// <summary>Holds the unlock at <paramref name="time"/> seconds in, for screenshots.</summary>
        internal void SeekUnlock(AchievementSeries after, float time)
        {
            if (!IsUnlocking) BeginUnlock(after, null, null);
            ApplyUnlock(time);
        }

        // ---------------------------------------------------------------- unlocking

        private void Tap()
        {
            if (IsUnlocking || Series.State != AchievementState.Claimable) return;
            if (Step != Series.UnlockedCount)
            {
                SlideTo(Series.UnlockedCount);
                return;
            }

            _onUnlock(this);
        }

        private void BeginUnlock(AchievementSeries after, Action onReleased, Action onDone)
        {
            _slide.TryCancel();
            _swiping = false;
            _before = Series;
            _after = after;
            _onReleased = onReleased;
            _onDone = onDone;
            _released = false;
            IsUnlocking = true;
            _position = _before.UnlockedCount;
            _blink = 1f;
            _hit.interactable = false;
            _burst.Bind(AchievementCopy.For(_before.Steps[_before.UnlockedCount]).Tint);
            _burst.Root.anchoredPosition = new Vector2(_burst.Root.anchoredPosition.x, _before.Steps.Count > 1 ? AchievementPane.MEDAL_LIFT : 0f);
            // The burst reaches over the rows around it.
            Root.SetAsLastSibling();
        }

        private void ApplyUnlock(float time)
        {
            int step = _before.UnlockedCount;
            bool released = time >= RELEASE;
            Series = released ? _after : _before;
            if (HasNext)
            {
                _starCount = _before.OpenCount + HomeMotion.Span(time, NEXT_STAR, NEXT_STAR_GROW, Ease.OutBack);
                _position = step + HomeMotion.Span(time, NEXT_SLIDE, NEXT_SLIDE_LENGTH, Ease.InOutCubic);
            }

            BindStars(time >= STAR ? _after : _before);
            Draw();
            AchievementPane pane = _panes[step & 1];
            if (released) pane.Release(time - RELEASE);
            else pane.Charge(HomeMotion.Span(time, 0f, RELEASE, Ease.OutQuad));

            float tint = HomeMotion.Span(time, RELEASE, 0.4f, Ease.OutQuad);
            AchievementProgress unlocked = _after.Steps[step];
            Color32 color = AchievementCopy.For(unlocked).Tint;
            if (_position <= step)
            {
                _background.color = Color.Lerp(Background(_before.Steps[step]), Background(unlocked), tint);
                _accent.color = HomeTheme.Colors.WithAlpha(color, (byte)Mathf.RoundToInt(255f * tint));
            }

            Color outline = color;
            outline.a = 0.9f * (1f - HomeMotion.Span(time, RELEASE, 0.9f, Ease.OutQuad));
            _outline.color = HasNext && released ? Color.Lerp(outline, Outline(1f), HomeMotion.Span(time, NEXT_SLIDE, NEXT_SLIDE_LENGTH)) : outline;
            float pop = HomeMotion.Span(time, STAR, STAR_POP);
            _stars.Apply(_position, _starCount, 1f, step, 0.7f * Mathf.Sin(pop * Mathf.PI) * (1f - pop * 0.4f));
            _burst.Apply(time - RELEASE);
            float sweep = HomeMotion.Span(time, RELEASE + 0.05f, 0.75f, Ease.InOutQuad);
            _sweep.enabled = sweep > 0f && sweep < 1f;
            if (_sweep.enabled)
            {
                _sweep.color = HomeTheme.Colors.WithAlpha(Color.Lerp(color, Color.white, 0.5f), 24);
                _sweep.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-120f, Width + 120f, sweep), 0f);
            }

            if (!released || _released) return;
            _released = true;
            _onReleased?.Invoke();
        }

        private void EndUnlock()
        {
            AchievementSeries after = _after;
            Action done = _onDone;
            _unlock = default;
            Bind(after);
            done?.Invoke();
        }

        /// <summary>Drops any motion; the row is to be bound again before it shows.</summary>
        internal void Stop()
        {
            _slide.TryCancel();
            _slide = default;
            _unlock.TryCancel();
            _unlock = default;
            _swiping = false;
            IsUnlocking = false;
            _before = null;
            _after = null;
            _onReleased = null;
            _onDone = null;
            _sweep.enabled = false;
            _burst.Apply(-1f);
            foreach (AchievementPane pane in _panes) pane?.Rest();
        }

        // ---------------------------------------------------------------- swiping

        /// <summary>False when there is nothing to turn to; the drag is the list's then.</summary>
        internal bool BeginSwipe(PointerEventData eventData)
        {
            if (IsUnlocking || Series == null || Series.OpenCount < 2 || eventData.button != PointerEventData.InputButton.Left) return false;
            _slide.TryCancel();
            _swiping = true;
            _swipeOrigin = _position;
            _swipeStartX = LocalX(eventData);
            _swipeLastX = _swipeStartX;
            _swipeLastTime = Time.unscaledTime;
            _swipeVelocity = 0f;
            return true;
        }

        internal void Swipe(PointerEventData eventData)
        {
            if (!_swiping) return;
            float x = LocalX(eventData);
            _position = Rubber(_swipeOrigin - (x - _swipeStartX) / (Width + SLIDE_GAP));
            Draw();
            float now = Time.unscaledTime;
            float elapsed = now - _swipeLastTime;
            if (elapsed > 0.0001f) _swipeVelocity = Mathf.Lerp(_swipeVelocity, (x - _swipeLastX) / elapsed, 0.6f);
            _swipeLastX = x;
            _swipeLastTime = now;
        }

        internal void EndSwipe()
        {
            if (!_swiping) return;
            _swiping = false;
            int from = Mathf.Clamp(Mathf.RoundToInt(_swipeOrigin), 0, Series.OpenCount - 1);
            float moved = _position - from;
            // A finger that stopped before lifting is not a flick.
            float velocity = Time.unscaledTime - _swipeLastTime > 0.1f ? 0f : _swipeVelocity;
            bool far = Mathf.Abs(moved) > HomeTheme.Motion.PAGE_TURN_DISTANCE;
            bool flicked = Mathf.Abs(velocity) > HomeTheme.Motion.PAGE_FLICK_SPEED && Mathf.Sign(-velocity) == Mathf.Sign(moved);
            int target = Mathf.Clamp(far || flicked ? from + (moved > 0f ? 1 : -1) : from, 0, Series.OpenCount - 1);
            if (target != from) UiSound.Play(UiCue.Select);
            Settle(target);
        }

        private void Settle(int step)
        {
            _slide.TryCancel();
            _slide = LMotion.Create(_position, step, SLIDE)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(this, static (position, row) =>
                {
                    row._position = position;
                    row.Draw();
                });
        }

        private float Rubber(float position)
        {
            float last = Series.OpenCount - 1;
            if (position < 0f) return position * RUBBER;
            return position > last ? last + (position - last) * RUBBER : position;
        }

        private float LocalX(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return local.x;
        }

        // ---------------------------------------------------------------- drawing

        private float Width => Mathf.Max(1f, _viewport.rect.width);

        /// <summary>Puts the level at <see cref="_position"/> in place, and the next one beside it while they slide.</summary>
        private void Draw()
        {
            int last = Series.OpenCount - 1;
            float clamped = Mathf.Clamp(_position, 0f, last);
            int low = Mathf.Min(Mathf.FloorToInt(clamped), last);
            float between = clamped - low;
            Show(low);
            if (between > 0f) Show(low + 1);
            else if (_panes[(low + 1) & 1] != null) _panes[(low + 1) & 1].Root.gameObject.SetActive(false);

            AchievementProgress here = Series.Steps[low];
            AchievementProgress there = between > 0f ? Series.Steps[low + 1] : here;
            _background.color = Color.Lerp(Background(here), Background(there), between);
            // Out of the way while a medal slides past it.
            Color accent = between < 0.5f ? Accent(here) : Accent(there);
            accent.a *= Mathf.Abs(1f - 2f * between);
            _accent.color = accent;
            _outline.color = Outline(_blink);
            _hit.interactable = Series.State == AchievementState.Claimable && !IsUnlocking;
            _stars.Apply(_position, _starCount, _blink);
        }

        private void Show(int step)
        {
            int slot = step & 1;
            if (_panes[slot] == null)
            {
                _panes[slot] = new AchievementPane(_viewport, "Level" + slot, _onReward);
                _sweep.transform.SetAsLastSibling();
            }

            AchievementPane pane = _panes[slot];
            pane.Root.gameObject.SetActive(true);
            if (pane.Progress != Series.Steps[step]) pane.Bind(Series.Steps[step], Series.Steps.Count > 1);
            pane.Root.anchoredPosition = new Vector2((step - _position) * (Width + SLIDE_GAP), 0f);
        }

        private void BindStars(AchievementSeries series)
        {
            _stars.Root.gameObject.SetActive(series.Steps.Count > 1);
            _stars.Bind(AchievementCopy.For(series.Steps[0]).Tint, series.UnlockedCount, series.State == AchievementState.Claimable);
        }

        private Color Outline(float wave)
        {
            if (Series.State != AchievementState.Claimable) return Color.clear;
            Color color = AchievementCopy.For(Series.Steps[Series.UnlockedCount]).Tint;
            color.a = Mathf.Lerp(0.3f, 0.95f, wave);
            return color;
        }

        private static Color Background(AchievementProgress progress)
        {
            if (progress.State == AchievementState.InProgress) return LibraryTheme.Colors.Control;
            return HomeTheme.Colors.WithAlpha(AchievementCopy.For(progress).Tint, (byte)(progress.State == AchievementState.Unlocked ? 22 : 34));
        }

        private static Color Accent(AchievementProgress progress)
        {
            Color color = AchievementCopy.For(progress).Tint;
            color.a = progress.State == AchievementState.Unlocked ? 1f : 0f;
            return color;
        }
    }
}
