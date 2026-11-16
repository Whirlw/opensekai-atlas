using NUnit.Framework;
using OpenSekai.App.UI;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class VirtualListLayoutTests
    {
        private static readonly VirtualListLayout Rows = VirtualListLayout.Rows(800f, 100f, 8f);
        private static readonly VirtualListLayout Grid = VirtualListLayout.Grid(860f, 4, 20f, 24f, 38f);

        [Test]
        public void ContentHeightCountsRowsAndTheGapsBetweenThem()
        {
            Assert.That(Rows.ContentHeight(0), Is.EqualTo(0f));
            Assert.That(Rows.ContentHeight(1), Is.EqualTo(100f));
            Assert.That(Rows.ContentHeight(10), Is.EqualTo(1072f));
        }

        [Test]
        public void GridSplitsTheWidthIntoSquareCoversPlusTheTitleStrip()
        {
            Assert.That(Grid.CellWidth, Is.EqualTo(200f));
            Assert.That(Grid.CellHeight, Is.EqualTo(238f));
            Assert.That(Grid.RowCount(10), Is.EqualTo(3));
            Assert.That(Grid.ContentHeight(10), Is.EqualTo(762f));
            Assert.That(Grid.PositionOf(5), Is.EqualTo(new Vector2(220f, -262f)));
        }

        [Test]
        public void VisibleRangeCoversOnlyRowsThatTouchTheViewport()
        {
            Rows.VisibleRange(0f, 400f, 1000, 0, out int first, out int end);
            Assert.That((first, end), Is.EqualTo((0, 4)));

            // Row 0 ends exactly where the viewport starts, so it is no longer visible.
            Rows.VisibleRange(100f, 400f, 1000, 0, out first, out end);
            Assert.That((first, end), Is.EqualTo((1, 5)));
        }

        [Test]
        public void VisibleRangeAddsBufferRowsWithoutLeavingTheList()
        {
            Rows.VisibleRange(0f, 400f, 1000, 1, out int first, out int end);
            Assert.That((first, end), Is.EqualTo((0, 5)));

            Rows.VisibleRange(1080f, 400f, 1000, 1, out first, out end);
            Assert.That((first, end), Is.EqualTo((9, 15)));
        }

        [Test]
        public void VisibleRangeEndsAtTheLastItemOfAPartialGridRow()
        {
            Grid.VisibleRange(0f, 300f, 10, 0, out int first, out int end);
            Assert.That((first, end), Is.EqualTo((0, 8)));

            Grid.VisibleRange(500f, 300f, 10, 0, out first, out end);
            Assert.That((first, end), Is.EqualTo((8, 10)));
        }

        [Test]
        public void VisibleRangeIsEmptyWithoutItemsViewportOrOverlap()
        {
            Rows.VisibleRange(0f, 400f, 0, 1, out int first, out int end);
            Assert.That(end - first, Is.EqualTo(0));

            Rows.VisibleRange(0f, 0f, 10, 1, out first, out end);
            Assert.That(end - first, Is.EqualTo(0));

            Rows.VisibleRange(5000f, 400f, 10, 0, out first, out end);
            Assert.That(end - first, Is.EqualTo(0));
        }

        [Test]
        public void OffsetToRevealMovesOnlyAsFarAsNeeded()
        {
            Assert.That(Rows.OffsetToReveal(1, 0f, 400f, 20), Is.EqualTo(0f), "already visible");
            Assert.That(Rows.OffsetToReveal(0, 300f, 400f, 20), Is.EqualTo(0f), "above the viewport");
            Assert.That(Rows.OffsetToReveal(10, 0f, 400f, 20), Is.EqualTo(780f), "below the viewport");
            Assert.That(Rows.OffsetToReveal(19, 0f, 400f, 20), Is.EqualTo(1752f), "last row stops at the end");
            Assert.That(Rows.OffsetToReveal(99, 120f, 400f, 20), Is.EqualTo(120f), "unknown index");
        }
    }
}
