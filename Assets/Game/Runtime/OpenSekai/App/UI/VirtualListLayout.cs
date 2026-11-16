using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Geometry of a vertically scrolling list of equally sized cells, in one or more columns.
    /// Pure math, no scene objects: <see cref="VirtualListView"/> asks it which indices are on
    /// screen and where each cell goes. Distances grow downward from the top of the content.
    /// </summary>
    internal readonly struct VirtualListLayout
    {
        internal VirtualListLayout(
            int columns, float cellWidth, float cellHeight, float spacingX, float spacingY,
            float paddingTop = 0f, float paddingBottom = 0f, float paddingLeft = 0f)
        {
            Columns = Mathf.Max(1, columns);
            CellWidth = Mathf.Max(0f, cellWidth);
            CellHeight = Mathf.Max(1f, cellHeight);
            SpacingX = Mathf.Max(0f, spacingX);
            SpacingY = Mathf.Max(0f, spacingY);
            PaddingTop = Mathf.Max(0f, paddingTop);
            PaddingBottom = Mathf.Max(0f, paddingBottom);
            PaddingLeft = Mathf.Max(0f, paddingLeft);
        }

        internal int Columns { get; }
        internal float CellWidth { get; }
        internal float CellHeight { get; }
        internal float SpacingX { get; }
        internal float SpacingY { get; }
        internal float PaddingTop { get; }
        internal float PaddingBottom { get; }
        internal float PaddingLeft { get; }

        private float RowPitch => CellHeight + SpacingY;

        /// <summary>Single column of rows that fill the width left between the side paddings.</summary>
        internal static VirtualListLayout Rows(
            float width, float rowHeight, float spacing, float paddingTop = 0f, float paddingBottom = 0f,
            float paddingLeft = 0f, float paddingRight = 0f)
        {
            float cellWidth = Mathf.Max(0f, width - paddingLeft - paddingRight);
            return new VirtualListLayout(1, cellWidth, rowHeight, 0f, spacing, paddingTop, paddingBottom, paddingLeft);
        }

        /// <summary>
        /// Equal columns filling the width left between the side paddings; each cell is a square
        /// cover plus <paramref name="extraCellHeight"/> for whatever sits under it.
        /// </summary>
        internal static VirtualListLayout Grid(
            float width, int columns, float spacingX, float spacingY, float extraCellHeight,
            float paddingTop = 0f, float paddingBottom = 0f, float paddingLeft = 0f, float paddingRight = 0f)
        {
            columns = Mathf.Max(1, columns);
            float cellWidth = Mathf.Max(0f, (width - paddingLeft - paddingRight - spacingX * (columns - 1)) / columns);
            return new VirtualListLayout(
                columns, cellWidth, cellWidth + Mathf.Max(0f, extraCellHeight), spacingX, spacingY,
                paddingTop, paddingBottom, paddingLeft);
        }

        internal int RowCount(int itemCount)
        {
            return itemCount <= 0 ? 0 : (itemCount + Columns - 1) / Columns;
        }

        internal float ContentHeight(int itemCount)
        {
            int rows = RowCount(itemCount);
            if (rows == 0)
            {
                return 0f;
            }

            return PaddingTop + rows * CellHeight + (rows - 1) * SpacingY + PaddingBottom;
        }

        /// <summary>Anchored position of a cell whose anchors and pivot are the content's top-left corner.</summary>
        internal Vector2 PositionOf(int index)
        {
            int row = index / Columns;
            int column = index % Columns;
            return new Vector2(PaddingLeft + column * (CellWidth + SpacingX), -(PaddingTop + row * RowPitch));
        }

        /// <summary>
        /// Half-open index range <c>[first, end)</c> of the cells that intersect the viewport, widened
        /// by <paramref name="bufferRows"/> rows on both sides so a fast scroll has cells ready.
        /// </summary>
        internal void VisibleRange(
            float scrollOffset, float viewportHeight, int itemCount, int bufferRows, out int first, out int end)
        {
            first = 0;
            end = 0;
            int rows = RowCount(itemCount);
            if (rows == 0 || viewportHeight <= 0f)
            {
                return;
            }

            // Row r spans [PaddingTop + r * pitch, PaddingTop + r * pitch + CellHeight).
            int firstRow = Mathf.FloorToInt((scrollOffset - PaddingTop - CellHeight) / RowPitch) + 1;
            int lastRow = Mathf.CeilToInt((scrollOffset + viewportHeight - PaddingTop) / RowPitch) - 1;
            firstRow = Mathf.Max(0, firstRow - Mathf.Max(0, bufferRows));
            lastRow = Mathf.Min(rows - 1, lastRow + Mathf.Max(0, bufferRows));
            if (firstRow > lastRow)
            {
                return;
            }

            first = firstRow * Columns;
            end = Mathf.Min(itemCount, (lastRow + 1) * Columns);
        }

        /// <summary>
        /// Scroll offset that brings a cell fully into view with the smallest move; returns
        /// <paramref name="scrollOffset"/> unchanged when the cell is already visible.
        /// </summary>
        internal float OffsetToReveal(int index, float scrollOffset, float viewportHeight, int itemCount)
        {
            if (index < 0 || index >= itemCount)
            {
                return scrollOffset;
            }

            float top = PaddingTop + (index / Columns) * RowPitch;
            float bottom = top + CellHeight;
            float target = scrollOffset;
            if (top - PaddingTop < scrollOffset)
            {
                target = top - PaddingTop;
            }
            else if (bottom + PaddingBottom > scrollOffset + viewportHeight)
            {
                target = bottom + PaddingBottom - viewportHeight;
            }

            float max = Mathf.Max(0f, ContentHeight(itemCount) - viewportHeight);
            return Mathf.Clamp(target, 0f, max);
        }
    }
}
