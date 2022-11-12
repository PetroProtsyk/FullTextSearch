using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Collections
{
    /// <summary>
    /// Collection that implements in-memory b-tree
    /// Reference: Douglas Comer, The Ubiquitous B-Tree, 1979
    /// </summary>
    public class Btree<TKey, TValue> : IDictionary<TKey, TValue>
    {
        #region Fields

        private readonly IComparer<TKey> comparer = Comparer<TKey>.Default;

        private readonly int order;
        private readonly int maxChildren;

        private Node rootNode;
        private int depth;
        private int count;

        #endregion

        #region Constructor

        public Btree()
            : this(2)
        {
        }

        public Btree(int order)
        {
            if (order < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            this.order = order;
            this.maxChildren = 2 * order;
            Clear();
        }

        #endregion

        #region Methods
        private bool ContainsKeyInternal(TKey key)
        {
            Node temp;
            return TryFindKeyOrLeaf(key, out temp);
        }

        private void AddInternal(TKey key, TValue value)
        {
            var target = FindLeaf(key);

            target.Put(key, value, comparer);
            ++count;

            if (target.Keys.Count > maxChildren)
            {
                SplitUp(target);
            }
        }


        private bool RemoveInternal(TKey key)
        {
            Node temp;
            if (!TryFindKeyOrLeaf(key, out temp))
            {
                return false;
            }

            int index;
            if (!temp.TryFindUpperBound(key, comparer, out index))
            {
                throw new InvalidOperationException();
            }

            Node nodeThatLostKey = null;
            if (!temp.IsLeaf)
            {
                // Locate adjacent key - leftmost leaf in the right subtree
                var rightSubtree = temp.Links[index + 1];
                while (!rightSubtree.IsLeaf)
                {
                    rightSubtree = rightSubtree.Links[0];
                }

                temp.Keys[index] = rightSubtree.Keys[0];
                temp.Values[index] = rightSubtree.Values[0];

                rightSubtree.Keys.RemoveAt(0);
                rightSubtree.Values.RemoveAt(0);
                rightSubtree.Links.RemoveAt(0);
                nodeThatLostKey = rightSubtree;
            }
            else
            {
                temp.Keys.RemoveAt(index);
                temp.Values.RemoveAt(index);
                temp.Links.RemoveAt(index);
                nodeThatLostKey = temp;
            }

            while (nodeThatLostKey.Parent != null && nodeThatLostKey.Keys.Count < order)
            {
                var linkIndex = nodeThatLostKey.Parent.Links.IndexOf(nodeThatLostKey);
                if (linkIndex < 0)
                {
                    throw new InvalidOperationException();
                }

                var neighborLinkIndex = -1;
                bool isSmaller;

                if (linkIndex + 1 < nodeThatLostKey.Parent.Links.Count && nodeThatLostKey.Parent.Links[linkIndex + 1] != null)
                {
                    isSmaller = false;
                    neighborLinkIndex = linkIndex + 1;
                }
                else
                {
                    isSmaller = true;
                    neighborLinkIndex = linkIndex - 1;
                }

                var neighbor = nodeThatLostKey.Parent.Links[neighborLinkIndex];
                if (neighbor.Keys.Count + nodeThatLostKey.Keys.Count >= maxChildren)
                {
                    // Redistribution
                    // During redistribution, the keys are evenly divided between the two neighboring nodes
                    while (nodeThatLostKey.Keys.Count < order)
                    {
                        if (isSmaller)
                        {
                            int keyIndex = linkIndex - 1;
                            int jj = nodeThatLostKey.Put(nodeThatLostKey.Parent.Keys[keyIndex], nodeThatLostKey.Parent.Values[keyIndex], comparer);
                            if (jj != 0)
                            {
                                throw new InvalidOperationException();
                            }
                            nodeThatLostKey.Links[jj] = neighbor.Links[neighbor.Links.Count - 1];
                            if (nodeThatLostKey.Links[jj] != null)
                            {
                                nodeThatLostKey.Links[jj].Parent = nodeThatLostKey;
                            }

                            nodeThatLostKey.Parent.Keys[keyIndex] = neighbor.Keys[neighbor.Keys.Count - 1];
                            nodeThatLostKey.Parent.Values[keyIndex] = neighbor.Values[neighbor.Values.Count - 1];

                            neighbor.Keys.RemoveAt(neighbor.Keys.Count - 1);
                            neighbor.Values.RemoveAt(neighbor.Values.Count - 1);
                            neighbor.Links.RemoveAt(neighbor.Links.Count - 1);
                        }
                        else
                        {
                            int keyIndex = linkIndex;
                            int jj = nodeThatLostKey.Put(nodeThatLostKey.Parent.Keys[linkIndex], nodeThatLostKey.Parent.Values[linkIndex], comparer);
                            if (jj != nodeThatLostKey.Links.Count -2)
                            {
                                throw new InvalidOperationException();
                            }
                            nodeThatLostKey.Links[jj] = nodeThatLostKey.Links[jj + 1];
                            nodeThatLostKey.Links[jj + 1] = neighbor.Links[0];
                            if (nodeThatLostKey.Links[jj + 1] != null)
                            {
                                nodeThatLostKey.Links[jj + 1].Parent = nodeThatLostKey;
                            }


                            nodeThatLostKey.Parent.Keys[keyIndex] = neighbor.Keys[0];
                            nodeThatLostKey.Parent.Values[keyIndex] = neighbor.Values[0];

                            neighbor.Keys.RemoveAt(0);
                            neighbor.Values.RemoveAt(0);
                            neighbor.Links.RemoveAt(0);
                        }
                    }
                }
                else
                {
                    // Concatenation
                    // During a concatenation, the keys are simply combined into one of the nodes, and the other is discarded
                    if (isSmaller)
                    {
                        int keyIndex = linkIndex - 1;
                        int jj = nodeThatLostKey.Put(nodeThatLostKey.Parent.Keys[keyIndex], nodeThatLostKey.Parent.Values[keyIndex], comparer);
                        nodeThatLostKey.Links[jj] = neighbor.Links[neighbor.Links.Count - 1];
                        if (nodeThatLostKey.Links[jj] != null)
                        {
                            nodeThatLostKey.Links[jj].Parent = nodeThatLostKey;
                        }

                        for (int i = 0; i < neighbor.Keys.Count; ++i)
                        {
                            int j = nodeThatLostKey.Put(neighbor.Keys[i], neighbor.Values[i], comparer);
                            nodeThatLostKey.Links[j] = neighbor.Links[i];
                            if (nodeThatLostKey.Links[j] != null)
                            {
                                nodeThatLostKey.Links[j].Parent = nodeThatLostKey;
                            }
                        }

                        nodeThatLostKey.Parent.Keys.RemoveAt(keyIndex);
                        nodeThatLostKey.Parent.Values.RemoveAt(keyIndex);
                        nodeThatLostKey.Parent.Links.RemoveAt(neighborLinkIndex);
                        neighbor.Dispose();
                    }
                    else
                    {
                        int keyIndex = linkIndex;
                        int jj = nodeThatLostKey.Put(nodeThatLostKey.Parent.Keys[keyIndex], nodeThatLostKey.Parent.Values[keyIndex], comparer);
                        nodeThatLostKey.Links[jj] = nodeThatLostKey.Links[nodeThatLostKey.Links.Count - 1];
                        nodeThatLostKey.Links[nodeThatLostKey.Links.Count - 1] = null;

                        for (int i = 0; i < neighbor.Keys.Count; ++i)
                        {
                            int j = nodeThatLostKey.Put(neighbor.Keys[i], neighbor.Values[i], comparer);
                            nodeThatLostKey.Links[j] = neighbor.Links[i];
                            if (nodeThatLostKey.Links[j] != null)
                            {
                                nodeThatLostKey.Links[j].Parent = nodeThatLostKey;
                            }
                        }
                        nodeThatLostKey.Links[nodeThatLostKey.Links.Count - 1] = neighbor.Links[neighbor.Links.Count - 1];
                        if (nodeThatLostKey.Links[nodeThatLostKey.Links.Count - 1] != null)
                        {
                            nodeThatLostKey.Links[nodeThatLostKey.Links.Count - 1].Parent = nodeThatLostKey;
                        }

                        nodeThatLostKey.Parent.Keys.RemoveAt(keyIndex);
                        nodeThatLostKey.Parent.Values.RemoveAt(keyIndex);
                        nodeThatLostKey.Parent.Links.RemoveAt(neighborLinkIndex);
                        neighbor.Dispose();
                    }

                    if (nodeThatLostKey.Parent.Parent == null && nodeThatLostKey.Parent.Keys.Count == 0)
                    {
                        var oldRoot = rootNode;
                        rootNode = nodeThatLostKey.Parent.Links[0];
                        rootNode.Parent = null;
                        oldRoot.Dispose();
                        break;
                    }
                }

                nodeThatLostKey = nodeThatLostKey.Parent;
                linkIndex = int.MinValue;
            }

            --count;
            return true;
        }


        private void SplitUp(Node target)
        {
            var targetParent = target.Parent;
            if (targetParent == null)
            {
                rootNode = new Node(null);
                targetParent = rootNode;
                ++depth;
            }

            var leftNode = new Node(targetParent);
            leftNode.Links[0] = target.Links[order];

            var rightNode = new Node(targetParent);
            rightNode.Links[0] = target.Links[target.Links.Count - 1];

            if (targetParent.Keys.Count > maxChildren)
            {
                throw new Exception();
            }

            for (int i = 0; i < target.Keys.Count; ++i)
            {
                if (i < order)
                {
                    int index = leftNode.Put(target.Keys[i], target.Values[i], comparer);
                    leftNode.Links[index] = target.Links[i];
                }
                else if (i == order)
                {
                    int index = targetParent.Put(target.Keys[order], target.Values[order], comparer);
                    targetParent.Links[index] = leftNode;
                    targetParent.Links[index + 1] = rightNode;
                }
                else
                {
                    int index = rightNode.Put(target.Keys[i], target.Values[i], comparer);
                    rightNode.Links[index] = target.Links[i];
                }
            }

            foreach (var leftLink in leftNode.Links.Where(l => l != null))
            {
                leftLink.Parent = leftNode;
            }

            foreach (var rightLink in rightNode.Links.Where(l => l != null))
            {
                rightLink.Parent = rightNode;
            }

            target.Dispose();

            if (targetParent.Keys.Count > maxChildren)
            {
                SplitUp(targetParent);
            }
        }

        private Node FindLeaf(TKey key)
        {
            Node result;
            if (TryFindKeyOrLeaf(key, out result))
            {
                throw new KeyAlreadyExistsException();
            }
            return result;
        }

        private bool TryFindKeyOrLeaf(TKey key, out Node keyOrLeafNode)
        {
            if (rootNode == null)
                throw new InvalidOperationException();

            var current = rootNode;
            while (true)
            {
                int index;
                if (current.TryFindUpperBound(key, comparer, out index))
                {
                    keyOrLeafNode = current;
                    return true;
                }

                if (current.IsLeaf)
                {
                    break;
                }

                current = current.Links[index];
            }

            if (current == null)
            {
                throw new InvalidOperationException();
            }

            keyOrLeafNode = current;
            return false;
        }

        #endregion

        #region Types

        private class Node
        {
            public List<TKey> Keys { get; private set; }

            public List<TValue> Values { get; private set; }

            public List<Node> Links { get; private set; }

            public Node Parent { get; set; }

            public bool IsLeaf => Links.All(l => l == null);


            public Node(Node parent)
            {
                Parent = parent;
                Keys = new List<TKey>();
                Values = new List<TValue>();
                Links = new List<Node>();
                Links.Add(null);
            }


            public int Put(TKey key, TValue value, IComparer<TKey> comparer)
            {
                int index;
                if (TryFindUpperBound(key, comparer, out index))
                {
                    throw new KeyAlreadyExistsException();
                }

                Keys.Insert(index, key);
                Values.Insert(index, value);
                Links.Insert(index, null);
                return index;
            }


            internal bool TryFindUpperBound(TKey key, IComparer<TKey> comparer, out int index)
            {
                int keyIndex = Keys.BinarySearch(key, comparer);
                if (keyIndex < 0)
                {
                    index = ~keyIndex;
                    return false;
                }
                index = keyIndex;
                return true;
            }

            public void Dispose()
            {
                Links = null;
                Keys = null;
                Values = null;
                Parent = null;
            }
        }

        #endregion

        #region Visualization

        public string ToDotNotation()
        {
            var text = new StringBuilder();
            text.AppendLine("digraph g {");
            text.AppendLine("node[shape = record, height = .1];");

            var labels = new Dictionary<Node, int>();

            // Nodes
            foreach (var node in Visit())
            {
                int index = 0;
                if (!labels.TryGetValue(node.Item1, out index))
                {
                    index = labels.Count + 1;
                    labels.Add(node.Item1, index);
                    FormatNode(text, node.Item1, index);
                }
            }

            text.AppendLine();

            // Links
            var linksDone = new HashSet<int>();
            foreach (var node in Visit())
            {
                int index;
                if (!labels.TryGetValue(node.Item1, out index))
                {
                    throw new InvalidOperationException();
                }
                if (linksDone.Contains(index))
                {
                    continue;
                }

                FormatLinks(text, labels, node.Item1, index);
                linksDone.Add(index);
            }

            text.AppendLine("}");
            return text.ToString();
        }


        private void FormatLinks(StringBuilder text, Dictionary<Node, int> labels, Node node, int id)
        {
            for (int i = 0; i < node.Links.Count; ++i)
            {
                var child = node.Links[i];
                if (child == null)
                {
                    continue;
                }

                int childIndex;
                if (!labels.TryGetValue(child, out childIndex))
                {
                    throw new InvalidOperationException();
                }

                text.AppendFormat("node{0}:f{1}->node{2}:f0;", id, i, childIndex);
                text.AppendLine();
            }
        }

        private void FormatNode(StringBuilder text, Node node, int id)
        {
            text.AppendFormat("node{0}[label = \"", id);
            for (int i = 0; i < node.Keys.Count; ++i)
            {
                text.AppendFormat("<f{0}>|", i);
                text.Append(node.Keys[i]);
                text.Append(" - ");
                text.Append(node.Values[i]);
                text.Append('|');
            }
            text.AppendFormat("<f{0}>", node.Keys.Count);
            text.Append("\"];");
            text.AppendLine();
        }

        private IEnumerable<Tuple<Node, int>> Visit()
        {
            var stack = new Stack<Tuple<Node, bool, int>>();
            if (rootNode != null)
            {
                stack.Push(Tuple.Create(rootNode, false, 0));
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                var node = current.Item1;
                var linksProcessed = current.Item2;
                var li = current.Item3;

                if (node.Keys.Count < order)
                {
                    if (node.IsLeaf || node.Parent == null)
                    {
                        // Only root
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                if (linksProcessed)
                {
                    yield return Tuple.Create(node, li);

                    if (li + 1 < node.Values.Count)
                    {
                        stack.Push(Tuple.Create(node, false, li + 1));
                    }
                    else if (li + 1 < node.Links.Count && node.Links[li + 1] != null)
                    {
                        stack.Push(Tuple.Create(node.Links[li + 1], false, 0));
                    }
                }
                else
                {
                    stack.Push(Tuple.Create(node, true, li));
                    if (li < node.Links.Count && node.Links[li] != null)
                    {
                        stack.Push(Tuple.Create(node.Links[li], false, 0));
                    }
                }
            }
        }
        #endregion

        #region IEnumerable<KeyValuePair<TKey, TValue>>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Visit().Select(v => new KeyValuePair<TKey, TValue>(v.Item1.Keys[v.Item2], v.Item1.Values[v.Item2])).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        #region IDictionary
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }


        public void Clear()
        {
            this.rootNode = new Node(null);
            this.count = 0;
            this.depth = 0;
        }


        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue value;
            if (TryGetValue(item.Key, out value))
            {
                return Equals(value, item.Value);
            }
            return false;
        }


        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }


        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }


        public int Count => count;

        public bool IsReadOnly => false;


        public bool ContainsKey(TKey key)
        {
            return ContainsKeyInternal(key);
        }


        public void Add(TKey key, TValue value)
        {
            AddInternal(key, value);
        }


        public bool Remove(TKey key)
        {
            return RemoveInternal(key);
        }


        public bool TryGetValue(TKey key, out TValue value)
        {
            Node temp;
            if (!TryFindKeyOrLeaf(key, out temp))
            {
                value = default(TValue);
                return false;
            }

            int index;
            if (!temp.TryFindUpperBound(key, comparer, out index))
            {
                value = default(TValue);
                return false;
            }

            value = temp.Values[index];
            return true;
        }


        public TValue this[TKey key]
        {
            get
            {
                TValue result;
                if (!TryGetValue(key, out result))
                {
                    throw new KeyNotFoundException();
                }
                return result;
            }
            set
            {
                Node temp;
                if (!TryFindKeyOrLeaf(key, out temp))
                {
                    AddInternal(key, value);
                }
                else
                {
                    int index;
                    if (temp.TryFindUpperBound(key, comparer, out index))
                    {
                        temp.Values[index] = value;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        public ICollection<TKey> Keys { get; }
        public ICollection<TValue> Values { get; }
        #endregion
    }

    public class KeyAlreadyExistsException : ArgumentException
    {
        public KeyAlreadyExistsException()
            :base("Key already exists")
        { }
    }
}
