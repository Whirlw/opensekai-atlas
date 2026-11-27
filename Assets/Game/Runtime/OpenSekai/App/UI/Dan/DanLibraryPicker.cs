using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    internal sealed class DanLibraryPicker : IDisposable
    {
        private const float ROW_HEIGHT = 62f;
        private const float ROW_PITCH = ROW_HEIGHT + 8f;
        private readonly LibraryModal _modal;
        private readonly TMP_InputField _search;
        private readonly TextMeshProUGUI _message;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private readonly Action<SongPackageItem> _pick;
        private readonly Func<SongPackageItem, bool> _isCandidate;
        private readonly List<PickerRow> _rows = new();
        private SongPackageItem[] _items = Array.Empty<SongPackageItem>();
        private SongPackageItem[] _matches = Array.Empty<SongPackageItem>();

        internal DanLibraryPicker(RectTransform parent, Action<SongPackageItem> pick,
            Func<SongPackageItem, bool> isCandidate = null, string title = "从曲库添加")
        {
            _isCandidate = isCandidate ?? DanLibrarySong.IsCandidate;
            _pick = pick;
            RectTransform root = LibraryUi.CreateRect("DanLibraryPicker", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(1000f, 760f), title, Close);
            _search = LibraryWidgets.TextField("Search", _modal.Body, 58f, "搜索曲名或谱师", LibraryIcon.Search, 22f);
            _search.textComponent.richText = false;
            LibraryWidgets.TopStrip((RectTransform)_search.transform, 0f, 0f, 0f, 58f);
            _search.onValueChanged.AddListener(Filter);
            RectTransform list = LibraryUi.CreateRect("List", _modal.Body);
            LibraryWidgets.Inset(list, 0f, 44f, 0f, 74f);
            _content = PackWidgets.Scroll(list, out _scroll);
            _scroll.onValueChanged.AddListener(_ => BindRows());
            _message = PackWidgets.UserLabel("Status", _modal.Body, string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.BottomStrip(_message.rectTransform, 6f, 0f, 0f, 32f);
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Show() => Show(SongPackageManager.Shared.LoadItems);

        internal void Show(Func<IReadOnlyList<SongPackageItem>> loadItems)
        {
            string error = null;
            try { _items = loadItems().Where(_isCandidate).ToArray(); }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                _items = Array.Empty<SongPackageItem>();
                error = "无法读取曲库";
            }

            _search.SetTextWithoutNotify(string.Empty);
            _modal.Open();
            Filter(string.Empty);
            if (error != null) SetMessage(error);
        }

        internal void Close()
        {
            _search.DeactivateInputField();
            _modal.Close();
        }

        internal void SetMessage(string message) => _message.text = message;

        private void Filter(string text)
        {
            string query = text.Trim();
            _matches = query.Length == 0 ? _items : _items.Where(item =>
                Matches(LibraryModel.BuildTitle(item), query) || Matches(item.Entry.Manifest.scoreTitle, query)
                    || Matches(item.Entry.Manifest.userName, query)).ToArray();
            _scroll.StopMovement();
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, _matches.Length * ROW_PITCH);
            SetMessage(_matches.Length == 0 ? "没有符合条件的谱面" : string.Empty);
            BindRows();
        }

        private static bool Matches(string value, string query) =>
            value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void BindRows()
        {
            int visible = Mathf.CeilToInt(_scroll.viewport.rect.height / ROW_PITCH) + 2;
            int count = Mathf.Min(visible, _matches.Length);
            int first = Mathf.Clamp(Mathf.FloorToInt(_content.anchoredPosition.y / ROW_PITCH), 0, _matches.Length - count);
            while (_rows.Count < count) _rows.Add(new PickerRow(_content, _pick));
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Root.gameObject.SetActive(i < count);
                if (i < count) _rows[i].Bind(_matches[first + i], (first + i) * ROW_PITCH);
            }
        }

        public void Dispose() => _modal.Dispose();

        private sealed class PickerRow
        {
            internal RectTransform Root { get; }
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _difficulty;
            private SongPackageItem _item;

            internal PickerRow(RectTransform parent, Action<SongPackageItem> pick)
            {
                UnityEngine.UI.Button button = LibraryWidgets.PillButton("Chart", parent, string.Empty, () => pick(_item),
                    0f, ROW_HEIGHT, LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.None);
                Root = (RectTransform)button.transform;
                _title = PackWidgets.UserLabel("Title", Root, string.Empty, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.Inset(_title.rectTransform, 24f, 0f, 210f, 0f);
                _difficulty = PackWidgets.UserLabel("Difficulty", Root, string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
                _difficulty.alignment = TextAlignmentOptions.Right;
                LibraryWidgets.Place(_difficulty.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-24f, 0f), new Vector2(176f, ROW_HEIGHT));
            }

            internal void Bind(SongPackageItem item, float top)
            {
                LibraryWidgets.TopStrip(Root, 0f, 0f, top, ROW_HEIGHT);
                if (_item == item) return;
                _item = item;
                _title.text = LibraryModel.BuildTitle(item);
                int level = LibraryModel.PlayLevelOf(item);
                _difficulty.text = LibraryModel.DifficultyLabel(item) + (level > 0 ? " Lv." + level : string.Empty);
            }
        }
    }
}
