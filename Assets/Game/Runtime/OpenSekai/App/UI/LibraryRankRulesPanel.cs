using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 「Ranked 规则」 card, opened from a chart's 详情: which charts are Ranked, how a local copy
    /// keeps that status, how the status is checked, and which plays count toward Best 30.
    /// </summary>
    internal sealed class LibraryRankRulesPanel : IDisposable
    {
        private const float CARD_WIDTH = 1040f;
        private const float CARD_HEIGHT = 900f;
        private const float SCROLL_GUTTER = 16f;

        /// <summary>Kept in step with RankingRules, Best30Index and ChartRankLookup.</summary>
        internal const string RULES =
            HEADING + "哪些谱面是 Ranked" + HEADING_END +
            "· sonolus.sbuga.com 上的全部谱面\n" +
            "· opensekai.com 标为「上架」的谱面\n" +
            "· UntitledCharts 带 Staff Pick 标签的谱面\n" +
            "从文件导入、本地制作或来自其他服务器的谱面都是 Unranked。\n" +
            SECTION_GAP + HEADING + "本地曲包还要与发布版本一致" + HEADING_END +
            "· 谱面文件与发布方的版本完全相同，改动过的谱面会变成 Unranked\n" +
            "· 曲包等级与发布等级相同\n" +
            SECTION_GAP + HEADING + "如何确认状态" + HEADING_END +
            "· 下载时一并记下状态。有来源链接且状态未知的谱面可点「检查」\n" +
            "· 谱面详情的来源链接下方显示本地记录，点「刷新Ranked状态」可重新联网核验\n" +
            "· 核验收到服务器响应后，同一谱面需等十分钟再核验。网络失败可立即重试\n" +
            "· 返回数据无法解析时记为 Unranked，可从详情页重新核验\n" +
            "· 浏览曲库、打开详情、开局和结算都只读本地记录\n" +
            "· 尚未核验的谱面按 Unranked 计。无来源链接的谱面无法核验\n" +
            SECTION_GAP + HEADING + "成绩如何计入 Best 30" + HEADING_END +
            "· 资格在开局时确定，整局按开局时的状态记录\n" +
            "· 手动游玩，没有自动演奏，打完全曲且体力没有耗尽\n" +
            "· 分数达到 C 评级（900,000）\n" +
            "· 积分由谱面等级和分数决定，FC 额外 +5，AP 额外 +10\n" +
            "· 每张谱面只取最高的一次，最高的 30 张相加即为 Ranking Score\n" +
            "Unranked 谱面和不满足条件的成绩仍保存在游玩历史中。";

        private const string SECTION_GAP = "\n";
        private const string HEADING = "<size=118%><b><color=#F2F4F7>";
        private const string HEADING_END = "</color></b></size>\n";

        private readonly LibraryModal _modal;
        private readonly ScrollRect _scroll;
        private readonly RectTransform _content;
        private readonly TextMeshProUGUI _text;

        internal LibraryRankRulesPanel(Transform parent)
        {
            RectTransform root = LibraryUi.CreateRect("LibraryRankRules", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "Ranked 规则", Hide);
            RectTransform area = LibraryUi.CreateRect("Rules", _modal.Body);
            LibraryUi.Stretch(area);
            _content = PackWidgets.Scroll(area, out _scroll);
            _text = LibraryUi.CreateText("Text", _content, RULES, 21f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _text.color = LibraryTheme.Colors.TextSecondary;
            _text.richText = true;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.lineSpacing = 18f;
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Show()
        {
            _modal.Open();
            Canvas.ForceUpdateCanvases();
            float width = _content.rect.width > 0f
                ? _content.rect.width - SCROLL_GUTTER
                : CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f - SCROLL_GUTTER;
            float height = Mathf.Ceil(_text.GetPreferredValues(RULES, width, 0f).y) + 8f;
            LibraryWidgets.TopStrip(_text.rectTransform, 0f, SCROLL_GUTTER, 0f, height);
            _content.sizeDelta = new Vector2(0f, height);
            _scroll.StopMovement();
            _content.anchoredPosition = Vector2.zero;
        }

        internal void Hide() => _modal.Close();

        public void Dispose() => _modal.Dispose();
    }
}
