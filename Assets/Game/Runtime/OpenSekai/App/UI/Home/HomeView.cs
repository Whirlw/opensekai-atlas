using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The home page: the brand in the header, the library card beside two cards for what comes
    /// next (服务器, 资源包), and a footer. Its entrance, its hand-over to the library (the library
    /// card grows over the page) and its return (the card shrinks back) are pure functions of
    /// time, so the splash can drive the entrance from its own timeline and a screenshot can stop
    /// at any frame.
    /// </summary>
    internal sealed class HomeView : IDisposable
    {
        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly CanvasGroup _header;
        private readonly RectTransform _headerRect;
        private readonly CanvasGroup _footer;
        private readonly RectTransform _librarySlot;
        private readonly HomeLibraryCard _library;
        private readonly HomeFeatureCard _server;
        private readonly HomeFeatureCard _packs;
        private readonly RectTransform _toast;
        private readonly CanvasGroup _toastGroup;
        private readonly TextMeshProUGUI _toastText;
        private MotionHandle _transition;
        private MotionHandle _toastMotion;

        /// <summary>Last value <see cref="ApplyExpansion"/> applied; NaN before the first.</summary>
        private float _expansion = float.NaN;

        /// <param name="parent">Full-screen rect the page fills.</param>
        /// <param name="openLibrary">The library card was tapped.</param>
        /// <param name="openAbout">The header's about button was tapped.</param>
        /// <param name="comingSoon">A card for an unbuilt part was tapped; gets the part's name.</param>
        internal HomeView(RectTransform parent, Action openLibrary, Action openAbout, Action<string> comingSoon, Action openServer = null)
        {
            _root = LibraryUi.CreateRect("Home", parent);
            LibraryUi.Stretch(_root);
            _group = _root.gameObject.AddComponent<CanvasGroup>();

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;

            // Built back to front: header and footer first, then the two small cards, and the
            // library card last, so it covers all of them as it grows over the page.
            _headerRect = LibraryUi.CreateRect("Header", _root);
            LibraryWidgets.TopStrip(_headerRect, margin, margin, 0f, HomeTheme.Layout.HEADER_HEIGHT);
            _header = _headerRect.gameObject.AddComponent<CanvasGroup>();
            BuildHeader(_headerRect, openAbout);

            RectTransform footer = LibraryUi.CreateRect("Footer", _root);
            LibraryWidgets.BottomStrip(footer, margin, margin, 30f, 30f);
            _footer = footer.gameObject.AddComponent<CanvasGroup>();
            _footer.blocksRaycasts = false;
            TextMeshProUGUI tagline = LibraryWidgets.Label(
                "Tagline", footer, "Open Sekai Atlas  ·  开源下落式音游播放器", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryUi.Stretch(tagline.rectTransform);
            TextMeshProUGUI licenses = LibraryWidgets.Label(
                "Licenses", footer, "MIT  ·  OFL  ·  CC0", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.Stretch(licenses.rectTransform);

            RectTransform grid = LibraryUi.CreateRect("Grid", _root);
            LibraryWidgets.Inset(grid, margin, HomeTheme.Layout.FOOTER_HEIGHT, margin, HomeTheme.Layout.HEADER_HEIGHT);
            float halfGap = HomeTheme.Layout.GRID_GAP * 0.5f;
            float share = HomeTheme.Layout.HERO_SHARE;

            RectTransform serverSlot = Slot(grid, "ServerSlot", new Vector2(share, 0.5f), Vector2.one,
                new Vector2(halfGap, halfGap), Vector2.zero);
            _server = new HomeFeatureCard(serverSlot, "ServerCard", "SERVER", "服务器", "浏览社区谱面 · 下载后自动导入",
                HomeTheme.Colors.Periwinkle, root => new HomeGlobeArt(root), () => openServer?.Invoke(), available: true);
            LibraryUi.Stretch(_server.Root);

            RectTransform packsSlot = Slot(grid, "PacksSlot", new Vector2(share, 0f), new Vector2(1f, 0.5f),
                new Vector2(halfGap, 0f), new Vector2(0f, -halfGap));
            _packs = new HomeFeatureCard(packsSlot, "PacksCard", "RESOURCE PACKS", "资源包", "音符皮肤 · 打击音效 · 界面主题",
                HomeTheme.Colors.Rose, root => new HomeStackArt(root), () => comingSoon?.Invoke("资源包"));
            LibraryUi.Stretch(_packs.Root);

            _librarySlot = Slot(grid, "LibrarySlot", Vector2.zero, new Vector2(share, 1f), Vector2.zero, new Vector2(-halfGap, 0f));
            _library = new HomeLibraryCard(_librarySlot, openLibrary);
            LibraryUi.Stretch(_library.Root);

            _toast = BuildToast(_root, out _toastGroup, out _toastText);

            ApplyEnter(EnterDuration);
        }

        /// <summary>Where the emblem sits in the header; the splash flies its emblem here.</summary>
        internal RectTransform BrandSlot { get; private set; }

        internal RectTransform Root => _root;

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

        /// <summary>Idle animation: marquee, globe, card fan, status lights.</summary>
        internal void Tick(double time)
        {
            _library.Tick(time);
            _server.Tick(time);
            _packs.Tick(time);
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
        internal void ApplyExpansion(float amount)
        {
            _expansion = amount;
            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            float bleed = HomeTheme.Layout.HERO_RADIUS + 8f;
            float halfGap = HomeTheme.Layout.GRID_GAP * 0.5f;
            _librarySlot.anchorMin = Vector2.zero;
            _librarySlot.anchorMax = Vector2.Lerp(new Vector2(HomeTheme.Layout.HERO_SHARE, 1f), Vector2.one, amount);
            _librarySlot.offsetMin = Vector2.Lerp(Vector2.zero,
                new Vector2(-margin - bleed, -HomeTheme.Layout.FOOTER_HEIGHT - bleed), amount);
            _librarySlot.offsetMax = Vector2.Lerp(new Vector2(-halfGap, 0f),
                new Vector2(margin + bleed, HomeTheme.Layout.HEADER_HEIGHT + bleed), amount);
            _library.SetCovering(HomeMotion.Span(amount, 0.25f, 0.75f, Ease.InOutQuad));

            float content = 1f - HomeMotion.Span(amount, 0f, 0.4f, Ease.OutQuad);
            _library.SetInnerAlpha(content, content);
            float others = 1f - HomeMotion.Span(amount, 0f, 0.45f, Ease.OutQuad);
            _server.Group.alpha = others;
            _packs.Group.alpha = others;
            float shrink = 1f - 0.05f * HomeMotion.Span(amount, 0f, 0.6f, Ease.OutCubic);
            _server.Root.localScale = new Vector3(shrink, shrink, 1f);
            _packs.Root.localScale = new Vector3(shrink, shrink, 1f);
            _header.alpha = others;
            _footer.alpha = others;
            _library.Root.anchoredPosition = Vector2.zero;
            _library.Root.localScale = Vector3.one;
            _library.Group.alpha = 1f;
            _library.Interactable = amount <= 0f;
        }

        /// <summary>Plays the entrance from the start.</summary>
        internal void PlayEnter(Action onDone = null)
        {
            Interactable = true;
            Run(0f, EnterDuration, EnterDuration, ApplyEnter, onDone, Ease.Linear);
        }

        /// <summary>Grows the library card over the page; <paramref name="onCovered"/> runs once it covers it.</summary>
        internal void PlayExpand(Action onCovered)
        {
            Interactable = false;
            Run(0f, 1f, HomeTheme.Motion.EXPAND, ApplyExpansion, onCovered, Ease.InOutCubic);
        }

        /// <summary>Starts from the covered page and shrinks the library card back into place.</summary>
        internal void PlayCollapse(Action onDone = null)
        {
            Interactable = false;
            Run(1f, 0f, HomeTheme.Motion.COLLAPSE, ApplyExpansion, () =>
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

        internal void ShowToast(string message)
        {
            _toastText.text = message;
            float width = Mathf.Ceil(_toastText.GetPreferredValues(message).x) + 112f;
            _toast.sizeDelta = new Vector2(width, _toast.sizeDelta.y);
            _toastMotion.TryCancel();
            float hold = HomeTheme.Motion.TOAST;
            _toastMotion = LMotion.Create(0f, hold + 0.6f, hold + 0.6f)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _toast.gameObject.SetActive(false))
                .Bind(time =>
                {
                    float shown = HomeMotion.Span(time, 0f, 0.28f, Ease.OutCubic) * (1f - HomeMotion.Span(time, hold + 0.3f, 0.3f, Ease.InQuad));
                    _toastGroup.alpha = shown;
                    _toast.anchoredPosition = new Vector2(0f, HomeTheme.Layout.FOOTER_HEIGHT + 4f + 14f * shown);
                });
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
        }

        public void Dispose()
        {
            StopTransition();
            _toastMotion.TryCancel();
            _server.Dispose();
            _packs.Dispose();
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

        private static RectTransform Slot(RectTransform grid, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform slot = LibraryUi.CreateRect(name, grid);
            slot.anchorMin = anchorMin;
            slot.anchorMax = anchorMax;
            slot.offsetMin = offsetMin;
            slot.offsetMax = offsetMax;
            return slot;
        }

        private void BuildHeader(RectTransform header, Action openAbout)
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
            const float chipHeight = 34f;
            Image chip = LibraryWidgets.Panel("Version", header, chipHeight * 0.5f, HomeTheme.Colors.Chip);
            TextMeshProUGUI chipLabel = LibraryWidgets.Label(
                "Label", chip.transform, version, 20f, LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(chipLabel);
            chipLabel.characterSpacing = 6f;
            LibraryUi.Stretch(chipLabel.rectTransform);
            float chipWidth = Mathf.Ceil(chipLabel.GetPreferredValues(version).x) + 30f;
            LibraryWidgets.Place(chip.rectTransform, leftCenter, leftCenter, new Vector2(wordmarkX + wordmarkWidth + 14f, 0f),
                new Vector2(chipWidth, chipHeight));

            float size = LibraryTheme.Layout.CONTROL_HEIGHT;
            Button about = LibraryWidgets.PillButton("About", header, string.Empty, () => openAbout?.Invoke(), size, size,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary, sound: UiCue.None);
            LibraryWidgets.Outline((Image)about.targetGraphic, size * 0.5f);
            LibraryWidgets.Icon(about.transform, LibraryIcon.Info, LibraryTheme.Colors.TextSecondary, 30f);
            LibraryWidgets.Place((RectTransform)about.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                new Vector2(size, size));
        }

        /// <summary>The wordmark's text, with "ATLAS" in the brand's light violet.</summary>
        internal static string Wordmark => "OPEN SEKAI <color=" + HomeTheme.Colors.WORDMARK_ACCENT_HEX + ">ATLAS</color>";

        private static RectTransform BuildToast(RectTransform root, out CanvasGroup group, out TextMeshProUGUI text)
        {
            const float height = 64f;
            RectTransform toast = LibraryUi.CreateRect("Toast", root);
            LibraryWidgets.Place(toast, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, HomeTheme.Layout.FOOTER_HEIGHT), new Vector2(480f, height));
            group = toast.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            LibraryWidgets.Shadow(toast, height * 0.5f, 30f, 10f, 140);
            Image surface = LibraryWidgets.Panel("Surface", toast, height * 0.5f, LibraryTheme.Colors.SurfaceRaised);
            LibraryUi.Stretch(surface.rectTransform);
            LibraryWidgets.Outline(surface, height * 0.5f);
            Image icon = LibraryWidgets.Icon(toast, LibraryIcon.Info, HomeTheme.Colors.Violet, 26f);
            LibraryWidgets.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(26f, 26f));
            text = LibraryWidgets.Label(
                "Text", toast, string.Empty, 22f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Left);
            LibraryWidgets.Inset(text.rectTransform, 70f, 0f, 30f, 0f);
            toast.gameObject.SetActive(false);
            return toast;
        }
    }
}
