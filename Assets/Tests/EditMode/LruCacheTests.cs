using System.Collections.Generic;
using NUnit.Framework;
using OpenSekai.App.UI;

namespace OpenSekai.Tests.EditMode
{
    public sealed class LruCacheTests
    {
        private List<string> _evicted;
        private LruCache<string, int> _cache;

        [SetUp]
        public void SetUp()
        {
            _evicted = new List<string>();
            _cache = new LruCache<string, int>(2, (key, value) => _evicted.Add(key + "=" + value));
        }

        [Test]
        public void ReadingAnEntryKeepsItOverAnOlderOne()
        {
            _cache.Set("a", 1);
            _cache.Set("b", 2);
            Assert.That(_cache.TryGet("a", out int value), Is.True);
            Assert.That(value, Is.EqualTo(1));

            _cache.Set("c", 3);

            Assert.That(_evicted, Is.EqualTo(new[] { "b=2" }));
            Assert.That(_cache.TryGet("b", out _), Is.False);
            Assert.That(_cache.TryGet("a", out _), Is.True);
            Assert.That(_cache.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReplacingAnEntryEvictsItsOldValue()
        {
            _cache.Set("a", 1);
            _cache.Set("a", 5);

            Assert.That(_evicted, Is.EqualTo(new[] { "a=1" }));
            Assert.That(_cache.TryGet("a", out int value), Is.True);
            Assert.That(value, Is.EqualTo(5));
            Assert.That(_cache.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearEvictsEverything()
        {
            _cache.Set("a", 1);
            _cache.Set("b", 2);
            _cache.Clear();

            Assert.That(_evicted, Is.EquivalentTo(new[] { "a=1", "b=2" }));
            Assert.That(_cache.Count, Is.EqualTo(0));
        }
    }
}
