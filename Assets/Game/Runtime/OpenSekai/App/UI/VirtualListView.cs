using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Recycling list for a <see cref="ScrollRect"/>: only the cells near the viewport exist, and a
    /// cell that scrolls out is handed to the next index that scrolls in. The content needs no
    /// LayoutGroup or ContentSizeFitter; cells are positioned from <see cref="VirtualListLayout"/>.
    /// </summary>
    internal sealed class VirtualListView : MonoBehaviour
    {
        private const int BUFFER_ROWS = 1;

        private readonly Dictionary<int, RectTransform> _activeCells = new Dictionary<int, RectTransform>();
        private readonly Stack<RectTransform> _pooledCells = new Stack<RectTransform>();
        private readonly List<int> _releaseScratch = new List<int>();

        private ScrollRect _scroll;
        private RectTransform _viewport;
        private RectTransform _content;
        private Func<float, VirtualListLayout> _layoutForWidth;
        private Func<RectTransform, RectTransform> _createCell;
        private Action<RectTransform, int> _bindCell;
        private Action<RectTransform> _releaseCell;
        private VirtualListLayout _layout;
        private Vector2 _viewportSize;
        private int _itemCount;
        private int _firstActive;
        private int _endActive;

        internal int ItemCount => _itemCount;

        /// <summary>First index that has a live cell.</summary>
        internal int FirstActiveIndex => _firstActive;

        /// <summary>One past the last index that has a live cell.</summary>
        internal int EndActiveIndex => _endActive;
        internal int ActiveCellCount => _activeCells.Count;
        internal int PooledCellCount => _pooledCells.Count;

        /// <summary>
        /// Takes over the content of <paramref name="scroll"/>.
        /// </summary>
        /// <param name="scroll">Scroll rect whose viewport and content are already assigned.</param>
        /// <param name="layoutForWidth">Builds the layout for the current viewport width.</param>
        /// <param name="createCell">Creates one cell under the given content transform.</param>
        /// <param name="bindCell">Fills a cell with the item at an index.</param>
        /// <param name="releaseCell">Called when a cell leaves the screen, before it is pooled.</param>
        internal static VirtualListView Attach(
            ScrollRect scroll,
            Func<float, VirtualListLayout> layoutForWidth,
            Func<RectTransform, RectTransform> createCell,
            Action<RectTransform, int> bindCell,
            Action<RectTransform> releaseCell = null)
        {
            VirtualListView view = scroll.gameObject.AddComponent<VirtualListView>();
            view._scroll = scroll;
            view._viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            view._content = scroll.content;
            view._layoutForWidth = layoutForWidth;
            view._createCell = createCell;
            view._bindCell = bindCell;
            view._releaseCell = releaseCell;

            view._content.anchorMin = new Vector2(0f, 1f);
            view._content.anchorMax = new Vector2(1f, 1f);
            view._content.pivot = new Vector2(0f, 1f);
            view._content.anchoredPosition = Vector2.zero;

            scroll.onValueChanged.AddListener(view.ScrollRect_OnValueChanged);
            view.Rebuild();
            return view;
        }

        /// <summary>Changes the number of items and rebinds every visible cell.</summary>
        internal void SetItemCount(int count, bool keepScrollPosition = false)
        {
            _itemCount = Mathf.Max(0, count);
            if (!keepScrollPosition)
            {
                _content.anchoredPosition = Vector2.zero;
                _scroll.velocity = Vector2.zero;
            }

            Rebuild();
        }

        /// <summary>Releases every cell, recomputes the layout from the viewport and binds the visible range again.</summary>
        internal void Rebuild()
        {
            ReleaseAll();
            _viewportSize = _viewport.rect.size;
            _layout = _layoutForWidth(_viewportSize.x);
            _content.sizeDelta = new Vector2(0f, _layout.ContentHeight(_itemCount));
            ClampScroll();
            Refresh();
        }

        /// <summary>Syncs the live cells with the current scroll position; cheap when the range did not change.</summary>
        internal void Refresh()
        {
            if (_content == null)
            {
                return;
            }

            _layout.VisibleRange(
                _content.anchoredPosition.y, _viewportSize.y, _itemCount, BUFFER_ROWS, out int first, out int end);
            _firstActive = first;
            _endActive = end;

            _releaseScratch.Clear();
            foreach (KeyValuePair<int, RectTransform> pair in _activeCells)
            {
                if (pair.Key < first || pair.Key >= end)
                {
                    _releaseScratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < _releaseScratch.Count; i++)
            {
                Release(_releaseScratch[i]);
            }

            for (int index = first; index < end; index++)
            {
                if (!_activeCells.ContainsKey(index))
                {
                    Bind(index);
                }
            }
        }

        /// <summary>Scrolls just far enough to bring an item fully into view.</summary>
        internal void ScrollTo(int index)
        {
            float offset = _layout.OffsetToReveal(index, _content.anchoredPosition.y, _viewportSize.y, _itemCount);
            _scroll.velocity = Vector2.zero;
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, offset);
            Refresh();
        }

        /// <summary>The live cell showing an index, or null when that index is off screen.</summary>
        internal RectTransform CellAt(int index)
        {
            return _activeCells.TryGetValue(index, out RectTransform cell) ? cell : null;
        }

        private void LateUpdate()
        {
            // The canvas sizes the viewport after this page is built (and again on rotation or a
            // resolution change), so the layout follows the viewport instead of assuming a size.
            if (_viewport != null && _viewport.rect.size != _viewportSize)
            {
                Rebuild();
            }
        }

        private void OnDestroy()
        {
            if (_scroll != null)
            {
                _scroll.onValueChanged.RemoveListener(ScrollRect_OnValueChanged);
            }
        }

        private void ScrollRect_OnValueChanged(Vector2 _)
        {
            Refresh();
        }

        private void Bind(int index)
        {
            RectTransform cell = _pooledCells.Count > 0 ? _pooledCells.Pop() : CreateCell();
            cell.sizeDelta = new Vector2(_layout.CellWidth, _layout.CellHeight);
            cell.anchoredPosition = _layout.PositionOf(index);
            cell.gameObject.SetActive(true);
            _activeCells[index] = cell;
            _bindCell(cell, index);
        }

        private RectTransform CreateCell()
        {
            RectTransform cell = _createCell(_content);
            cell.anchorMin = new Vector2(0f, 1f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 1f);
            return cell;
        }

        private void Release(int index)
        {
            RectTransform cell = _activeCells[index];
            _activeCells.Remove(index);
            _releaseCell?.Invoke(cell);
            cell.gameObject.SetActive(false);
            _pooledCells.Push(cell);
        }

        private void ReleaseAll()
        {
            _releaseScratch.Clear();
            _releaseScratch.AddRange(_activeCells.Keys);
            for (int i = 0; i < _releaseScratch.Count; i++)
            {
                Release(_releaseScratch[i]);
            }
        }

        private void ClampScroll()
        {
            float max = Mathf.Max(0f, _content.sizeDelta.y - _viewportSize.y);
            Vector2 position = _content.anchoredPosition;
            position.y = Mathf.Clamp(position.y, 0f, max);
            _content.anchoredPosition = position;
        }
    }
}
