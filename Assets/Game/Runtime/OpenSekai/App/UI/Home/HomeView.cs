using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The home page: the brand in the header, the library card beside the 服务器 and 资源包
    /// cards, and a footer. Its entrance, its hand-over to the library (the library
    /// card grows over the page) and its return (the card shrinks back) are pure functions of
    /// time, so the splash can drive the entrance from its own timeline and a screenshot can stop
    /// at any frame.
    /// </summary>
    /// <summary>Which card grows over the page when the home page hands over.</summary>
    internal enum HomeDestination
    {
        Library,
        Server,
        Packs
    }

    internal sealed class HomeView : IDisposable
    {
        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly CanvasGroup _header;
        private readonly RectTransform _headerRect;
        private readonly CanvasGroup _footer;
        private readonly RectTransform _librarySlot;
        private readonly RectTransform _serverSlot;
        private readonly RectTransform _packsSlot;
        private readonly HomeLibraryCard _library;
        private readonly HomeFeatureCard _server;
        private readonly HomeFeatureCard _packs;
        private readonly UiToast _toast;
        private MotionHandle _transition;

        /// <summary>Last value <see cref="ApplyExpansion"/> applied; NaN before the first.</summary>
        private float _expansion = float.NaN;

        /// <param name="parent">Full-screen rect the page fills.</param>
        /// <param name="openLibrary">The library card was tapped.</param>
        /// <param name="openAbout">The header's about button was tapped.</param>
        /// <param name="comingSoon">A card for an unbuilt part was tapped; gets the part's name.</param>
        internal HomeView(RectTransform parent, Action openLibrary, Action openAbout, Action openSettings, Action<string> comingSoon,
            Action openServer = null, Action openPacks = null)
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
            BuildHeader(_headerRect, openAbout, openSettings);

            RectTransform footer = LibraryUi.CreateRect("Footer", _root);
            LibraryWidgets.BottomStrip(footer, margin, margin, 30f, 30f);
            _footer = footer.gameObject.AddComponent<CanvasGroup>();
            _footer.blocksRaycasts = false;
            TextMeshProUGUI tagline = LibraryWidgets.Label(
                "Tagline", footer, "Open Sekai Atlas  ·  开源下落式音游播放器", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryUi.Stretch(tagline.rectTransform);
            TextMeshProUGUI licenses = LibraryWidgets.Label(
                "PlayFeatures", footer, "社区谱面  ·  离线打歌", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.Stretch(licenses.rectTransform);

            RectTransform grid = LibraryUi.CreateRect("Grid", _root);
            LibraryWidgets.Inset(grid, margin, HomeTheme.Layout.FOOTER_HEIGHT, margin, HomeTheme.Layout.HEADER_HEIGHT);
            float halfGap = HomeTheme.Layout.GRID_GAP * 0.5f;
            float share = HomeTheme.Layout.HERO_SHARE;

            RectTransform serverSlot = _serverSlot = Slot(grid, "ServerSlot", new Vector2(share, 0.5f), Vector2.one,
                new Vector2(halfGap, halfGap), Vector2.zero);
            _server = new HomeFeatureCard(serverSlot, "ServerCard", "SERVER", "服务器", "浏览社区谱面 · 下载后自动导入",
                HomeTheme.Colors.Periwinkle, root => new HomeGlobeArt(root), () => openServer?.Invoke(), available: openServer != null);
            LibraryUi.Stretch(_server.Root);

            RectTransform packsSlot = _packsSlot = Slot(grid, "PacksSlot", new Vector2(share, 0f), new Vector2(1f, 0.5f),
                new Vector2(halfGap, 0f), new Vector2(0f, -halfGap));
            _packs = new HomeFeatureCard(packsSlot, "PacksCard", "RESOURCE PACKS", "资源包", "音符皮肤 · 打击音效 · 判定与界面",
                HomeTheme.Colors.Rose, root => new HomeStackArt(root),
                () =>
                {
                    if (openPacks != null) openPacks();
                    else comingSoon?.Invoke("资源包");
                },
                available: openPacks != null, status: openPacks != null ? "管理资源" : null);
            LibraryUi.Stretch(_packs.Root);

            _librarySlot = Slot(grid, "LibrarySlot", Vector2.zero, new Vector2(share, 1f), Vector2.zero, new Vector2(-halfGap, 0f));
            _library = new HomeLibraryCard(_librarySlot, openLibrary);
            LibraryUi.Stretch(_library.Root);

            _toast = new UiToast(_root, HomeTheme.Layout.FOOTER_HEIGHT, HomeTheme.Colors.Violet);

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
        internal void ApplyExpansion(float amount) => ApplyExpansion(HomeDestination.Library, amount);

        /// <summary>
        /// The hand-over to <paramref name="target"/>: 0 is the page at rest, 1 is that card grown
        /// past every edge of the screen in the ground color with everything else faded. Any call
        /// with 0 settles every card back into its slot.
        /// </summary>
        internal void ApplyExpansion(HomeDestination target, float amount)
        {
            _expansion = amount;
            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            float bleed = HomeTheme.Layout.HERO_RADIUS + 8f;
            float halfGap = HomeTheme.Layout.GRID_GAP * 0.5f;
            float share = HomeTheme.Layout.HERO_SHARE;
            var bleedMin = new Vector2(-margin - bleed, -HomeTheme.Layout.FOOTER_HEIGHT - bleed);
            var bleedMax = new Vector2(margin + bleed, HomeTheme.Layout.HEADER_HEIGHT + bleed);
            float library = target == HomeDestination.Library ? amount : 0f;
            float server = target == HomeDestination.Server ? amount : 0f;
            float packs = target == HomeDestination.Packs ? amount : 0f;

            _librarySlot.anchorMin = Vector2.zero;
            _librarySlot.anchorMax = Vector2.Lerp(new Vector2(share, 1f), Vector2.one, library);
            _librarySlot.offsetMin = Vector2.Lerp(Vector2.zero, bleedMin, library);
            _librarySlot.offsetMax = Vector2.Lerp(new Vector2(-halfGap, 0f), bleedMax, library);
            _serverSlot.anchorMin = Vector2.Lerp(new Vector2(share, 0.5f), Vector2.zero, server);
            _serverSlot.anchorMax = Vector2.one;
            _serverSlot.offsetMin = Vector2.Lerp(new Vector2(halfGap, halfGap), bleedMin, server);
            _serverSlot.offsetMax = Vector2.Lerp(Vector2.zero, bleedMax, server);
            _packsSlot.anchorMin = Vector2.Lerp(new Vector2(share, 0f), Vector2.zero, packs);
            _packsSlot.anchorMax = Vector2.Lerp(new Vector2(1f, 0.5f), Vector2.one, packs);
            _packsSlot.offsetMin = Vector2.Lerp(new Vector2(halfGap, 0f), bleedMin, packs);
            _packsSlot.offsetMax = Vector2.Lerp(new Vector2(0f, -halfGap), bleedMax, packs);

            // The growing card draws over the others; at rest the library card is on top again.
            if (server > 0f)
            {
                _serverSlot.SetAsLastSibling();
            }
            else if (packs > 0f)
            {
                _packsSlot.SetAsLastSibling();
            }
            else
            {
                _librarySlot.SetAsLastSibling();
            }

            _library.SetCovering(HomeMotion.Span(library, 0.25f, 0.75f, Ease.InOutQuad));
            _server.SetCovering(HomeMotion.Span(server, 0.25f, 0.75f, Ease.InOutQuad));
            _packs.SetCovering(HomeMotion.Span(packs, 0.25f, 0.75f, Ease.InOutQuad));

            float content = 1f - HomeMotion.Span(amount, 0f, 0.4f, Ease.OutQuad);
            float others = 1f - HomeMotion.Span(amount, 0f, 0.45f, Ease.OutQuad);
            float shrink = 1f - 0.05f * HomeMotion.Span(amount, 0f, 0.6f, Ease.OutCubic);
            var shrunk = new Vector3(shrink, shrink, 1f);
            bool toLibrary = target == HomeDestination.Library;
            bool toServer = target == HomeDestination.Server;
            bool toPacks = target == HomeDestination.Packs;
            _library.SetInnerAlpha(toLibrary ? content : 1f, toLibrary ? content : 1f);
            _library.Group.alpha = toLibrary ? 1f : others;
            _library.Root.localScale = toLibrary ? Vector3.one : shrunk;
            _server.SetInnerAlpha(toServer ? content : 1f);
            _server.Group.alpha = toServer ? 1f : others;
            _server.Root.localScale = toServer ? Vector3.one : shrunk;
            _packs.SetInnerAlpha(toPacks ? content : 1f);
            _packs.Group.alpha = toPacks ? 1f : others;
            _packs.Root.localScale = toPacks ? Vector3.one : shrunk;
            _header.alpha = others;
            _footer.alpha = others;
            _library.Root.anchoredPosition = Vector2.zero;
            _server.Root.anchoredPosition = Vector2.zero;
            _packs.Root.anchoredPosition = Vector2.zero;
            _library.Interactable = amount <= 0f;
            _server.Interactable = amount <= 0f;
            _packs.Interactable = amount <= 0f;
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
            Run(0f, 1f, HomeTheme.Motion.EXPAND, amount => ApplyExpansion(target, amount), onCovered, Ease.InOutCubic);
        }

        /// <summary>Starts from the covered page and shrinks the library card back into place.</summary>
        internal void PlayCollapse(Action onDone = null) => PlayCollapse(HomeDestination.Library, onDone);

        /// <summary>Starts from the page covered by <paramref name="target"/>'s card and shrinks it back into place.</summary>
        internal void PlayCollapse(HomeDestination target, Action onDone)
        {
            Interactable = false;
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

        public void Dispose()
        {
            StopTransition();
            _toast.Dispose();
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
    }
}
