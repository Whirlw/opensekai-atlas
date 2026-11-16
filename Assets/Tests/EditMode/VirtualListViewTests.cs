using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.Tests.EditMode
{
    public sealed class VirtualListViewTests
    {
        private GameObject _root;
        private RectTransform _content;
        private VirtualListView _list;
        private Dictionary<RectTransform, int> _bound;
        private int _created;
        private int _released;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(800f, 400f);

            RectTransform viewport = CreateChild("Viewport", _root.transform);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            _content = CreateChild("Content", viewport);

            ScrollRect scroll = _root.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;

            _bound = new Dictionary<RectTransform, int>();
            _created = 0;
            _released = 0;
            _list = VirtualListView.Attach(
                scroll,
                width => VirtualListLayout.Rows(width, 100f, 8f),
                parent =>
                {
                    _created++;
                    return CreateChild("Cell", parent);
                },
                (cell, index) => _bound[cell] = index,
                cell =>
                {
                    _released++;
                    _bound.Remove(cell);
                });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void OnlyCellsNearTheViewportExist()
        {
            _list.SetItemCount(1000);

            Assert.That(_list.ActiveCellCount, Is.EqualTo(5), "four visible rows plus one buffer row");
            Assert.That(_created, Is.EqualTo(5));
            Assert.That(_bound.Values.OrderBy(index => index), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(_content.sizeDelta.y, Is.EqualTo(1000 * 100f + 999 * 8f));
        }

        [Test]
        public void ScrollingReusesCellsForTheNewRange()
        {
            _list.SetItemCount(1000);
            _content.anchoredPosition = new Vector2(0f, 1080f);
            _list.Refresh();

            Assert.That(_bound.Values.OrderBy(index => index), Is.EqualTo(new[] { 9, 10, 11, 12, 13, 14 }));
            Assert.That(_created, Is.EqualTo(6), "five cells are recycled, one is new");
            Assert.That(_list.CellAt(0), Is.Null);

            RectTransform cell = _list.CellAt(10);
            Assert.That(cell, Is.Not.Null);
            Assert.That(cell.anchoredPosition, Is.EqualTo(new Vector2(0f, -1080f)));
            Assert.That(cell.sizeDelta, Is.EqualTo(new Vector2(800f, 100f)));
        }

        [Test]
        public void ShrinkingTheListPoolsTheSpareCells()
        {
            _list.SetItemCount(1000);
            _list.SetItemCount(2);

            Assert.That(_list.ActiveCellCount, Is.EqualTo(2));
            Assert.That(_list.PooledCellCount, Is.EqualTo(3));
            Assert.That(_created, Is.EqualTo(5), "the second fill reuses pooled cells");
            Assert.That(_released, Is.EqualTo(5), "every cell of the first fill was released");

            _list.SetItemCount(0);
            Assert.That(_list.ActiveCellCount, Is.EqualTo(0));
            Assert.That(_bound, Is.Empty);
        }

        [Test]
        public void ScrollToBringsAnItemIntoView()
        {
            _list.SetItemCount(1000);
            _list.ScrollTo(10);

            Assert.That(_content.anchoredPosition.y, Is.EqualTo(780f));
            Assert.That(_list.CellAt(10), Is.Not.Null);
        }

        private static RectTransform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }
    }
}
