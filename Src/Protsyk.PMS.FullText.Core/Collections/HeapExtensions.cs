using System;
using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core.Collections
{
    public static class HeapExtensions
    {
        public static IEnumerable<T> HeapSort<T>(this IEnumerable<T> items)
        {
            return HeapSort(items, Comparer<T>.Default);
        }

        public static IEnumerable<T> HeapSort<T>(this IEnumerable<T> items, IComparer<T> comparer)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(comparer);

            var heap = new Heap<T>(comparer, items);
            while (!heap.IsEmpty)
            {
                yield return heap.RemoveTop();
            }
        }

        public static IEnumerable<T> TopN<T>(this IEnumerable<T> items, int n)
        {
            return TopN(items, Comparer<T>.Default, n);
        }

        public static IEnumerable<T> TopN<T>(this IEnumerable<T> items, IComparer<T> comparer, int n)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(comparer);

            if (n < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }

            if (n == 0)
            {
                return Enumerable.Empty<T>();
            }

            var heap = new Heap<T>(comparer);
            foreach (var item in items)
            {
                if (heap.Count < n)
                {
                    heap.Add(item);
                }
                else if (comparer.Compare(item, heap.Top) > 0)
                {
                    heap.RemoveTop();
                    heap.Add(item);
                }
            }

            var result = new T[heap.Count];
            while (heap.Count > 0)
            {
                result[heap.Count - 1] = heap.RemoveTop();
            }
            return result;
        }
    }
}