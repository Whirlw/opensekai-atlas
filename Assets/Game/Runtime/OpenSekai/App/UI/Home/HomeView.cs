using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Which card grows over the page when the home page hands over. The first three are on the
    /// first page, the other three on the second; the order is each page's hero card, then the
    /// top and the bottom card on its right.
    /// </summary>
    internal enum HomeDestination
    {
        Library,
        Server,
        Packs,
        Profile,
        Dan,
        Achievements
    }

    /// <summary>
    /// The home page: the brand in the header, two pages of three cards side by side, and a
    /// footer. The first page is the library card beside the 服务器 and 资源包 cards; swiping left
    /// (or the footer's tabs, or the arrow on the screen's edge) brings in the second page with 个人信息
    /// beside 段位 and 成就. A finger has to move <see cref="HomeTheme.Motion.SWIPE_SLOP"/> sideways
    /// before the touch stops being a tap on the card under it. The entrance, the hand-over to a
    /// part (its card grows over the page) and the return (the card shrinks back) are pure
    /// functions of time, so the splash can drive the entrance from its own timeline and a
    /// screenshot can stop at any frame.
    /// </summary>
    internal sealed class HomeView : IDisposable, IHomeSwipeTarget
    {
        internal const int PAGE_COUNT = 2;
        private const int CARDS_PER_PAGE = 3;
        private const int NO_POINTER = int.MinValue;
        private const float RUBBER = 0.3f;
        private static readonly string[] _pageNames = { "游玩", "个人" };

        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly CanvasGroup _header;
        private readonly RectTransform _headerRect;
        private readonly CanvasGroup _footer;
        private readonly CanvasGroup _chrome;
        private readonly RectTransform[] _pages = new RectTransform[PAGE_COUNT];
        private readonly HomeSlot[] _slots = new HomeSlot[PAGE_COUNT * CARDS_PER_PAGE];
        private readonly HomeLibraryCard _library;
        private readonly HomeFeatureCard _server;
        private readonly HomeFeatureCard _packs;
        private readonly HomeProfileCard _profile;
        private readonly HomeFeatureCard _dan;
        private readonly HomeFeatureCard _achievements;
        private readonly HomePageTabs _tabs;
        private readonly HomeEdgeHandle _next;
        private readonly HomeEdgeHandle _previous;
        private readonly UiToast _toast;
        private MotionHandle _transition;
        private HomePageMotion _pageMotion;
        private float _pageProgress;
        private int _page;
        private double _time;

        private int _swipePointer = NO_POINTER;
        private bool _swiping;
        private float _swipeOrigin;
        private float _swipeStartX;
        private float _swipeLastX;
        private float _swipeLastTime;
        private float _swipeVelocity;

        /// <summary>Last value <see cref="ApplyExpansion"/> applied; NaN before the first.</summary>
        private float _expansion = float.NaN;

        /// <param name="parent">Full-screen rect the page fills.</param>
        /// <param name="openLibrary">The library card was tapped.</param>
        /// <param name="openAbout">The header's about button was tapped.</param>
        /// <param name="comingSoon">A card for an unbuilt part was tapped; gets the part's name.</param>
        /// <param name="openProfile">The 个人信息 card was tapped.</param>
        internal HomeView(RectTransform parent, Action openLibrary, Action openAbout, Action openSettings, Action<string> comingSoon,
            Action openServer = null, Action openPacks = null, Action openProfile = null)
        {
            _root = LibraryUi.CreateRect("Home", parent);
            LibraryUi.Stretch(_root);
            _group = _root.gameObject.AddComponent<CanvasGroup>();

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;

            // Built back to front: header and footer first, then the pages, so a growing card
            // covers them; the edge handles and the toast go on top.
            _headerRect = LibraryUi.CreateRect("Header", _root);
            LibraryWidgets.TopStrip(_headerRect, margin, margin, 0f, HomeTheme.Layout.HEADER_HEIGHT);
            _header = _headerRect.gameObject.AddComponent<CanvasGroup>();
            BuildHeader(_headerRect, openAbout, openSettings);

            RectTransform footer = LibraryUi.CreateRect("Footer", _root);
            LibraryWidgets.BottomStrip(footer, margin, margin, 22f, 48f);
            _footer = footer.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI tagline = LibraryWidgets.Label(
                "Tagline", footer, "Open Sekai Atlas  ·  开源下落式音游播放器", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryUi.Stretch(tagline.rectTransform);
            TextMeshProUGUI licenses = LibraryWidgets.Label(
                "PlayFeatures", footer, "社区谱面  ·  离线打歌", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.Stretch(licenses.rectTransform);
            _tabs = new HomePageTabs(footer, _pageNames, page => TurnTo(page, true));

            // A swipe that starts between the cards turns the page too.
            Image swipeArea = LibraryUi.CreateImage("SwipeArea", _root, Color.clear);
            swipeArea.raycastTarget = true;
            LibraryWidgets.Inset(swipeArea.rectTransform, 0f, HomeTheme.Layout.FOOTER_HEIGHT, 0f, HomeTheme.Layout.HEADER_HEIGHT);
            HomeSwipe.Attach(swipeArea.gameObject, this);

            RectTransform track = LibraryUi.CreateRect("Pages", _root);
            LibraryUi.Stretch(track);
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                _pages[i] = LibraryUi.CreateRect("Page" + i, track);
            }

            _server = new HomeFeatureCard(Slot(0, 1).Rect, "ServerCard", "SERVER", "服务器", "浏览社区谱面 · 下载后自动导入",
                HomeTheme.Colors.Periwinkle, root => new HomeGlobeArt(root), () => openServer?.Invoke(), available: openServer != null);
            _packs = new HomeFeatureCard(Slot(0, 2).Rect, "PacksCard", "RESOURCE PACKS", "资源包", "音符皮肤 · 打击音效 · 判定与界面",
                HomeTheme.Colors.Rose, root => new HomeStackArt(root),
                () =>
                {
                    if (openPacks != null) openPacks();
                    else comingSoon?.Invoke("资源包");
                },
                available: openPacks != null, status: openPacks != null ? "管理资源" : null);
            _library = new HomeLibraryCard(Slot(0, 0).Rect, openLibrary);

            _dan = new HomeFeatureCard(Slot(1, 1).Rect, "DanCard", "DAN", "段位", "多曲连打 · 生命延续 · 段位牌",
                HomeTheme.Colors.Violet, root => new HomePlateArt(root), () => comingSoon?.Invoke("段位"));
            _achievements = new HomeFeatureCard(Slot(1, 2).Rect, "AchievementsCard", "ACHIEVEMENTS", "成就", "累计击打 · 谱面达成 · 段位通关",
                HomeTheme.Colors.Gold, root => new HomeMedalArt(root), () => comingSoon?.Invoke("成就"));
            _profile = new HomeProfileCard(Slot(1, 0).Rect, () => openProfile?.Invoke());

            Bind(0, 0, _library);
            Bind(0, 1, _server);
            Bind(0, 2, _packs);
            Bind(1, 0, _profile);
            Bind(1, 1, _dan);
            Bind(1, 2, _achievements);

            RectTransform chrome = LibraryUi.CreateRect("Chrome", _root);
            LibraryWidgets.Inset(chrome, 0f, HomeTheme.Layout.FOOTER_HEIGHT, 0f, HomeTheme.Layout.HEADER_HEIGHT);
            _chrome = chrome.gameObject.AddComponent<CanvasGroup>();
            _next = new HomeEdgeHandle(chrome, true, () => TurnTo(1, true));
            _previous = new HomeEdgeHandle(chrome, false, () => TurnTo(0, true));

            _toast = new UiToast(_root, HomeTheme.Layout.FOOTER_HEIGHT, HomeTheme.Colors.Violet);

            ApplyPage(0f);
            ApplyEnter(EnterDuration);
        }

        /// <summary>Where the emblem sits in the header; the splash flies its emblem here.</summary>
        internal RectTransform BrandSlot { get; private set; }

        internal RectTransform Root => _root;

        /// <summary>The page on screen once it settles: 0 the first, 1 the second.</summary>
        internal int Page => _page;

        /// <summary>Seconds from the start of the entrance until everything has settled.</summary>
        internal static float EnterDuration =>
            Mathf.Max(ENTER_DELAY + 2f * HomeTheme.Motion.ENTER_STAGGER + HomeTheme.Motion.ENTER, COUNT_DELAY + HomeTheme.Motion.COUNT_UP);

        private const float ENTER_DELAY = 0.04f;
        private const float COUNT_DELAY = 0.2f;

        internal bool Interactable
        {
            set
            {
                _group.interactable = value;
                _group.blocksRaycasts = value;
            }
        }

        internal void SetSummary(HomeLibrarySummary summary) => _library.SetSummary(summary);

        internal void SetMarquee(Sprite[] sprites) => _library.SetMarquee(sprites);

        internal void SetRecentJacket(Sprite sprite) => _library.SetJacket(0, sprite);

        internal void SetAccent(Color32 accent, Color32 glow) => _library.SetAccent(accent, glow);

        internal void SetProfile(HomeProfileSummary summary) => _profile.SetSummary(summary);

        /// <summary>Idle animation: marquee, globe, card fan, badge, status lights; only for a page in view.</summary>
        internal void Tick(double time)
        {
            _time = time;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (Mathf.Abs(i / CARDS_PER_PAGE - _pageProgress) < 1f)
                {
                    _slots[i].Card.Tick(time);
                }
            }

            ApplyHandles();
        }

        /// <summary>
        /// The entrance at <paramref name="elapsed"/> seconds in: header and footer fade, the cards
        /// rise one after another and the library's numbers count up. Before 0 everything is
        /// hidden; from <see cref="EnterDuration"/> on, everything is in place.
        /// </summary>
        internal void ApplyEnter(float elapsed)
        {
            // The entrance starts from the page at rest; settle it once, not every frame.
            if (_expansion != 0f)
            {
                ApplyExpansion(0f);
            }

            float header = HomeMotion.Span(elapsed, 0f, 0.45f, Ease.OutQuad);
            _header.alpha = header;
            _headerRect.anchoredPosition = new Vector2(0f, 18f * (1f - header));
            _footer.alpha = HomeMotion.Span(elapsed, 0.35f, 0.5f, Ease.OutQuad);
            _chrome.alpha = _footer.alpha;

            Rise(_library.Root, _library.Group, HomeMotion.Span(elapsed, ENTER_DELAY, HomeTheme.Motion.ENTER, Ease.OutCubic));
            Rise(_server.Root, _server.Group,
                HomeMotion.Span(elapsed, ENTER_DELAY + HomeTheme.Motion.ENTER_STAGGER, HomeTheme.Motion.ENTER, Ease.OutCubic));
            Rise(_packs.Root, _packs.Group,
                HomeMotion.Span(elapsed, ENTER_DELAY + 2f * HomeTheme.Motion.ENTER_STAGGER, HomeTheme.Motion.ENTER, Ease.OutCubic));

            _library.SetInnerAlpha(1f, HomeMotion.Span(elapsed, 0.15f, 0.7f, Ease.OutQuad));
            _library.ApplyCount(HomeMotion.Span(elapsed, COUNT_DELAY, HomeTheme.Motion.COUNT_UP, Ease.OutCubic));
        }

        /// <summary>
        /// 0 is the page at rest; 1 is the library card grown past every edge of the screen in the
        /// ground color, everything else faded. The hand-over to the library runs it up, the
        /// return runs it back down.
        /// </summary>
        internal void ApplyExpansion(float amount) => ApplyExpansion(HomeDestination.Library, amount);

        /// <summary>
        /// The hand-over to <paramref name="target"/>: 0 is the page at rest, 1 is that card grown
        /// past every edge of the screen in the ground color with everything else faded. Any call
        /// with 0 settles every card back into its slot. The target's page has to be the one on screen.
        /// </summary>
        internal void ApplyExpansion(HomeDestination target, float amount)
        {
            _expansion = amount;
            int targetIndex = (int)target;
            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            float bleed = HomeTheme.Layout.HERO_RADIUS + 8f;
            var bleedMin = new Vector2(-margin - bleed, -HomeTheme.Layout.FOOTER_HEIGHT - bleed);
            var bleedMax = new Vector2(margin + bleed, HomeTheme.Layout.HEADER_HEIGHT + bleed);

            float content = 1f - HomeMotion.Span(amount, 0f, 0.4f, Ease.OutQuad);
            float others = 1f - HomeMotion.Span(amount, 0f, 0.45f, Ease.OutQuad);
            float shrink = 1f - 0.05f * HomeMotion.Span(amount, 0f, 0.6f, Ease.OutCubic);
            var shrunk = new Vector3(shrink, shrink, 1f);
            for (int i = 0; i < _slots.Length; i++)
            {
                HomeSlot slot = _slots[i];
                bool isTarget = i == targetIndex;
                slot.Apply(isTarget ? amount : 0f, bleedMin, bleedMax);
                IHomeCard card = slot.Card;
                card.SetCovering(isTarget ? HomeMotion.Span(amount, 0.25f, 0.75f, Ease.InOutQuad) : 0f);
                card.SetInnerAlpha(isTarget ? content : 1f);
                card.Group.alpha = isTarget ? 1f : others;
                card.Root.localScale = isTarget ? Vector3.one : shrunk;
                card.Root.anchoredPosition = Vector2.zero;
            }

            // The growing card draws over the others; at rest each page's hero card is on top again.
            if (amount > 0f)
            {
                _slots[targetIndex].Rect.SetAsLastSibling();
            }
            else
            {
                for (int page = 0; page < PAGE_COUNT; page++)
                {
                    _slots[page * CARDS_PER_PAGE].Rect.SetAsLastSibling();
                }
            }

            _header.alpha = others;
            _footer.alpha = others;
            _chrome.alpha = others;
            ApplyInteractable();
        }

        /// <summary>Plays the entrance from the start.</summary>
        internal void PlayEnter(Action onDone = null)
        {
            Interactable = true;
            Run(0f, EnterDuration, EnterDuration, ApplyEnter, onDone, Ease.Linear);
        }

        /// <summary>Grows the library card over the page; <paramref name="onCovered"/> runs once it covers it.</summary>
        internal void PlayExpand(Action onCovered) => PlayExpand(HomeDestination.Library, onCovered);

        /// <summary>Grows <paramref name="target"/>'s card over the page; <paramref name="onCovered"/> runs once it covers it.</summary>
        internal void PlayExpand(HomeDestination target, Action onCovered)
        {
            Interactable = false;
            SettleOn(PageOf(target));
            Run(0f, 1f, HomeTheme.Motion.EXPAND, amount => ApplyExpansion(target, amount), onCovered, Ease.InOutCubic);
        }

        /// <summary>Starts from the page covered by the library card and shrinks it back into place.</summary>
        internal void PlayCollapse(Action onDone = null) => PlayCollapse(HomeDestination.Library, onDone);

        /// <summary>Starts from the page covered by <paramref name="target"/>'s card and shrinks it back into place.</summary>
        internal void PlayCollapse(HomeDestination target, Action onDone)
        {
            Interactable = false;
            SettleOn(PageOf(target));
            Run(1f, 0f, HomeTheme.Motion.COLLAPSE, amount => ApplyExpansion(target, amount), () =>
            {
                Interactable = true;
                onDone?.Invoke();
            }, Ease.OutCubic);
        }

        /// <summary>Stops any entrance or hand-over where it is.</summary>
        internal void StopTransition()
        {
            _transition.TryCancel();
            _transition = default;
        }

        internal void ShowToast(string message) => _toast.Show(message);

        // ---------------------------------------------------------------- pages

        internal static int PageOf(HomeDestination destination) => (int)destination / CARDS_PER_PAGE;

        /// <summary>Turns to <paramref name="page"/>, sliding when <paramref name="animate"/>.</summary>
        internal void TurnTo(int page, bool animate)
        {
            page = Mathf.Clamp(page, 0, PAGE_COUNT - 1);
            if (!animate)
            {
                SettleOn(page);
                return;
            }

            if (page != _page)
            {
                UiSound.Play(UiCue.Select);
            }

            _page = page;
            _pageMotion.Run(_pageProgress, page, ApplyPage, ApplyInteractable);
            ApplyInteractable();
        }

        /// <summary>
        /// Holds the pages mid-turn: 0 the first page, 1 the second, in between part of each (for
        /// screenshots of a swipe). Nothing settles until <see cref="TurnTo"/>.
        /// </summary>
        internal void SeekPage(float progress)
        {
            _pageMotion.Stop();
            ApplyPage(progress);
            ApplyInteractable();
        }

        private void SettleOn(int page)
        {
            _pageMotion.Stop();
            _swipePointer = NO_POINTER;
            _swiping = false;
            _page = Mathf.Clamp(page, 0, PAGE_COUNT - 1);
            ApplyPage(_page);
            ApplyInteractable();
        }

        private void ApplyPage(float progress)
        {
            _pageProgress = progress;
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                RectTransform page = _pages[i];
                page.anchorMin = new Vector2(i - progress, 0f);
                page.anchorMax = new Vector2(i - progress + 1f, 1f);
                page.offsetMin = Vector2.zero;
                page.offsetMax = Vector2.zero;
            }

            _tabs.Apply(progress);
            ApplyHandles();
        }

        private void ApplyHandles()
        {
            float progress = Mathf.Clamp01(_pageProgress);
            _next.Apply(1f - progress, _time);
            _previous.Apply(progress, _time);
        }

        private void ApplyInteractable()
        {
            bool settled = !_swiping && !_pageMotion.IsActive && Mathf.Approximately(_pageProgress, _page);
            bool usable = settled && !(_expansion > 0f);
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Card.Interactable = usable && i / CARDS_PER_PAGE == _page;
            }
        }

        // ---------------------------------------------------------------- swiping

        public void BeginSwipe(PointerEventData eventData)
        {
            if (_swipePointer != NO_POINTER || !_group.blocksRaycasts || _transition.IsActive())
            {
                return;
            }

            _swipePointer = eventData.pointerId;
            _swiping = false;
            _swipeStartX = Local(eventData.pressPosition, eventData).x;
            _swipeLastX = Local(eventData.position, eventData).x;
            _swipeLastTime = Time.unscaledTime;
            _swipeVelocity = 0f;
            Swipe(eventData);
        }

        public void Swipe(PointerEventData eventData)
        {
            if (eventData.pointerId != _swipePointer)
            {
                return;
            }

            Vector2 position = Local(eventData.position, eventData);
            Vector2 press = Local(eventData.pressPosition, eventData);
            float dx = position.x - _swipeStartX;
            if (!_swiping)
            {
                float dy = position.y - press.y;
                if (Mathf.Abs(dx) < HomeTheme.Motion.SWIPE_SLOP || Mathf.Abs(dx) < Mathf.Abs(dy))
                {
                    // Still a tap, or going up and down: a long vertical drag is no tap either.
                    if (Mathf.Abs(dy) > HomeTheme.Motion.SWIPE_SLOP * 2f)
                    {
                        HomeSwipe.CancelPress(eventData);
                    }

                    return;
                }

                // From here the finger has the pages: grab a turn in flight where it is.
                _swiping = true;
                HomeSwipe.CancelPress(eventData);
                _pageMotion.Stop();
                _swipeOrigin = _pageProgress;
                _swipeStartX = position.x;
                dx = 0f;
                ApplyInteractable();
            }

            float width = Mathf.Max(1f, _root.rect.width);
            ApplyPage(Rubber(_swipeOrigin - dx / width));
            float now = Time.unscaledTime;
            float elapsed = now - _swipeLastTime;
            if (elapsed > 0.0001f)
            {
                _swipeVelocity = Mathf.Lerp(_swipeVelocity, (position.x - _swipeLastX) / elapsed, 0.6f);
            }

            _swipeLastX = position.x;
            _swipeLastTime = now;
        }

        public void EndSwipe(PointerEventData eventData)
        {
            if (eventData.pointerId != _swipePointer)
            {
                return;
            }

            _swipePointer = NO_POINTER;
            if (!_swiping)
            {
                return;
            }

            _swiping = false;
            // A finger that stopped before lifting is not a flick.
            float velocity = Time.unscaledTime - _swipeLastTime > 0.1f ? 0f : _swipeVelocity;
            int from = Mathf.Clamp(Mathf.RoundToInt(_swipeOrigin), 0, PAGE_COUNT - 1);
            float moved = _pageProgress - from;
            int target = from;
            bool far = Mathf.Abs(moved) > HomeTheme.Motion.PAGE_TURN_DISTANCE;
            bool flicked = Mathf.Abs(velocity) > HomeTheme.Motion.PAGE_FLICK_SPEED && Mathf.Sign(-velocity) == Mathf.Sign(moved);
            if (far || flicked)
            {
                target = from + (moved > 0f ? 1 : -1);
            }

            TurnTo(target, true);
        }

        private static float Rubber(float progress)
        {
            float last = PAGE_COUNT - 1;
            if (progress < 0f) return progress * RUBBER;
            if (progress > last) return last + (progress - last) * RUBBER;
            return progress;
        }

        private Vector2 Local(Vector2 screen, PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, eventData.pressEventCamera, out Vector2 local);
            return local;
        }

        // ---------------------------------------------------------------- building

        public void Dispose()
        {
            StopTransition();
            _pageMotion.Stop();
            _toast.Dispose();
            _server.Dispose();
            _packs.Dispose();
            _dan.Dispose();
            _achievements.Dispose();
        }

        private void Run(float from, float to, float duration, Action<float> apply, Action onDone, Ease ease)
        {
            StopTransition();
            apply(from);
            _transition = LMotion.Create(from, to, duration)
                .WithEase(ease)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() =>
                {
                    _transition = default;
                    onDone?.Invoke();
                })
                .Bind(apply);
        }

        private static void Rise(RectTransform card, CanvasGroup group, float progress)
        {
            group.alpha = progress;
            card.anchoredPosition = new Vector2(0f, -HomeTheme.Motion.ENTER_RISE * (1f - progress));
            float scale = 0.97f + 0.03f * progress;
            card.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Creates the slot for <paramref name="role"/> (0 hero, 1 top right, 2 bottom right) on a page.</summary>
        private HomeSlot Slot(int page, int role)
        {
            RectTransform grid = _pages[page].childCount > 0 ? (RectTransform)_pages[page].GetChild(0) : CreateGrid(_pages[page]);
            float halfGap = HomeTheme.Layout.GRID_GAP * 0.5f;
            float share = HomeTheme.Layout.HERO_SHARE;
            HomeSlot slot;
            switch (role)
            {
                case 0:
                    slot = new HomeSlot(grid, "HeroSlot", Vector2.zero, new Vector2(share, 1f), Vector2.zero, new Vector2(-halfGap, 0f));
                    break;
                case 1:
                    slot = new HomeSlot(grid, "TopSlot", new Vector2(share, 0.5f), Vector2.one, new Vector2(halfGap, halfGap), Vector2.zero);
                    break;
                default:
                    slot = new HomeSlot(grid, "BottomSlot", new Vector2(share, 0f), new Vector2(1f, 0.5f), new Vector2(halfGap, 0f),
                        new Vector2(0f, -halfGap));
                    break;
            }

            _slots[page * CARDS_PER_PAGE + role] = slot;
            return slot;
        }

        private void Bind(int page, int role, IHomeCard card)
        {
            HomeSlot slot = _slots[page * CARDS_PER_PAGE + role];
            slot.Card = card;
            LibraryUi.Stretch(card.Root);
            HomeSwipe.Attach(card.Surface, this);
        }

        private static RectTransform CreateGrid(RectTransform page)
        {
            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform grid = LibraryUi.CreateRect("Grid", page);
            LibraryWidgets.Inset(grid, margin, HomeTheme.Layout.FOOTER_HEIGHT, margin, HomeTheme.Layout.HEADER_HEIGHT);
            return grid;
        }

        private void BuildHeader(RectTransform header, Action openAbout, Action openSettings)
        {
            Vector2 leftCenter = new Vector2(0f, 0.5f);
            float emblem = HomeTheme.Layout.BRAND_EMBLEM_SIZE;
            BrandSlot = LibraryUi.CreateRect("BrandSlot", header);
            LibraryWidgets.Place(BrandSlot, leftCenter, leftCenter, new Vector2(-6f, 0f), new Vector2(emblem, emblem));

            TextMeshProUGUI wordmark = LibraryWidgets.Label("Wordmark", header, Wordmark, HomeTheme.Layout.WORDMARK_SIZE,
                LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(wordmark);
            wordmark.characterSpacing = HomeTheme.Layout.WORDMARK_SPACING;
            wordmark.richText = true;
            float wordmarkX = emblem + 6f;
            float wordmarkWidth = Mathf.Ceil(wordmark.GetPreferredValues(wordmark.text).x) + 4f;
            LibraryWidgets.Place(wordmark.rectTransform, leftCenter, leftCenter, new Vector2(wordmarkX, 2f), new Vector2(wordmarkWidth, 52f));

            string version = "v" + Application.version;
            const float CHIP_HEIGHT = 34f;
            Image chip = LibraryWidgets.Panel("Version", header, CHIP_HEIGHT * 0.5f, HomeTheme.Colors.Chip);
            TextMeshProUGUI chipLabel = LibraryWidgets.Label(
                "Label", chip.transform, version, 20f, LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(chipLabel);
            chipLabel.characterSpacing = 6f;
            LibraryUi.Stretch(chipLabel.rectTransform);
            float chipWidth = Mathf.Ceil(chipLabel.GetPreferredValues(version).x) + 30f;
            LibraryWidgets.Place(chip.rectTransform, leftCenter, leftCenter, new Vector2(wordmarkX + wordmarkWidth + 14f, 0f),
                new Vector2(chipWidth, CHIP_HEIGHT));

            float size = LibraryTheme.Layout.CONTROL_HEIGHT;
            float right = LibraryView.AddSettingsButton(header, () => openSettings());
            Button about = LibraryWidgets.PillButton("About", header, string.Empty, () => openAbout?.Invoke(), size, size,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary, sound: UiCue.None);
            LibraryWidgets.Outline((Image)about.targetGraphic, size * 0.5f);
            LibraryWidgets.Icon(about.transform, LibraryIcon.Info, LibraryTheme.Colors.TextSecondary, 30f);
            LibraryWidgets.Place((RectTransform)about.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-right, 0f),
                new Vector2(size, size));
        }

        /// <summary>The wordmark's text, with "ATLAS" in the brand's light violet.</summary>
        internal static string Wordmark => "OPEN SEKAI <color=" + HomeTheme.Colors.WORDMARK_ACCENT_HEX + ">ATLAS</color>";

        /// <summary>One card's place on a page, at rest and grown over the screen.</summary>
        private sealed class HomeSlot
        {
            private readonly Vector2 _anchorMin;
            private readonly Vector2 _anchorMax;
            private readonly Vector2 _offsetMin;
            private readonly Vector2 _offsetMax;

            internal HomeSlot(RectTransform grid, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                Rect = LibraryUi.CreateRect(name, grid);
                _anchorMin = anchorMin;
                _anchorMax = anchorMax;
                _offsetMin = offsetMin;
                _offsetMax = offsetMax;
                Apply(0f, Vector2.zero, Vector2.zero);
            }

            internal RectTransform Rect { get; }
            internal IHomeCard Card { get; set; }

            /// <param name="grow">0 in its slot, 1 over the whole screen.</param>
            internal void Apply(float grow, Vector2 bleedMin, Vector2 bleedMax)
            {
                Rect.anchorMin = Vector2.Lerp(_anchorMin, Vector2.zero, grow);
                Rect.anchorMax = Vector2.Lerp(_anchorMax, Vector2.one, grow);
                Rect.offsetMin = Vector2.Lerp(_offsetMin, bleedMin, grow);
                Rect.offsetMax = Vector2.Lerp(_offsetMax, bleedMax, grow);
            }
        }
    }
}
