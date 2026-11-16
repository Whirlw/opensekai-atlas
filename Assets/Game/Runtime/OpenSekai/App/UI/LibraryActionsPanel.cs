using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Per-package action card: the package's jacket and title over two large actions, 导出曲包
    /// and 删除曲包. Delete asks for a second tap before it runs — the row turns red and says so —
    /// so a stray tap cannot remove a package.
    /// </summary>
    public sealed class LibraryActionsPanel : MonoBehaviour
    {
        private const float CARD_WIDTH = 780f;
        private const float CARD_HEIGHT = 408f;
        private const float COVER_SIZE = 64f;
        private const float ACTION_HEIGHT = 116f;
        private const float ACTION_GAP = 16f;
        private const float ICON_SIZE = 64f;

        private const string DELETE_TITLE = "删除曲包";
        private const string DELETE_BODY = "从曲库移除这个曲包，之后只能重新导入";
        private const string ARMED_TITLE = "再点一次确认删除";
        private const string ARMED_BODY = "此操作无法撤销";

        private LibraryModal _modal;
        private Image _cover;
        private Image _coverPlaceholder;
        private Image _exportIconBackground;
        private Image _exportIcon;
        private Image _deleteBackground;
        private Image _deleteOutline;
        private Image _deleteIconBackground;
        private Image _deleteIcon;
        private TextMeshProUGUI _deleteTitle;
        private TextMeshProUGUI _deleteBody;
        private RectTransform _deleteRow;
        private MotionHandle _armMotion;

        private Action _onExport;
        private Action _onDelete;
        private bool _deleteArmed;

        public static LibraryActionsPanel Create(Transform parent)
        {
            RectTransform rect = LibraryUi.CreateRect("LibraryActionsPanel", parent);
            LibraryUi.Stretch(rect);
            LibraryActionsPanel panel = rect.gameObject.AddComponent<LibraryActionsPanel>();
            panel.Build();
            return panel;
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;
            gameObject.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)transform;
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), string.Empty, Hide, withSubtitle: true);

            // The jacket sits in front of the title, so the card names the package at a glance.
            float padding = LibraryTheme.Layout.MODAL_PADDING;
            float header = LibraryTheme.Layout.MODAL_HEADER_HEIGHT;
            RectTransform coverFrame = LibraryUi.CreateRect("Cover", _modal.Card);
            LibraryWidgets.Place(coverFrame, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(padding, -header * 0.5f - 2f), new Vector2(COVER_SIZE, COVER_SIZE));
            Image frame = LibraryWidgets.Panel("Frame", coverFrame, LibraryTheme.Radius.COVER_SMALL, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _coverPlaceholder = LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.TextMuted, 28f);
            _cover = LibraryUi.CreateImage("Jacket", frame.transform, Color.white);
            _cover.raycastTarget = false;
            LibraryUi.Stretch(_cover.rectTransform);
            float textLeft = padding + COVER_SIZE + 20f;
            _modal.Title.rectTransform.offsetMin = new Vector2(textLeft, _modal.Title.rectTransform.offsetMin.y);
            _modal.Subtitle.rectTransform.offsetMin = new Vector2(textLeft, _modal.Subtitle.rectTransform.offsetMin.y);
            _modal.Title.fontSize = 30f;

            RectTransform body = _modal.Body;
            body.offsetMax = new Vector2(body.offsetMax.x, -header - 8f);

            Image export = ActionRow(body, 0, "Export", LibraryIcon.Export, "导出曲包", "保存为 .oskp 文件，用于备份或分享",
                OnExportClicked, out _exportIconBackground, out _exportIcon, out _, out _);
            LibraryWidgets.Outline(export, LibraryTheme.Radius.ROW);

            _deleteBackground = ActionRow(body, 1, "Delete", LibraryIcon.Trash, DELETE_TITLE, DELETE_BODY,
                OnDeleteClicked, out _deleteIconBackground, out _deleteIcon, out _deleteTitle, out _deleteBody);
            _deleteOutline = LibraryWidgets.Shape("Outline", _deleteBackground.transform,
                LibraryShapes.Ring(LibraryTheme.Radius.ROW, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(_deleteOutline.rectTransform);
            _deleteRow = _deleteBackground.rectTransform;
            _deleteTitle.color = LibraryTheme.Colors.Danger;
            _deleteIcon.color = LibraryTheme.Colors.Danger;

            _modal.Close();
        }

        private static Image ActionRow(
            RectTransform body, int index, string name, LibraryIcon icon, string title, string description,
            UnityEngine.Events.UnityAction onClick,
            out Image iconBackground, out Image iconImage, out TextMeshProUGUI titleText, out TextMeshProUGUI bodyText)
        {
            Button button = LibraryWidgets.PillButton(
                name, body, string.Empty, onClick, 0f, ACTION_HEIGHT, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary);
            Image background = (Image)button.targetGraphic;
            background.sprite = LibraryShapes.RoundedRect(LibraryTheme.Radius.ROW);
            LibraryWidgets.TopStrip(background.rectTransform, 0f, 0f, index * (ACTION_HEIGHT + ACTION_GAP), ACTION_HEIGHT);

            iconBackground = LibraryWidgets.Panel("IconBackground", background.transform, ICON_SIZE * 0.5f, LibraryTheme.Colors.Control);
            LibraryWidgets.Place(iconBackground.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(26f, 0f), new Vector2(ICON_SIZE, ICON_SIZE));
            iconImage = LibraryWidgets.Icon(iconBackground.transform, icon, LibraryTheme.Colors.TextPrimary, 30f);

            float textLeft = 26f + ICON_SIZE + 22f;
            titleText = LibraryWidgets.Label("Title", background.transform, title, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Place(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f),
                new Vector2(textLeft, 2f), new Vector2(560f, 34f));
            bodyText = LibraryWidgets.Label("Body", background.transform, description, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(bodyText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f),
                new Vector2(textLeft, -4f), new Vector2(560f, 28f));

            Image chevron = LibraryWidgets.Icon(background.transform, LibraryIcon.ChevronRight, LibraryTheme.Colors.TextMuted, 26f);
            LibraryWidgets.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-26f, 0f), new Vector2(26f, 26f));
            return background;
        }

        /// <param name="title">The package's title.</param>
        /// <param name="subtitle">Difficulty, level and charter under the title.</param>
        /// <param name="cover">The package's jacket, when one is already decoded.</param>
        /// <param name="accent">Color of the export icon; follows the jacket.</param>
        internal void Show(string title, string subtitle, Sprite cover, Color32 accent, Action export, Action delete)
        {
            _modal.Title.text = title;
            _modal.Subtitle.text = subtitle;
            _cover.sprite = cover;
            _cover.enabled = cover != null;
            _coverPlaceholder.enabled = cover == null;
            _exportIcon.color = accent;
            _exportIconBackground.color = WithAlpha(accent, 36);
            _onExport = export;
            _onDelete = delete;
            SetArmed(false, false);
            _modal.Open();
        }

        public void Hide()
        {
            _onExport = null;
            _onDelete = null;
            SetArmed(false, false);
            _modal.Close();
        }

        private void OnDestroy()
        {
            _armMotion.TryCancel();
            _modal?.Dispose();
        }

        private void OnExportClicked()
        {
            Action action = _onExport;
            Hide();
            action?.Invoke();
        }

        private void OnDeleteClicked()
        {
            if (!_deleteArmed)
            {
                SetArmed(true, true);
                return;
            }

            Action action = _onDelete;
            Hide();
            action?.Invoke();
        }

        private void SetArmed(bool armed, bool animate)
        {
            _deleteArmed = armed;
            _deleteTitle.text = armed ? ARMED_TITLE : DELETE_TITLE;
            _deleteBody.text = armed ? ARMED_BODY : DELETE_BODY;
            _deleteBody.color = armed ? LibraryTheme.Colors.Danger : LibraryTheme.Colors.TextMuted;
            _deleteBackground.color = armed ? LibraryTheme.Colors.DangerSurface : LibraryTheme.Colors.Control;
            _deleteOutline.color = armed ? WithAlpha(LibraryTheme.Colors.Danger, 110) : LibraryTheme.Colors.Hairline;
            _deleteIconBackground.color = WithAlpha(LibraryTheme.Colors.Danger, (byte)(armed ? 60 : 30));

            _armMotion.TryCancel();
            _armMotion = default;
            _deleteRow.localRotation = Quaternion.identity;
            if (animate)
            {
                // A short shake says "this one is different now" without a second dialog.
                _armMotion = LMotion.Punch.Create(0f, 1.6f, 0.36f)
                    .WithFrequency(8)
                    .WithDampingRatio(1f)
                    .Bind(_deleteRow, static (z, row) => row.localRotation = Quaternion.Euler(0f, 0f, z));
            }
        }

        private static Color32 WithAlpha(Color32 color, byte alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
