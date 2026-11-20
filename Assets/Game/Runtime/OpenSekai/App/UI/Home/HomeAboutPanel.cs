using TMPro;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 关于: the emblem, the name and version, one line on what the app is, and where its code,
    /// fonts, sounds and art come from (the same sources docs/operations/asset-provenance.md lists).
    /// </summary>
    internal sealed class HomeAboutPanel
    {
        private const float CARD_WIDTH = 940f;
        private const float CARD_HEIGHT = 640f;
        private const float EMBLEM_SIZE = 176f;

        private static readonly string[,] Credits =
        {
            { "代码", "MIT License" },
            { "字体", "思源黑体 · Noto Sans SC · Barlow Condensed（SIL OFL 1.1）" },
            { "音频", "自制音效（CC0）· uisfx「zen」界面音效（CC0）" },
            { "美术", "音符皮肤、HUD、特效与图标均为自制或程序生成" }
        };

        private readonly LibraryModal _modal;
        private readonly BrandEmblem _emblem;

        internal HomeAboutPanel(RectTransform parent)
        {
            RectTransform root = LibraryUi.CreateRect("About", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "关于", Hide);
            RectTransform body = _modal.Body;
            Vector2 topLeft = new Vector2(0f, 1f);

            RectTransform emblemFrame = LibraryUi.CreateRect("EmblemFrame", body);
            LibraryWidgets.Place(emblemFrame, topLeft, topLeft, new Vector2(-8f, 6f), new Vector2(EMBLEM_SIZE, EMBLEM_SIZE));
            _emblem = new BrandEmblem(emblemFrame, "Emblem");
            LibraryUi.Stretch(_emblem.Root);

            float textLeft = EMBLEM_SIZE + 24f;
            TextMeshProUGUI name = LibraryWidgets.Label("Name", body, HomeView.Wordmark, 54f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(name);
            name.characterSpacing = 12f;
            name.richText = true;
            // The condensed face's line is taller than its capitals; ellipsising would hide it.
            name.overflowMode = TextOverflowModes.Overflow;
            Strip(name.rectTransform, textLeft, 0f, 72f);

            TextMeshProUGUI version = LibraryWidgets.Label(
                "Version", body, "版本 " + Application.version, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            Strip(version.rectTransform, textLeft, 72f, 30f);

            TextMeshProUGUI summary = LibraryWidgets.Label(
                "Summary", body, "开源下落式音游，支持导入本地曲包。\n在 Sonolus 搜谱、试听或直接游玩。\n点「下载」加入曲库，随时离线打歌。", 22f,
                LibraryTheme.Colors.TextSecondary);
            summary.textWrappingMode = TextWrappingModes.Normal;
            summary.overflowMode = TextOverflowModes.Overflow;
            Strip(summary.rectTransform, textLeft, 106f, 88f);

            RectTransform list = LibraryUi.CreateRect("Credits", body);
            LibraryWidgets.Inset(list, 0f, 0f, 0f, EMBLEM_SIZE + 36f);
            float rowHeight = LibraryTheme.Layout.SETTING_ROW_HEIGHT - 8f;
            for (int i = 0; i < Credits.GetLength(0); i++)
            {
                RectTransform row = LibraryUi.CreateRect("Row" + i, list);
                LibraryWidgets.TopStrip(row, 0f, 0f, i * rowHeight, rowHeight);
                if (i > 0)
                {
                    UnityEngine.UI.Image line = LibraryUi.CreateImage("Divider", row, LibraryTheme.Colors.Hairline);
                    line.raycastTarget = false;
                    LibraryWidgets.TopStrip(line.rectTransform, 0f, 0f, 0f, 1f);
                }

                TextMeshProUGUI label = LibraryWidgets.Label("Label", row, Credits[i, 0], LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = new Vector2(96f, 0f);
                TextMeshProUGUI value = LibraryWidgets.Label("Value", row, Credits[i, 1], LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextPrimary);
                LibraryWidgets.Inset(value.rectTransform, 96f, 0f, 0f, 0f);
            }

            root.gameObject.SetActive(false);

            void Strip(RectTransform rect, float left, float top, float height)
            {
                rect.anchorMin = topLeft;
                rect.anchorMax = Vector2.one;
                rect.pivot = topLeft;
                rect.offsetMin = new Vector2(left, -top - height);
                rect.offsetMax = new Vector2(0f, -top);
            }
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Show() => _modal.Open();

        internal void Hide() => _modal.Close();

        internal void ApplySprites(BrandSprites sprites) => _emblem.ApplySprites(sprites);

        internal void Dispose() => _modal.Dispose();
    }
}
