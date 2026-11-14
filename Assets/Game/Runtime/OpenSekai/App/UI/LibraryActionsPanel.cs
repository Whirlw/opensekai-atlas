using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Per-package action card: 导出 ZIP / 删除曲包. Delete asks for a second click before it
    /// runs, so a stray tap cannot remove a package.
    /// </summary>
    public sealed class LibraryActionsPanel : MonoBehaviour
    {
        private const float CARD_WIDTH = 760f;
        private const float CARD_HEIGHT = 360f;

        private RectTransform _card;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _hintText;
        private Button _exportButton;
        private Button _deleteButton;
        private Button _cancelButton;

        private Action _onExport;
        private Action _onDelete;
        private bool _deleteArmed;

        public static LibraryActionsPanel Create(Transform parent)
        {
            GameObject go = new GameObject("LibraryActionsPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            LibraryUi.Stretch(rect);

            LibraryActionsPanel panel = go.AddComponent<LibraryActionsPanel>();
            panel.Build();
            return panel;
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform root = (RectTransform)transform;

            Image scrim = LibraryUi.CreateImage("Scrim", root, new Color32(0, 0, 0, 150));
            LibraryUi.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;

            Image cardImage = LibraryUi.CreateImage("Card", root, new Color32(24, 29, 36, 245));
            _card = cardImage.rectTransform;
            _card.anchorMin = new Vector2(0.5f, 0.5f);
            _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CARD_WIDTH, CARD_HEIGHT);

            _titleText = LibraryUi.CreateText("Title", _card, string.Empty, 32f, FontStyles.Bold, TextAlignmentOptions.Center);
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _titleText.rectTransform.offsetMin = new Vector2(32f, -60f);
            _titleText.rectTransform.offsetMax = new Vector2(-32f, -20f);

            _hintText = LibraryUi.CreateText("Hint", _card, string.Empty, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
            _hintText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _hintText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _hintText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _hintText.rectTransform.offsetMin = new Vector2(32f, -100f);
            _hintText.rectTransform.offsetMax = new Vector2(-32f, -64f);
            _hintText.color = new Color32(150, 160, 174, 255);

            _exportButton = LibraryUi.CreateButton("Export", _card, "导出 ZIP", OnExportClicked, 560f, 56f);
            PlaceButton((RectTransform)_exportButton.transform, -132f);

            _deleteButton = LibraryUi.CreateButton("Delete", _card, "删除曲包", OnDeleteClicked, 560f, 56f);
            PlaceButton((RectTransform)_deleteButton.transform, -200f);

            _cancelButton = LibraryUi.CreateButton("Cancel", _card, "取消", Hide, 560f, 56f);
            PlaceButton((RectTransform)_cancelButton.transform, -268f);

            gameObject.SetActive(false);
        }

        private static void PlaceButton(RectTransform rect, float topOffset)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, topOffset);
        }

        public void Show(string title, string hint, Action export, Action delete)
        {
            _titleText.text = title;
            _hintText.text = hint;
            _onExport = export;
            _onDelete = delete;
            _deleteArmed = false;
            _deleteButton.GetComponentInChildren<TextMeshProUGUI>().text = "删除曲包";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            _onExport = null;
            _onDelete = null;
            _deleteArmed = false;
            gameObject.SetActive(false);
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
                _deleteArmed = true;
                _deleteButton.GetComponentInChildren<TextMeshProUGUI>().text = "再点一次确认删除";
                _hintText.text = "删除后只能重新导入，无法撤销。";
                return;
            }

            Action action = _onDelete;
            Hide();
            action?.Invoke();
        }
    }
}
