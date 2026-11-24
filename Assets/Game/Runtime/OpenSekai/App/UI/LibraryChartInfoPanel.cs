using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.Infrastructure.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// A chart's 详情 card shows its Ranked status below the copyable source links.
    /// </summary>
    internal sealed class LibraryChartInfoPanel : IDisposable
    {
        internal const string RANKED_BANNER = "-Ranked-";
        internal const string UNRANKED_BANNER = "-Unranked-";

        private const string REFRESH_LABEL = "刷新Ranked状态";
        private const float CARD_WIDTH = 1120f;
        private const float COPY_WIDTH = 136f;
        private const float FOOTER_HEIGHT = 56f;
        private const float BANNER_HEIGHT = 116f;
        private const float MIN_STATUS_HEIGHT = 56f;
        private const float GAP = 14f;
        private const float RULES_WIDTH = 196f;
        private const float REFRESH_WIDTH = 196f;
        private const float BANNER_FONT_SIZE = 80f;
        private const byte RANKED_BAND_OPACITY = 44;
        private const byte UNRANKED_BAND_OPACITY = 12;

        private readonly Transform _parent;
        private readonly Func<SongPackageItem, UniTask<RankCheck>> _checkRank;
        private readonly HashSet<string> _refreshing = new(StringComparer.Ordinal);
        private readonly LibraryModal _modal;
        private readonly RectTransform _links;
        private readonly ScrollRect _scroll;
        private readonly RectTransform _content;
        private readonly LinkRow _first;
        private readonly LinkRow _second;
        private readonly RectTransform _banner;
        private readonly CanvasGroup _bannerGroup;
        private readonly Image _glow;
        private readonly Image _band;
        private readonly Image _topLine;
        private readonly Image _bottomLine;
        private readonly TextMeshProUGUI _bannerText;
        private readonly TextMeshProUGUI _note;
        private readonly UnityEngine.UI.Button _refresh;
        private readonly TextMeshProUGUI _refreshLabel;
        private LibraryRankRulesPanel _rules;
        private MotionHandle _bannerMotion;
        private bool? _shownRanked;

        internal LibraryChartInfoPanel(Transform parent, Func<SongPackageItem, UniTask<RankCheck>> checkRank)
        {
            _parent = parent;
            _checkRank = checkRank;
            RectTransform root = LibraryUi.CreateRect("LibraryChartInfo", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, 0f), "谱面详情", Hide, withSubtitle: true);
            _modal.Subtitle.richText = false;
            RectTransform body = _modal.Body;

            // The banner: a band that fades out toward both sides, edged by two hairlines, the
            // verdict large in the middle.
            _banner = LibraryUi.CreateRect("Banner", body);
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _glow = LibraryUi.CreateImage("Glow", _banner, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(_glow.rectTransform, center, center, Vector2.zero, new Vector2(820f, 300f));
            _band = LibraryWidgets.Shape("Band", _banner, LibraryShapes.Band(), Color.clear);
            _band.type = Image.Type.Simple;
            LibraryWidgets.Inset(_band.rectTransform, 0f, 10f, 0f, 10f);
            _topLine = LibraryWidgets.Shape("TopLine", _banner, LibraryShapes.Band(), Color.clear);
            _topLine.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(_topLine.rectTransform, 0f, 0f, 8f, 2f);
            _bottomLine = LibraryWidgets.Shape("BottomLine", _banner, LibraryShapes.Band(), Color.clear);
            _bottomLine.type = Image.Type.Simple;
            LibraryWidgets.BottomStrip(_bottomLine.rectTransform, 0f, 0f, 8f, 2f);
            _bannerText = LibraryWidgets.Label(
                "Verdict", _banner, string.Empty, BANNER_FONT_SIZE, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_bannerText, 6f * 100f / BANNER_FONT_SIZE);
            _bannerText.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(_bannerText.rectTransform);

            _note = PackWidgets.UserLabel("Note", body, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            _note.textWrappingMode = TextWrappingModes.Normal;

            _links = LibraryUi.CreateRect("Links", body);
            _content = PackWidgets.Scroll(_links, out _scroll);
            _first = new LinkRow(_content, "FirstLink", OnCopied);
            _second = new LinkRow(_content, "SecondLink", OnCopied);

            _refresh = LibraryWidgets.PillButton(
                "RefreshRank", body, REFRESH_LABEL, () => RefreshRankAsync().Forget(), REFRESH_WIDTH, FOOTER_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, 21f);
            LibraryWidgets.Place((RectTransform)_refresh.transform, Vector2.right, Vector2.right,
                new Vector2(-RULES_WIDTH - GAP, 0f), new Vector2(REFRESH_WIDTH, FOOTER_HEIGHT));
            _refreshLabel = _refresh.GetComponentInChildren<TextMeshProUGUI>();

            Button rules = LibraryWidgets.PillButton(
                "Rules", body, "Ranked 规则", OpenRules, RULES_WIDTH, FOOTER_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, 21f);
            LibraryWidgets.Place((RectTransform)rules.transform, Vector2.right, Vector2.right, Vector2.zero,
                new Vector2(RULES_WIDTH, FOOTER_HEIGHT));

            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <summary>The chart on the card, while it is open or since it last was.</summary>
        internal SongPackageItem Item { get; private set; }

        internal void Show(SongPackageItem item)
        {
            Item = item;
            _modal.Subtitle.text = LibraryModel.BuildTitle(item);
            SongPackageSource source = item?.Source;
            if (source == null)
            {
                _first.Bind("来源", "未记录下载地址", copyable: false);
                _second.Hide();
            }
            else
            {
                bool web = source.IsWeb;
                _first.Bind(web ? "来源网页" : "Sonolus 服务器", source.PageUrl ?? source.SonolusServerUrl);
                _second.Bind(web ? "下载直链" : "Sonolus 关卡", source.DownloadUrl ?? source.SonolusLevelUrl);
            }

            _refresh.gameObject.SetActive(item?.RankKey != null);
            UpdateRefreshButton();
            _shownRanked = null;
            ShowRank(null);
            _modal.Root.SetAsLastSibling();
            _modal.Open();
            _scroll.StopMovement();
            _content.anchoredPosition = Vector2.zero;
        }

        private float LayoutBody(Vector2 available)
        {
            const float NOTE_PADDING = 6f;
            float noteWidth = Mathf.Max(1f, available.x - 2f * NOTE_PADDING);
            float statusHeight = Mathf.Max(MIN_STATUS_HEIGHT, Mathf.Ceil(_note.GetPreferredValues(_note.text, noteWidth, 0f).y));
            float statusBottom = FOOTER_HEIGHT + GAP;
            float bannerBottom = statusBottom + statusHeight;
            float linksBottom = bannerBottom + BANNER_HEIGHT + GAP;
            LibraryWidgets.BottomStrip(_note.rectTransform, NOTE_PADDING, NOTE_PADDING, statusBottom, statusHeight);
            LibraryWidgets.BottomStrip(_banner, 0f, 0f, bannerBottom, BANNER_HEIGHT);
            LibraryWidgets.Inset(_links, 0f, Mathf.Min(linksBottom, available.y), 0f, 0f);

            float firstBottom = _first.Layout(0f, available.x);
            float contentHeight = _second.Layout(firstBottom, available.x);
            _content.sizeDelta = new Vector2(0f, contentHeight);
            _scroll.vertical = contentHeight > Mathf.Max(0f, available.y - linksBottom);
            if (!_scroll.vertical)
            {
                _scroll.StopMovement();
                _content.anchoredPosition = Vector2.zero;
            }

            return linksBottom + contentHeight;
        }

        /// <summary>Draws the banner from the chart's local record.</summary>
        /// <param name="problem">The refresh failure message, if any.</param>
        internal void ShowRank(string problem)
        {
            bool ranked = LibraryModel.IsRanked(Item);
            _bannerText.text = ranked ? RANKED_BANNER : UNRANKED_BANNER;
            _bannerText.enableVertexGradient = ranked;
            if (ranked)
            {
                _bannerText.color = Color.white;
                _bannerText.colorGradient = new VertexGradient(
                    LibraryTheme.Colors.RankedLight, LibraryTheme.Colors.RankedLight,
                    LibraryTheme.Colors.Ranked, LibraryTheme.Colors.Ranked);
            }
            else
            {
                _bannerText.color = LibraryTheme.Colors.TextMuted;
            }

            Color32 tint = ranked ? LibraryTheme.Colors.Ranked : new Color32(255, 255, 255, 255);
            _band.sprite = LibraryShapes.DitheredBand(ranked ? RANKED_BAND_OPACITY : UNRANKED_BAND_OPACITY);
            _band.color = tint;
            _topLine.color = _bottomLine.color = WithAlpha(tint, ranked ? (byte)210 : (byte)40);
            _glow.enabled = ranked;
            _glow.color = WithAlpha(tint, 64);
            _note.text = problem ?? RankNote(Item);
            _modal.FitToBody(LayoutBody);

            if (_shownRanked == ranked)
            {
                return;
            }

            _shownRanked = ranked;
            _bannerMotion.TryCancel();
            _bannerGroup.alpha = 0f;
            _banner.localScale = new Vector3(0.92f, 0.92f, 1f);
            _bannerMotion = LSequence.Create()
                .Append(LMotion.Create(0f, 1f, LibraryTheme.Motion.SLOW)
                    .WithEase(LibraryTheme.Motion.FADE_EASE)
                    .BindToAlpha(_bannerGroup))
                .Join(LMotion.Create(_banner.localScale, Vector3.one, LibraryTheme.Motion.SLOW)
                    .WithEase(LibraryTheme.Motion.SELECT_EASE)
                    .BindToLocalScale(_banner))
                .Run();
        }

        /// <summary>The line under the banner: why the chart is or is not Ranked.</summary>
        internal static string RankNote(SongPackageItem item)
        {
            if (item?.Source == null) return "未记录下载地址，不能参与 Ranked";
            if (LibraryModel.IsRanked(item)) return "已与发布版本核对，成绩可计入 Best 30";
            if (item.RankKey == null) return "此来源不提供 Ranked 谱面";
            ChartRankCache.Verdict verdict = ChartRankCache.Read(item.RankKey);
            if (verdict == null) return "尚未核验，暂按 Unranked 计";
            if (verdict.UnreadableResponse) return "发布方返回的数据无法解析，已记为 Unranked";
            return verdict.Ranked ? "谱面内容或等级与发布版本不一致" : "发布方未将此谱面列为 Ranked";
        }

        internal void Hide()
        {
            _rules?.Hide();
            _modal.Close();
        }

        /// <summary>The back key: closes the rules when they are up, otherwise the card.</summary>
        internal void Back()
        {
            if (_rules != null && _rules.IsOpen)
            {
                _rules.Hide();
                return;
            }

            Hide();
        }

        public void Dispose()
        {
            Item = null;
            _bannerMotion.TryCancel();
            _rules?.Dispose();
            _modal.Dispose();
        }

        private async UniTask RefreshRankAsync()
        {
            SongPackageItem item = Item;
            string key = item?.RankKey;
            if (key == null || !_refreshing.Add(key)) return;

            UpdateRefreshButton();
            ShowRank(null);
            string problem = "暂时无法更新";
            try
            {
                RankCheck result = await _checkRank(item);
                problem = result.Reason;
            }
            finally
            {
                _refreshing.Remove(key);
                if (Item?.RankKey == key && IsOpen)
                {
                    UpdateRefreshButton();
                    ShowRank(problem);
                }
            }
        }

        private void UpdateRefreshButton()
        {
            bool refreshing = Item?.RankKey != null && _refreshing.Contains(Item.RankKey);
            _refresh.interactable = !refreshing;
            _refreshLabel.text = refreshing ? "刷新中…" : REFRESH_LABEL;
            _refreshLabel.color = refreshing ? LibraryTheme.Colors.TextMuted : LibraryTheme.Colors.TextPrimary;
        }

        private void OpenRules()
        {
            _rules ??= new LibraryRankRulesPanel(_parent);
            _rules.Show();
        }

        private void OnCopied(LinkRow copied)
        {
            (copied == _first ? _second : _first).ResetCopy();
        }

        private static Color32 WithAlpha(Color32 color, byte alpha)
        {
            color.a = alpha;
            return color;
        }

        private sealed class LinkRow
        {
            private const float ROW_GAP = 22f;
            private const float URL_LEFT = 6f;
            private const float URL_TOP = 38f;
            private const float COPY_GUTTER = COPY_WIDTH + 24f;

            private readonly RectTransform _root;
            private readonly TextMeshProUGUI _caption;
            private readonly TextMeshProUGUI _url;
            private readonly GameObject _copy;
            private readonly TextMeshProUGUI _copyLabel;

            internal LinkRow(Transform content, string name, Action<LinkRow> copied)
            {
                _root = LibraryUi.CreateRect(name, content);
                _caption = LibraryWidgets.Label("Caption", _root, string.Empty, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_caption.rectTransform, URL_LEFT, COPY_GUTTER, 4f, 32f);
                _url = LibraryWidgets.Label("Url", _root, string.Empty, 20f, LibraryTheme.Colors.TextSecondary,
                    alignment: TextAlignmentOptions.TopLeft);
                _url.richText = false;
                _url.textWrappingMode = TextWrappingModes.Normal;
                _url.overflowMode = TextOverflowModes.Overflow;
                Button copy = LibraryWidgets.PillButton("Copy", _root, "复制", () =>
                {
                    GUIUtility.systemCopyBuffer = _url.text;
                    _copyLabel.text = "已复制";
                    copied(this);
                }, COPY_WIDTH, 54f, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Success);
                LibraryWidgets.Place((RectTransform)copy.transform, Vector2.one, Vector2.one, new Vector2(-4f, -4f),
                    new Vector2(COPY_WIDTH, 54f));
                _copy = copy.gameObject;
                _copyLabel = copy.GetComponentInChildren<TextMeshProUGUI>();
            }

            internal void Bind(string caption, string url, bool copyable = true)
            {
                if (string.IsNullOrEmpty(url))
                {
                    Hide();
                    return;
                }
                _root.gameObject.SetActive(true);
                _caption.text = caption;
                _url.text = url;
                _url.color = copyable ? LibraryTheme.Colors.TextSecondary : LibraryTheme.Colors.TextMuted;
                _copy.SetActive(copyable);
                ResetCopy();
            }

            /// <returns>The bottom of the last visible row.</returns>
            internal float Layout(float top, float width)
            {
                if (!_root.gameObject.activeSelf) return top;
                if (top > 0f) top += ROW_GAP;

                float right = _copy.activeSelf ? COPY_GUTTER : URL_LEFT;
                float textWidth = Mathf.Max(1f, width - URL_LEFT - right);
                float textHeight = Mathf.Max(32f, Mathf.Ceil(_url.GetPreferredValues(_url.text, textWidth, 0f).y) + 4f);
                float height = URL_TOP + textHeight;
                LibraryWidgets.TopStrip(_root, 0f, 0f, top, height);
                LibraryWidgets.TopStrip(_url.rectTransform, URL_LEFT, right, URL_TOP, textHeight);
                return top + height;
            }

            internal void Hide() => _root.gameObject.SetActive(false);

            internal void ResetCopy() => _copyLabel.text = "复制";
        }
    }
}
