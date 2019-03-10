using System;
using System.Collections.Generic;
using System.Threading;

namespace Protsyk.PMS.FullText.Core.Collections
{
    public class LFUCache<TKey, TValue>
    {
        private readonly GenericHeap<ValueTuple<int, int, TKey>> h = new GenericHeap<ValueTuple<int, int, TKey>>();
        private readonly Dictionary<TKey, ValueTuple<TValue, GenericHeap<ValueTuple<int, int, TKey>>.IItemReference>> c
                                    = new Dictionary<TKey, (TValue, GenericHeap<ValueTuple<int, int, TKey>>.IItemReference)>();
        private readonly int capacity;
        private int nextOrder;

        public LFUCache(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            this.capacity = capacity;
            this.nextOrder = 0;
        }

        private void Ensure(int count)
        {
            while (h.Count + count > capacity)
            {
                var k = h.RemoveTop();
                if (!c.Remove(k.Item3))
                {
                    throw new Exception("Unexpected");
                }
            }
        }

        public void Put(TKey key, TValue value)
        {
            if (!c.TryGetValue(key, out var valref))
            {
                Ensure(1);
                var order = Interlocked.Increment(ref nextOrder);
                var newRef = h.Add(new ValueTuple<int, int, TKey>(1, nextOrder, key));
                valref = new ValueTuple<TValue, GenericHeap<ValueTuple<int, int, TKey>>.IItemReference>(value, newRef);
                c.Add(key, valref);
            }
            else
            {
                var order = Interlocked.Increment(ref nextOrder);
                var newFrequency = valref.Item2.Value.Item1 + 1;
                valref.Item2.Change(new ValueTuple<int, int, TKey>(newFrequency, order, key));
                c[key] = new ValueTuple<TValue, GenericHeap<ValueTuple<int, int, TKey>>.IItemReference>(value, valref.Item2);
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
            if (!c.TryGetValue(key, out var valref))
            {
                value = default(TValue);
                return false;
            }
            else
            {
                var order = Interlocked.Increment(ref nextOrder);
                var newFrequency = valref.Item2.Value.Item1 + 1;
                valref.Item2.Change(new ValueTuple<int, int, TKey>(newFrequency, order, key));
                value = valref.Item1;
                return true;
            }
        }
    }
}
