using System;
using System.Collections.Generic;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Fixed-capacity cache that drops the least recently used entry when full. Reading an entry
    /// counts as using it. The eviction callback is where the owner frees what the value holds.
    /// </summary>
    internal sealed class LruCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Action<TKey, TValue> _onEvicted;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _nodes;
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new LinkedList<KeyValuePair<TKey, TValue>>();

        internal LruCache(int capacity, Action<TKey, TValue> onEvicted = null)
        {
            _capacity = Math.Max(1, capacity);
            _onEvicted = onEvicted;
            _nodes = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(_capacity);
        }

        internal int Count => _nodes.Count;

        internal bool TryGet(TKey key, out TValue value)
        {
            if (_nodes.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>> node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Adds or replaces an entry; a replaced value is evicted like any other.</summary>
        internal void Set(TKey key, TValue value)
        {
            if (_nodes.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>> existing))
            {
                Evict(existing);
            }

            _nodes[key] = _order.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            while (_nodes.Count > _capacity)
            {
                Evict(_order.Last);
            }
        }

        internal void Clear()
        {
            while (_order.Last != null)
            {
                Evict(_order.Last);
            }
        }

        private void Evict(LinkedListNode<KeyValuePair<TKey, TValue>> node)
        {
            _order.Remove(node);
            _nodes.Remove(node.Value.Key);
            _onEvicted?.Invoke(node.Value.Key, node.Value.Value);
        }
    }
}
