using NUnit.Framework;
using OpenSekai.App.UI;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class VirtualListLayoutTests
    {
        private static readonly VirtualListLayout _rows = VirtualListLayout.Rows(800f, 100f, 8f);
        private static readonly VirtualListLayout _grid = VirtualListLayout.Grid(860f, 4, 20f, 24f, 38f);

        [Test]
        public void ContentHeightCountsRowsAndTheGapsBetweenThem()
        {
            Assert.That(_rows.ContentHeight(0), Is.EqualTo(0f));
            Assert.That(_rows.ContentHeight(1), Is.EqualTo(100f));
            Assert.That(_rows.ContentHeight(10), Is.EqualTo(1072f));
        }

        [Test]
        public void GridSplitsTheWidthIntoSquareCoversPlusTheTitleStrip()
        {
            Assert.That(_grid.CellWidth, Is.EqualTo(200f));
            Assert.That(_grid.CellHeight, Is.EqualTo(238f));
            Assert.That(_grid.RowCount(10), Is.EqualTo(3));
            Assert.That(_grid.ContentHeight(10), Is.EqualTo(762f));
            Assert.That(_grid.PositionOf(5), Is.EqualTo(new Vector2(220f, -262f)));
        }

        [Test]
        public void VisibleRangeCoversOnlyRowsThatTouchTheViewport()
        {
            _rows.VisibleRange(0f, 400f, 1000, 0, out int first, out int end);
            Assert.That((first, end), Is.EqualTo((0, 4)));

            // Row 0 ends exactly where the viewport starts, so it is no longer visible.
            _rows.VisibleRange(100f, 400f, 1000, 0, out first, out end);
            Assert.That((first, end), Is.EqualTo((1, 5)));
        }

        [Test]
        public void VisibleRangeAddsBufferRowsWithoutLeavingTheList()
        {
            _rows.VisibleRange(0f, 400f, 1000, 1, out int first, out int end);
            Assert.That((first, end), Is.EqualTo((0, 5)));

            _rows.VisibleRange(1080f, 400f, 1000, 1, out first, out end);
            Assert.That((first, end), Is.EqualTo((9, 15)));
        }

        [Test]
        public void VisibleRangeEndsAtTheLastItemOfAPartialGridRow()
        {
            _grid.VisibleRange(0f, 300f, 10, 0, out int first, out int end);
            Assert.That((first, end), Is.EqualTo((0, 8)));

            _grid.VisibleRange(500f, 300f, 10, 0, out first, out end);
            Assert.That((first, end), Is.EqualTo((8, 10)));
        }

        [Test]
        public void VisibleRangeIsEmptyWithoutItemsViewportOrOverlap()
        {
            _rows.VisibleRange(0f, 400f, 0, 1, out int first, out int end);
            Assert.That(end - first, Is.EqualTo(0));

            _rows.VisibleRange(0f, 0f, 10, 1, out first, out end);
            Assert.That(end - first, Is.EqualTo(0));

            _rows.VisibleRange(5000f, 400f, 10, 0, out first, out end);
            Assert.That(end - first, Is.EqualTo(0));
        }

        [Test]
        public void OffsetToRevealMovesOnlyAsFarAsNeeded()
        {
            Assert.That(_rows.OffsetToReveal(1, 0f, 400f, 20), Is.EqualTo(0f), "already visible");
            Assert.That(_rows.OffsetToReveal(0, 300f, 400f, 20), Is.EqualTo(0f), "above the viewport");
            Assert.That(_rows.OffsetToReveal(10, 0f, 400f, 20), Is.EqualTo(780f), "below the viewport");
            Assert.That(_rows.OffsetToReveal(19, 0f, 400f, 20), Is.EqualTo(1752f), "last row stops at the end");
            Assert.That(_rows.OffsetToReveal(99, 120f, 400f, 20), Is.EqualTo(120f), "unknown index");
        }
    }
}
