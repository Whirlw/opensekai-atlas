using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>Receives the swipes <see cref="HomeSwipe"/> picks up.</summary>
    internal interface IHomeSwipeTarget
    {
        void BeginSwipe(PointerEventData eventData);
        void Swipe(PointerEventData eventData);
        void EndSwipe(PointerEventData eventData);
    }

    /// <summary>
    /// Sits on a home card's surface (and on the empty page behind the cards) and hands its drags
    /// to the page. Being on the same object as the card's button matters: the input module then
    /// leaves the press alone when the drag starts, so the page itself decides when a moving
    /// finger stops being a tap (<see cref="HomeTheme.Motion.SWIPE_SLOP"/>) instead of the module's
    /// few pixels.
    /// </summary>
    internal sealed class HomeSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal IHomeSwipeTarget Target { get; set; }

        internal static HomeSwipe Attach(GameObject host, IHomeSwipeTarget target)
        {
            HomeSwipe swipe = host.AddComponent<HomeSwipe>();
            swipe.Target = target;
            return swipe;
        }

        public void OnBeginDrag(PointerEventData eventData) => Target?.BeginSwipe(eventData);

        public void OnDrag(PointerEventData eventData) => Target?.Swipe(eventData);

        public void OnEndDrag(PointerEventData eventData) => Target?.EndSwipe(eventData);

        /// <summary>Ends a press without a click, the way the input module does when a drag takes over.</summary>
        internal static void CancelPress(PointerEventData eventData)
        {
            if (eventData.pointerPress != null)
            {
                ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerUpHandler);
            }

            eventData.eligibleForClick = false;
            eventData.pointerPress = null;
            eventData.rawPointerPress = null;
        }
    }

    /// <summary>
    /// A row of labels on a pill with a highlight that slides under the chosen one. The home
    /// footer's page switch uses it (the highlight follows the finger during a swipe), and so do the
    /// 个人信息 page's Best 30 and history tabs. Tapping a label selects it.
    /// </summary>
    internal sealed class HomePageTabs
    {
        private const float INSET = 5f;

        private readonly RectTransform _highlight;
        private readonly TextMeshProUGUI[] _labels;
        private readonly float _segmentWidth;

        internal HomePageTabs(Transform parent, string[] names, Action<int> select, float segmentWidth = 128f, float height = 48f,
            float fontSize = 20f)
        {
            _segmentWidth = segmentWidth;
            Root = LibraryUi.CreateRect("PageTabs", parent);
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(Root, center, center, Vector2.zero,
                new Vector2(segmentWidth * names.Length + INSET * 2f, height));
            Image pill = LibraryWidgets.Panel("Pill", Root, height * 0.5f, HomeTheme.Colors.Chip);
            LibraryUi.Stretch(pill.rectTransform);
            LibraryWidgets.Outline(pill, height * 0.5f);

            float inner = height - INSET * 2f;
            Image highlight = LibraryWidgets.Panel("Highlight", Root, inner * 0.5f, LibraryTheme.Colors.ControlActive);
            _highlight = highlight.rectTransform;
            LibraryWidgets.Place(_highlight, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(INSET, 0f),
                new Vector2(segmentWidth, inner));

            _labels = new TextMeshProUGUI[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int page = i;
                Image hit = LibraryUi.CreateImage("Tab" + i, Root, Color.clear);
                hit.raycastTarget = true;
                LibraryWidgets.Place(hit.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(INSET + i * segmentWidth, 0f), new Vector2(segmentWidth, height));
                Button button = hit.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => select(page));
                TextMeshProUGUI label = LibraryWidgets.Label("Label", hit.transform, names[i], fontSize, LibraryTheme.Colors.TextMuted,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                LibraryUi.Stretch(label.rectTransform);
                _labels[i] = label;
            }
        }

        internal RectTransform Root { get; }

        internal void SetLabel(int index, string text) => _labels[index].text = text;

        /// <param name="progress">0 on the first label, 1 on the second, in between mid-swipe.</param>
        internal void Apply(float progress)
        {
            float clamped = Mathf.Clamp(progress, 0f, _labels.Length - 1);
            _highlight.anchoredPosition = new Vector2(INSET + clamped * _segmentWidth, 0f);
            for (int i = 0; i < _labels.Length; i++)
            {
                float near = 1f - Mathf.Clamp01(Mathf.Abs(clamped - i));
                _labels[i].color = Color.Lerp(LibraryTheme.Colors.TextMuted, LibraryTheme.Colors.TextPrimary, near);
            }
        }
    }

    /// <summary>
    /// A slim tab on the screen's edge that points at the other page; tapping it turns there. The
    /// one toward the second page shows on the first page and the other way round.
    /// </summary>
    internal sealed class HomeEdgeHandle
    {
        private const float WIDTH = 44f;
        private const float HEIGHT = 128f;

        private readonly CanvasGroup _group;
        private readonly Button _button;
        private readonly RectTransform _glyph;
        private readonly float _direction;

        internal HomeEdgeHandle(Transform parent, bool right, Action onClick)
        {
            Root = LibraryUi.CreateRect(right ? "NextPage" : "PreviousPage", parent);
            Vector2 anchor = new Vector2(right ? 1f : 0f, 0.5f);
            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            LibraryWidgets.Place(Root, anchor, new Vector2(0.5f, 0.5f), new Vector2((right ? -1f : 1f) * margin * 0.5f, 0f),
                new Vector2(WIDTH, HEIGHT));
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            Image pill = LibraryWidgets.Panel("Pill", Root, WIDTH * 0.5f, HomeTheme.Colors.Chip);
            pill.raycastTarget = true;
            LibraryUi.Stretch(pill.rectTransform);
            LibraryWidgets.Outline(pill, WIDTH * 0.5f);
            _button = pill.gameObject.AddComponent<Button>();
            _button.targetGraphic = pill;
            _button.transition = Selectable.Transition.None;
            _button.onClick.AddListener(() => onClick());
            LibraryPressFeedback.Attach(pill.gameObject, Root);
            Image glyph = LibraryWidgets.Icon(Root, LibraryIcon.ChevronRight, LibraryTheme.Colors.TextSecondary, 26f);
            if (!right)
            {
                glyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            }

            _glyph = glyph.rectTransform;
            _direction = right ? 1f : -1f;
        }

        internal RectTransform Root { get; }

        /// <param name="shown">0 hidden, 1 fully shown.</param>
        /// <param name="time">The idle clock: the chevron leans toward its page now and then.</param>
        internal void Apply(float shown, double time)
        {
            _group.alpha = shown;
            bool usable = shown > 0.5f;
            _group.blocksRaycasts = usable;
            _button.interactable = usable;
            float phase = (float)(time % 3d / 3d);
            float nudge = Mathf.Sin(HomeMotion.Span(phase, 0f, 0.35f) * Mathf.PI);
            _glyph.anchoredPosition = new Vector2(_direction * 5f * nudge, 0f);
        }
    }

    /// <summary>A brief, cancellable motion of the page position toward a page.</summary>
    internal struct HomePageMotion
    {
        private MotionHandle _handle;

        internal bool IsActive => _handle.IsActive();

        internal void Run(float from, float to, Action<float> apply, Action onDone)
        {
            Stop();
            float distance = Mathf.Abs(to - from);
            float duration = HomeTheme.Motion.PAGE_TURN * Mathf.Clamp(distance, 0.45f, 1f);
            _handle = LMotion.Create(from, to, duration)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => onDone?.Invoke())
                .Bind(value => apply(value));
        }

        internal void Stop()
        {
            _handle.TryCancel();
            _handle = default;
        }
    }
}
