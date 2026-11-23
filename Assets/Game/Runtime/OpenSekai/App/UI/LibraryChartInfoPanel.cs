using System;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.Infrastructure.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// A chart's 详情 card: where it came from, with copyable links, and whether it plays Ranked,
    /// as a large banner across the lower middle with the reason under it. 「Ranked 规则」 in the
    /// bottom-right corner opens the rules over the card.
    /// </summary>
    internal sealed class LibraryChartInfoPanel : IDisposable
    {
        internal const string RANKED_BANNER = "-Ranked-";
        internal const string UNRANKED_BANNER = "-Unranked-";

        private const float CARD_WIDTH = 1120f;
        private const float CARD_HEIGHT = 660f;
        private const float COPY_WIDTH = 136f;
        private const float FOOTER_HEIGHT = 56f;
        private const float BANNER_HEIGHT = 116f;
        private const float BANNER_GAP = 10f;
        private const float LINKS_GAP = 14f;
        private const float RULES_WIDTH = 196f;
        private const float BANNER_FONT_SIZE = 80f;
        private const byte RANKED_BAND_OPACITY = 44;
        private const byte UNRANKED_BAND_OPACITY = 12;

        private readonly Transform _parent;
        private readonly LibraryModal _modal;
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
        private LibraryRankRulesPanel _rules;
        private MotionHandle _bannerMotion;
        private bool? _shownRanked;

        internal LibraryChartInfoPanel(Transform parent)
        {
            _parent = parent;
            RectTransform root = LibraryUi.CreateRect("LibraryChartInfo", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "谱面详情", Hide, withSubtitle: true);
            _modal.Subtitle.richText = false;
            RectTransform body = _modal.Body;

            RectTransform links = LibraryUi.CreateRect("Links", body);
            LibraryWidgets.Inset(links, 0f, FOOTER_HEIGHT + BANNER_GAP + BANNER_HEIGHT + LINKS_GAP, 0f, 10f);
            _content = PackWidgets.Scroll(links, out _scroll);
            _first = new LinkRow(_content, "FirstLink", OnCopied);
            _second = new LinkRow(_content, "SecondLink", OnCopied);

            // The banner: a band that fades out toward both sides, edged by two hairlines, the
            // verdict large in the middle.
            _banner = LibraryUi.CreateRect("Banner", body);
            LibraryWidgets.BottomStrip(_banner, 0f, 0f, FOOTER_HEIGHT + BANNER_GAP, BANNER_HEIGHT);
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

            _note = PackWidgets.UserLabel(
                "Note", body, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted,
                alignment: TextAlignmentOptions.Center);
            LibraryWidgets.BottomStrip(_note.rectTransform, RULES_WIDTH + 16f, RULES_WIDTH + 16f, 0f, FOOTER_HEIGHT);

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
            float top;
            if (source == null)
            {
                top = _first.Bind("来源", "未记录下载地址", 0f, copyable: false);
                _second.Hide();
            }
            else
            {
                bool web = source.PageUrl != null;
                top = _first.Bind(web ? "来源网页" : "Sonolus 服务器", source.PageUrl ?? source.SonolusServerUrl, 0f);
                top = _second.Bind(web ? "下载直链" : "Sonolus 关卡", source.DownloadUrl ?? source.SonolusLevelUrl, top);
            }

            _content.sizeDelta = new Vector2(0f, top);
            _shownRanked = null;
            ShowRank(null);
            _modal.Root.SetAsLastSibling();
            _modal.Open();
            _scroll.StopMovement();
            _content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Draws the banner from the chart's local record.</summary>
        /// <param name="problem">Why a refresh of that record just failed; the card then says it shows the old one.</param>
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
            _note.text = problem != null ? problem + "，显示本地记录" : RankNote(Item);

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
            _bannerMotion.TryCancel();
            _rules?.Dispose();
            _modal.Dispose();
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
            private readonly RectTransform _root;
            private readonly TextMeshProUGUI _caption;
            private readonly TextMeshProUGUI _url;
            private readonly GameObject _copy;
            private readonly TextMeshProUGUI _copyLabel;

            internal LinkRow(Transform content, string name, Action<LinkRow> copied)
            {
                _root = LibraryUi.CreateRect(name, content);
                _caption = LibraryWidgets.Label("Caption", _root, string.Empty, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_caption.rectTransform, 6f, COPY_WIDTH + 24f, 4f, 32f);
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

            /// <returns>The top of the next row.</returns>
            internal float Bind(string caption, string url, float top, bool copyable = true)
            {
                _root.gameObject.SetActive(true);
                _caption.text = caption;
                _url.text = url;
                _url.color = copyable ? LibraryTheme.Colors.TextSecondary : LibraryTheme.Colors.TextMuted;
                _copy.SetActive(copyable);
                ResetCopy();
                float width = CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f - COPY_WIDTH - 42f;
                float height = Mathf.Max(32f, _url.GetPreferredValues(url, width, 0f).y + 4f);
                LibraryWidgets.TopStrip(_root, 0f, 0f, top, height + 60f);
                LibraryWidgets.TopStrip(_url.rectTransform, 6f, COPY_WIDTH + 24f, 38f, height);
                return top + height + 60f;
            }

            internal void Hide() => _root.gameObject.SetActive(false);

            internal void ResetCopy() => _copyLabel.text = "复制";
        }
    }
}
