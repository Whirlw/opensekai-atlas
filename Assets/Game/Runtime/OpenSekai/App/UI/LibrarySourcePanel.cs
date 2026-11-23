using OpenSekai.Infrastructure.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    internal sealed class LibrarySourcePanel : System.IDisposable
    {
        private const float CARD_WIDTH = 1120f;
        private const float COPY_WIDTH = 136f;
        private readonly LibraryModal _modal;
        private readonly ScrollRect _scroll;
        private readonly RectTransform _content;
        private readonly TextMeshProUGUI _status;
        private readonly LinkRow _first;
        private readonly LinkRow _second;

        internal LibrarySourcePanel(Transform parent)
        {
            RectTransform root = LibraryUi.CreateRect("LibrarySource", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, 510f), "谱面来源", Hide, withSubtitle: true);
            _modal.Subtitle.richText = false;
            RectTransform links = LibraryUi.CreateRect("Links", _modal.Body);
            LibraryWidgets.Inset(links, 0f, 46f, 0f, 10f);
            _content = PackWidgets.Scroll(links, out _scroll);
            _status = PackWidgets.UserLabel("CopyStatus", _modal.Body, string.Empty, 21f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.BottomStrip(_status.rectTransform, 6f, 0f, 0f, 32f);
            _first = new LinkRow(_content, "FirstLink", _status);
            _second = new LinkRow(_content, "SecondLink", _status);
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void SetRankStatus(string status) => _status.text = status;

        internal void Show(SongPackageSource source, string title)
        {
            _modal.Subtitle.text = title;
            _status.text = string.Empty;
            bool web = source.PageUrl != null;
            float top = _first.Bind(web ? "来源网页" : "Sonolus 服务器", source.PageUrl ?? source.SonolusServerUrl, 0f);
            top = _second.Bind(web ? "下载直链" : "Sonolus 关卡", source.DownloadUrl ?? source.SonolusLevelUrl, top);
            _content.sizeDelta = new Vector2(0f, top);
            _modal.Root.SetAsLastSibling();
            _modal.Open();
            _scroll.StopMovement();
            _content.anchoredPosition = Vector2.zero;
        }

        internal void Hide() => _modal.Close();

        public void Dispose() => _modal.Dispose();

        private sealed class LinkRow
        {
            private readonly RectTransform _root;
            private readonly TextMeshProUGUI _caption;
            private readonly TextMeshProUGUI _url;

            internal LinkRow(Transform content, string name, TextMeshProUGUI status)
            {
                _root = LibraryUi.CreateRect(name, content);
                _caption = LibraryWidgets.Label("Caption", _root, string.Empty, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_caption.rectTransform, 6f, COPY_WIDTH + 24f, 4f, 32f);
                _url = LibraryWidgets.Label("Url", _root, string.Empty, 20f, LibraryTheme.Colors.TextSecondary,
                    alignment: TextAlignmentOptions.TopLeft);
                _url.richText = false;
                _url.textWrappingMode = TextWrappingModes.Normal;
                _url.overflowMode = TextOverflowModes.Overflow;
                UnityEngine.UI.Button copy = LibraryWidgets.PillButton("Copy", _root, "复制", () =>
                {
                    GUIUtility.systemCopyBuffer = _url.text;
                    status.text = "已复制" + _caption.text;
                }, COPY_WIDTH, 54f, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Success);
                LibraryWidgets.Place((RectTransform)copy.transform, Vector2.one, Vector2.one, new Vector2(-4f, -4f),
                    new Vector2(COPY_WIDTH, 54f));
            }

            internal float Bind(string caption, string url, float top)
            {
                _caption.text = caption;
                _url.text = url;
                float width = CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f - COPY_WIDTH - 42f;
                float height = Mathf.Max(32f, _url.GetPreferredValues(url, width, 0f).y + 4f);
                LibraryWidgets.TopStrip(_root, 0f, 0f, top, height + 60f);
                LibraryWidgets.TopStrip(_url.rectTransform, 6f, COPY_WIDTH + 24f, 38f, height);
                return top + height + 60f;
            }
        }
    }
}
