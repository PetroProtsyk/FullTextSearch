using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Collections
{
    /// <summary>
    /// Heap Data Structure
    /// Supports modification of value
    /// Reference: J. Bentley, Programming Pearls, Column 14
    /// </summary>
    public class GenericHeap<T> : IEnumerable<T>
    {
        #region Fields
        private readonly List<V> items;
        private readonly IComparer<T> comparer;
        #endregion

        #region Constructors
        public GenericHeap()
            : this(Comparer<T>.Default) { }

        public GenericHeap(IEnumerable<T> elements)
            : this(Comparer<T>.Default, elements) { }

        public GenericHeap(IComparer<T> comparer)
            : this(comparer, Enumerable.Empty<T>()) { }

        public GenericHeap(IComparer<T> comparer, IEnumerable<T> range)
        {
            this.comparer = comparer;
            this.items = new List<V>();
            var i = 0;
            foreach (var v in range)
            {
                items.Add(new V { owner = this, item = v, index = i++ });
            }
            MakeHeap();
        }
        #endregion

        #region Methods
        private IItemReference AddInternal(T item)
        {
            var result = new V { item = item, owner = this, index = items.Count };
            items.Add(result);
            SiftUp(Count - 1);
            Verify();
            return result;
        }

        private void RemoveAtInternal(int index)
        {
            Swap(index, Count - 1);
            items.RemoveAt(Count - 1);
            SiftDown(index);
            Verify();
        }

        private int SiftDown(int k)
        {
            int left = LeftChild(k);
            int right = left + 1;
            int max = k;

            while (left < Count)
            {
                if (IsOutOfOrder(max, left))
                {
                    max = left;
                }

                if (right < Count && IsOutOfOrder(max, right))
                {
                    max = right;
                }

                if (max == k)
                {
                    break;
                }

                Swap(max, k);

                k = max;
                left = LeftChild(k);
                right = left + 1;
            }

            return k;
        }

        private int SiftUp(int k)
        {
            int parent = ParentOf(k);
            while (k > 0 && IsOutOfOrder(parent, k))
            {
                Swap(k, parent);
                k = parent;
                parent = ParentOf(k);
            }
            return k;
        }

        private void Verify()
        {
#if DEBUG
            for (int i = 0; i < items.Count; ++i)
            {
                if (i != items[i].index)
                {
                    throw new Exception($"Invariant broken at {i} != {items[i].index}");
                }
            }
#endif
        }

        private int ChangedAt(int k, T value, out T old)
        {
            old = items[k].item;
            items[k].item = value;
            var result = SiftDown(SiftUp(k));
            Verify();
            return result;
        }

        private int FindInternal(T value)
        {
            var stack = new Stack<int>();
            if (Count > 0)
            {
                stack.Push(0);
            }

            while (stack.Count > 0)
            {
                var parent = stack.Pop();
                var compare = comparer.Compare(items[parent].item, value);
                if (compare > 0)
                {
                    continue;
                }
                else if (compare == 0)
                {
                    return parent;
                }

                int left = LeftChild(parent);
                int right = RightChild(parent);

                if (right < Count)
                {
                    stack.Push(right);
                }

                if (left < Count)
                {
                    stack.Push(left);
                }
            }

            return -1;
        }

        private void MakeHeap()
        {
            for (int i = ParentOf(Count); i >= 0; i--)
            {
                SiftDown(i);
            }
        }

        private void Swap(int i, int j)
        {
            var tmp = items[i];
            items[i] = items[j];
            items[i].index = i;
            items[j] = tmp;
            items[j].index = j;
        }

        private bool IsOutOfOrder(int i, int j)
        {
            return comparer.Compare(items[i].item, items[j].item) > 0;
        }

        private static int ParentOf(int index)
        {
            return (index - 1) >> 1;
        }

        private static int LeftChild(int index)
        {
            return (index << 1) + 1;
        }

        private static int RightChild(int index)
        {
            return LeftChild(index) + 1;
        }
        #endregion

        #region Public Properties
        public int Count
        {
            get { return items.Count; }
        }

        public bool IsEmpty
        {
            get { return items.Count == 0; }
        }

        public T Top
        {
            get
            {
                return items[0].item;
            }
        }
        #endregion

        #region Public API
        public IItemReference Add(T item)
        {
            return AddInternal(item);
        }

        public void AddRange(IEnumerable<T> range)
        {
            foreach (var item in range)
            {
                AddInternal(item);
            }
        }

        public T RemoveTop()
        {
            var result = Top;
            RemoveAtInternal(0);
            return result;
        }

        public bool Contains(T value)
        {
            var index = FindInternal(value);
            return (index >= 0);
        }

        public bool Remove(T value)
        {
            var index = FindInternal(value);
            if (index < 0)
            {
                return false;
            }

            RemoveAtInternal(index);
            return true;
        }
        #endregion

        #region IEnumerable<T>
        public IEnumerator<T> GetEnumerator()
        {
            return Visit().Select(n => items[n].item).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        #region Visualization

        public string ToDotNotation()
        {
            var text = new StringBuilder();
            text.AppendLine("digraph g {");
            text.AppendLine("node[shape = circle];");

            // Nodes
            foreach (var parent in Visit())
            {
                text.AppendLine($"node{parent} [label=\"{items[parent]}\"]");

                int left = LeftChild(parent);
                int right = RightChild(parent);

                if (left < Count)
                {
                    text.AppendLine($"node{parent} -> node{left}");
                }

                if (right < Count)
                {
                    text.AppendLine($"node{parent} -> node{right}");
                }
            }

            text.AppendLine("}");
            return text.ToString();
        }


        private IEnumerable<int> Visit()
        {
            var stack = new Stack<int>();
            if (Count > 0)
            {
                stack.Push(0);
            }

            while (stack.Count > 0)
            {
                var parent = stack.Pop();

                int left = LeftChild(parent);
                int right = RightChild(parent);

                if (right < Count)
                {
                    stack.Push(right);
                }

                if (left < Count)
                {
                    stack.Push(left);
                }

                yield return parent;
            }
        }

        #endregion

        #region Types
        private class V : IItemReference
        {
            public T item;

            public GenericHeap<T> owner;

            public int index;

            public T Value => item;

            public void Change(T newValue)
            {
                owner.ChangedAt(index, newValue, out var _);
            }
        }

        public interface IItemReference
        {
            T Value { get; }

            void Change(T newValue);
        }
        #endregion
    }
}