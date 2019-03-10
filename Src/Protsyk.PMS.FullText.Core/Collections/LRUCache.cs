using System;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core.Collections
{
    public class LRUCache<TKey, TValue>
    {
        private readonly IDictionary<TKey, LinkedListNode<ValueTuple<TKey, TValue>>> items;
        private readonly LinkedList<ValueTuple<TKey, TValue>> usageQueue;
        private readonly int maxSize;

        public LRUCache()
            : this(65536)
        {
        }

        public LRUCache(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            items = new Dictionary<TKey, LinkedListNode<ValueTuple<TKey, TValue>>>();
            usageQueue = new LinkedList<ValueTuple<TKey, TValue>>();
            maxSize = capacity;
        }

        public void Put(TKey key, TValue value)
        {
            if (!Remove(key))
            {
                Evict(maxSize - 1);
            }

            var valueNode = usageQueue.AddFirst(new ValueTuple<TKey, TValue>(key, value));
            try
            {
                items.Add(key, valueNode);
            }
            catch
            {
                usageQueue.Remove(valueNode);
            }
        }

        public TValue Get(TKey key)
        {
            if (!TryGet(key, out var result))
            {
                throw new KeyNotFoundException();
            }
            return result;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (items.TryGetValue(key, out var valueNode))
            {
                if (valueNode != usageQueue.First)
                {
                    usageQueue.Remove(valueNode);
                    usageQueue.AddFirst(valueNode);
                }
                value = valueNode.Value.Item2;
                return true;
            }

            value = default(TValue);
            return false;
        }

        public bool Remove(TKey key)
        {
            if (items.TryGetValue(key, out var valueNode))
            {
                items.Remove(key);
                usageQueue.Remove(valueNode);
                return true;
            }

            return false;
        }

        private void Evict(int preferredSize)
        {
            while (usageQueue.Count > preferredSize)
            {
                var last = usageQueue.Last;

                if (!items.Remove(last.Value.Item1))
                {
                    throw new Exception();
                }

                usageQueue.RemoveLast();
            }
        }

    }
}