using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core.Collections
{
    /// <summary>
    /// B-Tree
    /// Reference: Douglas Comer, The Ubiquitous B-Tree, 1979
    /// </summary>
    public class BtreePersistent<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
    {
        #region Fields

        private readonly IComparer<TKey> comparer = Comparer<TKey>.Default;
        private readonly IDataSerializer<TKey> keySerializer;
        private readonly IDataSerializer<TValue> valueSerializer;

        private readonly int order;
        private readonly int maxChildren;

        private NodeManager nodeManager;

        #endregion

        #region Constructor

        public BtreePersistent()
            : this(2) { }


        public BtreePersistent(int order)
            : this(new MemoryStorage(), order) { }


        public BtreePersistent(IPersistentStorage persistentStorage, int order)
        {
            ArgumentNullException.ThrowIfNull(persistentStorage);

            if (order < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            keySerializer = DataSerializer.GetDefault<TKey>();
            valueSerializer = DataSerializer.GetDefault<TValue>();

            this.order = order;
            this.maxChildren = 2 * order;
            this.nodeManager = new NodeManager(persistentStorage, order);
        }

        #endregion

        #region Methods

        private void AddInternal(TKey key, TValue value)
        {
            var target = FindLeaf(key);
            using (var transaction = nodeManager.StartTransaction())
            {
                var dataLink = new DataLink(
                    nodeManager.SaveData(keySerializer.GetBytes(key)),
                    nodeManager.SaveData(valueSerializer.GetBytes(value)));
                var i = Put(target, key, dataLink);

                nodeManager.Save(target);
                nodeManager.Count++;

                if (target.Count > maxChildren)
                {
                    SplitUp(target);
                }

                transaction.Commit(nodeManager.Header);
            }
        }


        private bool ContainsKeyInternal(TKey key)
        {
            NodeData temp;
            return TryFindKeyOrLeaf(key, out temp);
        }


        private int Put(NodeData target, TKey key, DataLink dataLink)
        {
            var targetKeys = LoadKeys(target);

            int index;
            if (TryFindUpperBound(key, targetKeys, comparer, out index))
            {
                throw new KeyAlreadyExistsException();
            }

            target.Insert(index, maxChildren + 1, dataLink);
            return index;
        }


        private void SplitUp(NodeData target)
        {
            NodeData targetParent;
            if (target.ParentId == NodeManager.NoId)
            {
                targetParent = CreateNode();
                nodeManager.RootNodeId = targetParent.Id;
                nodeManager.Depth++;
            }
            else
            {
                targetParent = nodeManager.Get(target.ParentId);
            }

            var leftNode = CreateNode();
            leftNode.ParentId = targetParent.Id;
            {
                var leftChildId = target.GetLink(order);
                if (leftChildId != NodeManager.NoId)
                {
                    var leftChild = nodeManager.Get(leftChildId);
                    leftChild.ParentId = leftNode.Id;
                    nodeManager.Save(leftChild);
                }
                leftNode.SetLink(0, leftChildId);
            }

            var rightNode = CreateNode();
            rightNode.ParentId = targetParent.Id;
            {
                var rightChildId = target.GetLink(target.Count);
                if (rightChildId != NodeManager.NoId)
                {
                    var rightChild = nodeManager.Get(rightChildId);
                    rightChild.ParentId = rightNode.Id;
                    nodeManager.Save(rightChild);
                }
                rightNode.SetLink(0, rightChildId);
            }

            if (targetParent.Count > maxChildren)
            {
                throw new Exception();
            }

            var targetKeys = LoadKeys(target);
            int li = 0;
            int ri = 0;
            for (int i = 0; i < targetKeys.Length; ++i)
            {
                if (i < order)
                {
                    leftNode.Insert(li, maxChildren + 1, target.GetData(i, maxChildren + 1));
                    var leftChildId = target.GetLink(i);
                    leftNode.SetLink(li, leftChildId);
                    if (leftChildId != NodeManager.NoId)
                    {
                        var leftChild = nodeManager.Get(leftChildId);
                        leftChild.ParentId = leftNode.Id;
                        nodeManager.Save(leftChild);
                    }
                    ++li;
                }
                else if (i == order)
                {
                    int index = Put(targetParent, targetKeys[order], target.GetData(order, maxChildren + 1));
                    targetParent.SetLink(index, leftNode.Id);
                    targetParent.SetLink(index + 1, rightNode.Id);
                }
                else
                {
                    rightNode.Insert(ri, maxChildren + 1, target.GetData(i, maxChildren + 1));
                    var rightChildId = target.GetLink(i);
                    rightNode.SetLink(ri, rightChildId);
                    if (rightChildId != NodeManager.NoId)
                    {
                        var rightChild = nodeManager.Get(rightChildId);
                        rightChild.ParentId = rightNode.Id;
                        nodeManager.Save(rightChild);
                    }
                    ++ri;
                }
            }

            DisposeNode(target.Id);
            nodeManager.Save(rightNode);
            nodeManager.Save(leftNode);
            nodeManager.Save(targetParent);

            if (targetParent.Count > maxChildren)
            {
                SplitUp(targetParent);
            }
        }


        private NodeData CreateNode()
        {
            return nodeManager.CreateNode();
        }


        private void DisposeNode(int nodeId)
        {
            nodeManager.DisposeNode(nodeId);
        }


        private TKey[] LoadKeys(NodeData node)
        {
            var result = new TKey[node.Count];
            for (int i = 0; i < node.Count; ++i)
            {
                result[i] = keySerializer.GetValue(nodeManager.LoadData(node.GetData(i, maxChildren + 1).KeyAddress));
            }
            return result;
        }


        private KeyValuePair<TKey, TValue>[] LoadKeyValues(NodeData node)
        {
            var result = new KeyValuePair<TKey, TValue>[node.Count];
            for (int i = 0; i < node.Count; ++i)
            {
                var address = node.GetData(i, maxChildren + 1);
                result[i] = new KeyValuePair<TKey, TValue>(
                    keySerializer.GetValue(nodeManager.LoadData(address.KeyAddress)),
                    valueSerializer.GetValue(nodeManager.LoadData(address.ValueAddress)));
            }
            return result;
        }


        private NodeData FindLeaf(TKey key)
        {
            NodeData result;
            if (TryFindKeyOrLeaf(key, out result))
            {
                throw new KeyAlreadyExistsException();
            }
            return result;
        }


        private bool TryFindKeyOrLeaf(TKey key, out NodeData keyOrLeafNode)
        {
            if (nodeManager.RootNodeId == NodeManager.NoId)
                throw new InvalidOperationException();

            var current = nodeManager.Get(nodeManager.RootNodeId);
            while (true)
            {
                int index;
                var currentKeys = LoadKeys(current);
                if (TryFindUpperBound(key, currentKeys, comparer, out index))
                {
                    keyOrLeafNode = current;
                    return true;
                }

                if (current.IsLeaf)
                {
                    break;
                }

                current = nodeManager.Get(current.GetLink(index));
            }

            keyOrLeafNode = current;
            return false;
        }


        private static bool TryFindUpperBound(TKey key, TKey[] keys, IComparer<TKey> comparer, out int index)
        {
            int keyIndex = Array.BinarySearch(keys, 0, keys.Length, key, comparer);
            if (keyIndex < 0)
            {
                index = ~keyIndex;
                return false;
            }
            index = keyIndex;
            return true;
        }

        private bool RemoveInternal(TKey key)
        {
            NodeData temp;
            if (!TryFindKeyOrLeaf(key, out temp))
            {
                return false;
            }

            var targetKeys = LoadKeys(temp);

            int index;
            if (!TryFindUpperBound(key, targetKeys, comparer, out index))
            {
                throw new InvalidOperationException();
            }

            NodeData nodeThatLostKey;
            if (!temp.IsLeaf)
            {
                // Locate adjacent key - leftmost leaf in the right subtree
                var rightSubtree = nodeManager.Get(temp.GetLink(index + 1));
                while (!rightSubtree.IsLeaf)
                {
                    rightSubtree = nodeManager.Get(rightSubtree.GetLink(0));
                }

                temp.SetData(index, maxChildren + 1, rightSubtree.GetData(0, maxChildren + 1));
                rightSubtree.RemoveAt(0, maxChildren + 1);
                nodeThatLostKey = rightSubtree;

                nodeManager.Save(temp);
                nodeManager.Save(rightSubtree);
            }
            else
            {
                temp.RemoveAt(index, maxChildren + 1);
                nodeManager.Save(temp);
                nodeThatLostKey = temp;
            }

            while (nodeThatLostKey.ParentId != NodeManager.NoId && nodeThatLostKey.Count < order)
            {
                var parentNode = nodeManager.Get(nodeThatLostKey.ParentId);
                var linkIndex = parentNode.IndexOfLink(nodeThatLostKey.Id);
                if (linkIndex < 0)
                {
                    throw new InvalidOperationException();
                }

                var parentKeys = LoadKeys(parentNode);

                var neighborLinkIndex = -1;
                bool isSmaller;

                if ((linkIndex + 1 < parentNode.Count + 1) && (parentNode.GetLink(linkIndex + 1) != NodeManager.NoId))
                {
                    isSmaller = false;
                    neighborLinkIndex = linkIndex + 1;
                }
                else
                {
                    isSmaller = true;
                    neighborLinkIndex = linkIndex - 1;
                }

                var neighbor = nodeManager.Get(parentNode.GetLink(neighborLinkIndex));
                if (neighbor.Count + nodeThatLostKey.Count >= maxChildren)
                {
                    // Redistribution
                    // During redistribution, the keys are evenly divided between the two neighboring nodes
                    while (nodeThatLostKey.Count < order)
                    {
                        if (isSmaller)
                        {
                            int keyIndex = linkIndex - 1;
                            int jj = Put(nodeThatLostKey, parentKeys[keyIndex], parentNode.GetData(keyIndex, maxChildren + 1));
                            if (jj != 0)
                            {
                                throw new InvalidOperationException();
                            }

                            var neighborLink = neighbor.GetLink(neighbor.Count);
                            nodeThatLostKey.SetLink(jj, neighborLink);
                            if (neighborLink != NodeManager.NoId)
                            {
                                var neighborChild = nodeManager.Get(neighborLink);
                                neighborChild.ParentId = nodeThatLostKey.Id;
                                nodeManager.Save(neighborChild);
                            }

                            parentNode.SetData(keyIndex, maxChildren + 1, neighbor.GetData(neighbor.Count - 1, maxChildren + 1));

                            neighbor.RemoveLinkAt(neighbor.Count, maxChildren + 1);
                            neighbor.RemoveDataAt(neighbor.Count - 1, maxChildren + 1);

                            nodeManager.Save(neighbor);
                            nodeManager.Save(nodeThatLostKey);
                            nodeManager.Save(parentNode);
                        }
                        else
                        {
                            int keyIndex = linkIndex;
                            int jj = Put(nodeThatLostKey, parentKeys[keyIndex], parentNode.GetData(keyIndex, maxChildren + 1));
                            if (jj != nodeThatLostKey.Count - 1)
                            {
                                throw new InvalidOperationException();
                            }

                            nodeThatLostKey.SetLink(jj, nodeThatLostKey.GetLink(jj + 1));
                            var neighborLink = neighbor.GetLink(0);
                            nodeThatLostKey.SetLink(jj + 1, neighborLink);
                            if (neighborLink != NodeManager.NoId)
                            {
                                var neighborChild = nodeManager.Get(neighborLink);
                                neighborChild.ParentId = nodeThatLostKey.Id;
                                nodeManager.Save(neighborChild);
                            }

                            parentNode.SetData(keyIndex, maxChildren + 1, neighbor.GetData(0, maxChildren + 1));
                            neighbor.RemoveAt(0, maxChildren + 1);

                            nodeManager.Save(neighbor);
                            nodeManager.Save(nodeThatLostKey);
                            nodeManager.Save(parentNode);
                        }
                    }
                }
                else
                {
                    // Concatenation
                    // During a concatenation, the keys are simply combined into one of the nodes, and the other is discarded
                    var neighborKeys = LoadKeys(neighbor);
                    if (isSmaller)
                    {
                        int keyIndex = linkIndex - 1;
                        int jj = Put(nodeThatLostKey, parentKeys[keyIndex], parentNode.GetData(keyIndex, maxChildren + 1));
                        {
                            var neighborLink = neighbor.GetLink(neighbor.Count);
                            nodeThatLostKey.SetLink(jj, neighborLink);
                            if (neighborLink != NodeManager.NoId)
                            {
                                var neighborChild = nodeManager.Get(neighborLink);
                                neighborChild.ParentId = nodeThatLostKey.Id;
                                nodeManager.Save(neighborChild);
                            }
                        }

                        for (int i = 0; i < neighbor.Count; ++i)
                        {
                            int j = Put(nodeThatLostKey, neighborKeys[i], neighbor.GetData(i, maxChildren + 1));

                            var neighborLink = neighbor.GetLink(i);
                            nodeThatLostKey.SetLink(j, neighborLink);
                            if (neighborLink != NodeManager.NoId)
                            {
                                var neighborChild = nodeManager.Get(neighborLink);
                                neighborChild.ParentId = nodeThatLostKey.Id;
                                nodeManager.Save(neighborChild);
                            }
                        }

                        parentNode.RemoveLinkAt(neighborLinkIndex, maxChildren + 1);
                        parentNode.RemoveDataAt(keyIndex, maxChildren + 1);

                        nodeManager.Save(nodeThatLostKey);
                        nodeManager.Save(parentNode);
                        DisposeNode(neighbor.Id);
                    }
                    else
                    {
                        int keyIndex = linkIndex;
                        int jj = Put(nodeThatLostKey, parentKeys[keyIndex], parentNode.GetData(keyIndex, maxChildren + 1));

                        nodeThatLostKey.SetLink(jj, nodeThatLostKey.GetLink(nodeThatLostKey.Count));
                        nodeThatLostKey.SetLink(nodeThatLostKey.Count, NodeManager.NoId);

                        for (int i = 0; i < neighbor.Count; ++i)
                        {
                            int j = Put(nodeThatLostKey, neighborKeys[i], neighbor.GetData(i, maxChildren + 1));

                            var neighborLink = neighbor.GetLink(i);
                            nodeThatLostKey.SetLink(j, neighborLink);
                            if (neighborLink != NodeManager.NoId)
                            {
                                var neighborChild = nodeManager.Get(neighborLink);
                                neighborChild.ParentId = nodeThatLostKey.Id;
                                nodeManager.Save(neighborChild);
                            }
                        }

                        {
                            var neighborLink = neighbor.GetLink(neighbor.Count);
                            nodeThatLostKey.SetLink(nodeThatLostKey.Count, neighborLink);
                            if (neighborLink != NodeManager.NoId)
                            {
                                var neighborChild = nodeManager.Get(neighborLink);
                                neighborChild.ParentId = nodeThatLostKey.Id;
                                nodeManager.Save(neighborChild);
                            }
                        }

                        parentNode.RemoveLinkAt(neighborLinkIndex, maxChildren + 1);
                        parentNode.RemoveDataAt(keyIndex, maxChildren + 1);

                        nodeManager.Save(nodeThatLostKey);
                        nodeManager.Save(parentNode);
                        DisposeNode(neighbor.Id);
                    }

                    if (parentNode.ParentId == NodeManager.NoId && parentNode.Count == 0)
                    {
                        var oldRoot = nodeManager.RootNodeId;
                        nodeManager.RootNodeId = parentNode.GetLink(0);
                        var newRoot = nodeManager.Get(nodeManager.RootNodeId);
                        newRoot.ParentId = NodeManager.NoId;
                        nodeManager.Save(newRoot);
                        DisposeNode(oldRoot);
                        break;
                    }
                }

                nodeThatLostKey = nodeManager.Get(nodeThatLostKey.ParentId);
                linkIndex = int.MinValue;
            }

            nodeManager.Count--;
            return true;
        }

        #endregion

        #region Visualization

        public string ToDotNotation()
        {
            var text = new StringBuilder();
            text.AppendLine("digraph g {");
            text.AppendLine("node[shape = record, height = .1];");

            var labels = new HashSet<int>();

            if (nodeManager.RootNodeId != NodeManager.NoId)
            {
                text.AppendFormat("node{0};", nodeManager.RootNodeId);
                text.AppendLine();
                text.AppendFormat("{{rank = same; node{0}; }}", nodeManager.RootNodeId);
                text.AppendLine();
            }

            // Nodes
            foreach (var node in Visit())
            {
                if (labels.Add(node.Item1.Id))
                {
                    FormatNode(text, node.Item1, node.Item1.Id);
                }
            }

            text.AppendLine();

            // Links
            var linksDone = new HashSet<int>();
            foreach (var node in Visit())
            {
                if (!labels.Contains(node.Item1.Id))
                {
                    throw new InvalidOperationException();
                }
                if (linksDone.Contains(node.Item1.Id))
                {
                    continue;
                }

                FormatLinks(text, node.Item1);
                linksDone.Add(node.Item1.Id);
            }

            text.AppendLine("}");
            return text.ToString();
        }


        private void FormatLinks(StringBuilder text, NodeData node)
        {
            bool rankEmpty = true;
            for (int i = 0; i < node.Count + 1; ++i)
            {
                var child = node.GetLink(i);
                if (child == NodeManager.NoId)
                {
                    continue;
                }

                if (rankEmpty)
                {
                    text.Append("{rank = same; ");
                    rankEmpty = false;
                }
                text.AppendFormat("node{0};", child);
            }
            if (!rankEmpty)
            {
                text.Append("} ");
                text.AppendLine();
            }

            for (int i = 0; i < node.Count + 1; ++i)
            {
                var child = node.GetLink(i);
                if (child == NodeManager.NoId)
                {
                    continue;
                }

                text.AppendFormat("node{0}:f{1}->node{2}:f0;", node.Id, i, child);
                text.AppendFormat("node{0}:f0->node{2}:f{1};", child, i, nodeManager.Get(child).ParentId);
                text.AppendLine();
            }
        }


        private void FormatNode(StringBuilder text, NodeData node, int id)
        {
            text.AppendFormat("node{0}[label = \"", id);
            var nodeKeys = LoadKeyValues(node);
            for (int i = 0; i < nodeKeys.Length; ++i)
            {
                text.AppendFormat("<f{0}>|", i);
                text.Append(nodeKeys[i].Key);
                text.Append(" - ");
                text.Append(nodeKeys[i].Value);
                text.Append("|");
            }
            text.AppendFormat("<f{0}>", nodeKeys.Length);
            text.Append("\"");
            if (node.Id == nodeManager.RootNodeId)
            {
                text.Append(", style = bold");
            }
            text.Append("];");
            text.AppendLine();
        }


        private IEnumerable<Tuple<NodeData, int>> Visit()
        {
            var stack = new Stack<Tuple<NodeData, bool, int>>();
            if (nodeManager.RootNodeId != NodeManager.NoId)
            {
                stack.Push(Tuple.Create(nodeManager.Get(nodeManager.RootNodeId), false, 0));
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                var node = current.Item1;
                var linksProcessed = current.Item2;
                var li = current.Item3;

                if (node.Count < order)
                {
                    if (node.IsLeaf || node.ParentId == 0)
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

                    if (li + 1 < node.Count)
                    {
                        stack.Push(Tuple.Create(node, false, li + 1));
                    }
                    else if (li + 1 < node.Count + 1 && node.GetLink(li + 1) != NodeManager.NoId)
                    {
                        stack.Push(Tuple.Create(nodeManager.Get(node.GetLink(li + 1)), false, 0));
                    }
                }
                else
                {
                    stack.Push(Tuple.Create(node, true, li));
                    if (li < node.Count + 1 && node.GetLink(li + 1) != NodeManager.NoId)
                    {
                        stack.Push(Tuple.Create(nodeManager.Get(node.GetLink(li)), false, 0));
                    }
                }
            }
        }

        #endregion

        #region IEnumerable<KeyValuePair<TKey, TValue>>

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Visit()
                .Select(v => LoadKeyValues(v.Item1)[v.Item2])
                .GetEnumerator();
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
            nodeManager.Clear();
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


        public int Count => nodeManager.Count;

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
            NodeData temp;
            if (!TryFindKeyOrLeaf(key, out temp))
            {
                value = default(TValue);
                return false;
            }

            var targetKeys = LoadKeys(temp);

            int index;
            if (!TryFindUpperBound(key, targetKeys, comparer, out index))
            {
                value = default(TValue);
                return false;
            }

            var targetValues = LoadKeyValues(temp);
            value = targetValues[index].Value;
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
                NodeData temp;
                if (!TryFindKeyOrLeaf(key, out temp))
                {
                    AddInternal(key, value);
                }
                else
                {
                    var targetKeys = LoadKeys(temp);

                    int index;
                    if (TryFindUpperBound(key, targetKeys, comparer, out index))
                    {
                        var oldAddress = temp.GetData(index, maxChildren + 1);
                        var newAddress = new DataLink(
                            nodeManager.SaveData(keySerializer.GetBytes(key)),
                            nodeManager.SaveData(valueSerializer.GetBytes(value)));
                        temp.SetData(index, maxChildren + 1, newAddress);
                        nodeManager.Save(temp);

                        DeleteData(oldAddress.KeyAddress);
                        DeleteData(oldAddress.ValueAddress);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }


        public void DeleteData(ulong address)
        {
            nodeManager.DeleteData(address);
        }


        public ICollection<TKey> Keys { get; }
        public ICollection<TValue> Values { get; }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            nodeManager?.Dispose();
        }

        #endregion

        #region Types

        private interface ITransaction : IDisposable
        {
            void Commit(byte[] header);

            void TouchPage(int pageId);
        }

        private class NodeManager : IDisposable
        {
            private static readonly string HeaderText = "Btree-v1";
            private static readonly int NewId = -1;
            public static readonly int NoId = 0;

            private readonly byte[] headerData;
            private readonly int maxChildren;
            private PageDataStorage storage;

            public byte[] Header
            {
                get { return headerData; }
            }

            private string Text
            {
                get { return Encoding.UTF8.GetString(headerData, 0, HeaderText.Length); }
                set { Array.Copy(Encoding.UTF8.GetBytes(HeaderText), 0, headerData, 0, HeaderText.Length); }
            }

            private int Order
            {
                get { return GetInHeader(0); }
                set { SetInHeader(value, 0); }
            }

            public int Count
            {
                get { return GetInHeader(1); }
                set { SetInHeader(value, 1); }
            }

            public int Depth
            {
                get { return GetInHeader(2); }
                set { SetInHeader(value, 2); }
            }

            private int NextEmptyId
            {
                get { return GetInHeader(3); }
                set { SetInHeader(value, 3); }
            }

            private int NextDataId
            {
                get { return GetInHeader(4); }
                set { SetInHeader(value, 4); }
            }

            public int RootNodeId
            {
                get { return GetInHeader(5); }
                set { SetInHeader(value, 5); }
            }

            public int MaxId
            {
                get { return GetInHeader(6); }
                set { SetInHeader(value, 6); }
            }

            private void SetInHeader(int value, int index)
            {
                Array.Copy(BitConverter.GetBytes(value), 0, headerData, HeaderText.Length + sizeof(int) * index, sizeof(int));
            }


            private int GetInHeader(int index)
            {
                return BitConverter.ToInt32(headerData, HeaderText.Length + sizeof(int) * index);
            }

            public NodeManager(IPersistentStorage persistentStorage, int order)
            {
                this.maxChildren = 2 * order + 1;
                this.headerData = new byte[HeaderText.Length + 7 * sizeof(int)];
                this.storage = new PageDataStorage(persistentStorage, headerData.Length, maxChildren);

                if (persistentStorage.Length == 0)
                {
                    InitializeHeader(order);
                    storage.SaveHeader(headerData);
                }
                else
                {
                    storage.ReadHeader(headerData);

                    if (Text != HeaderText)
                    {
                        throw new InvalidOperationException("Header text mismatch");
                    }

                    if (Order != order)
                    {
                        throw new InvalidOperationException("Orders to do not match");
                    }
                }
            }


            private void InitializeHeader(int order)
            {
                Text = HeaderText;
                Order = order;
                NextEmptyId = NewId;
                Count = 0;
                Depth = 0;
                MaxId = 0;
                RootNodeId = CreateNode().Id;

                var dataNode = CreateNode();
                dataNode.DataOffset = NodeData.DataHeaderLength;
                NextDataId = dataNode.Id;
                Save(dataNode);
            }


            public void Clear()
            {
                InitializeHeader(Order);
                storage.SaveHeader(headerData);
            }


            public NodeData CreateNode()
            {
                var result = Get(NextEmptyId);
                if (NextEmptyId != NewId)
                {
                    NextEmptyId = result.ParentId;
                    var cleanData = NodeData.ForId(result.Id, maxChildren);
                    Array.Copy(cleanData.Data, result.Data, cleanData.Data.Length);
                    Save(result);
                }
                return result;
            }


            public void DisposeNode(int nodeId)
            {
                var node = Get(nodeId);
                node.ParentId = NextEmptyId;
                node.Count = 0;
                NextEmptyId = node.Id;
                node.ClearData();
                Save(node);
            }


            public ITransaction StartTransaction()
            {
                return storage.StartTransaction();
            }


            public ulong SaveData(byte[] data)
            {
                //TODO: Allow to save zero length data
                if (data == null || data.Length == 0)
                {
                    throw new ArgumentNullException();
                }

                int count = data.Length;
                int offset = 0;
                int firstNodeId = NextDataId;
                var node = Get(firstNodeId);
                int nodeOffset = node.DataOffset;

                if (node.Data.Length - nodeOffset < sizeof(int))
                {
                    node = Get(NewId);
                    node.ParentId = 0;
                    node.Count = 1;
                    node.DataOffset = NodeData.DataHeaderLength;

                    firstNodeId = node.Id;
                    nodeOffset = NodeData.DataHeaderLength;
                }
                else
                {
                    node.Count++;
                }

                ulong result = (ulong)((((long)firstNodeId) << 32) | (long)nodeOffset);

                Array.Copy(BitConverter.GetBytes(data.Length), 0, node.Data, nodeOffset, sizeof(int));
                nodeOffset += sizeof(int);

                //DataNode
                //Id
                //ParentId -> Link to next data block if size of data spans multiple blocks
                //Count -> Number of data values in this block, when count = 0 node can be disposed
                //Data Offset -> Number of bytes used
                while (count > 0)
                {
                    int toCopy = Math.Min(count, node.Data.Length - nodeOffset);
                    if (toCopy == 0)
                    {
                        var prevBlock = node;
                        node = Get(NewId);
                        node.ParentId = 0;
                        node.Count = 1;
                        node.DataOffset = NodeData.DataHeaderLength;

                        prevBlock.ParentId = node.Id;
                        prevBlock.DataOffset = nodeOffset;
                        Save(prevBlock);
                        nodeOffset = NodeData.DataHeaderLength;
                        continue;
                    }

                    Array.Copy(data, offset, node.Data, nodeOffset, toCopy);
                    nodeOffset += toCopy;
                    offset += toCopy;
                    count -= toCopy;
                }

                NextDataId = node.Id;
                node.DataOffset = nodeOffset;
                Save(node);
                return result;
            }


            public byte[] LoadData(ulong address)
            {
                var index = (int) (address >> 32);
                int offset = (int)(address & 0xFFFFFFFF);

                var firstNode = Get(index);
                var count = BitConverter.ToInt32(firstNode.Data, offset);
                var result = new byte[count];
                offset += sizeof(int);

                var currentNode = firstNode;
                var read = 0;
                while (true)
                {
                    int toCopy = Math.Min(count, currentNode.DataOffset - offset);
                    Array.Copy(currentNode.Data, offset, result, read, toCopy);
                    count -= toCopy;
                    read += toCopy;

                    if (count == 0)
                    {
                        break;
                    }

                    if (currentNode.ParentId == NodeManager.NoId)
                    {
                        break;
                    }

                    currentNode = Get(currentNode.ParentId);
                    offset = NodeData.DataHeaderLength;
                }

                if (count > 0)
                {
                    throw new InvalidOperationException();
                }

                return result;
            }

            public void DeleteData(ulong address)
            {
                var index = (int)(address >> 32);
                int offset = (int)(address & 0xFFFFFFFF);

                var firstNode = Get(index);
                var count = BitConverter.ToInt32(firstNode.Data, offset);
                offset += sizeof(int);

                var currentNode = firstNode;
                while (true)
                {
                    int toCopy = Math.Min(count, currentNode.DataOffset - offset);
                    count -= toCopy;

                    if (count == 0)
                    {
                        break;
                    }

                    if (currentNode.ParentId == NodeManager.NoId)
                    {
                        break;
                    }

                    var nextNode = Get(currentNode.ParentId);

                    currentNode.Count--;
                    if (currentNode.Count == 0)
                    {
                        DisposeNode(currentNode.Id);
                    }
                    else
                    {
                        Save(currentNode);
                    }

                    currentNode = nextNode;
                    offset = NodeData.DataHeaderLength;
                }

                currentNode.Count--;
                if (currentNode.Count == 0)
                {
                    DisposeNode(currentNode.Id);
                }
                else
                {
                    Save(currentNode);
                }
            }

            public NodeData Get(int index)
            {
                if (index == NewId)
                {
                    int id = GetNewId();
                    var newNode = NodeData.ForId(id, maxChildren);
                    storage.SaveNode(id, newNode.Data);
                    return newNode;
                }

                if (index == NoId)
                {
                    throw new ArgumentException();
                }

                var data = new NodeData(storage.ReadNode(index));
                return data;
            }


            public void Save(NodeData node)
            {
                storage.SaveNode(node.Id, node.Data);
            }


            public void Dispose()
            {
                storage?.Dispose();
            }

            private int GetNewId()
            {
                MaxId = MaxId + 1;
                return MaxId;
            }
        }

        private class PageDataStorage : IDisposable
        {
            private readonly int pageSize = 4096;
            private readonly int maxPagesInCache = 1000;
            private readonly int pageFooterSize = sizeof(int);

            private readonly object syncRoot;
            private readonly Dictionary<int, byte[]> pageCache;
            private readonly int nodePerPage;
            private readonly int maxChildren;

            private ITransaction currentTransaction;
            private readonly IPersistentStorage persistentStorage;
            private readonly int headerSize;


            public PageDataStorage(IPersistentStorage persistentStorage, int headerSize, int maxChildren)
            {
                ArgumentNullException.ThrowIfNull(persistentStorage);

                this.persistentStorage = persistentStorage;
                this.syncRoot = new object();
                this.maxChildren = maxChildren;
                this.pageCache = new Dictionary<int, byte[]>();
                this.nodePerPage = (pageSize - pageFooterSize) / NodeData.Size(maxChildren);
                this.headerSize = headerSize;

                if (this.nodePerPage == 0)
                {
                    throw new ArgumentException($"Page is too small to accommodate one node, should be at least {NodeData.Size(maxChildren) + pageFooterSize} bytes");
                }
            }


            public void SaveNode(int id, byte[] data)
            {
                var pageId = GetPageId(id);
                var pageOffset = GetOffset(id);
                var pageData = GetPage(pageId, 0x140DEFFF, true);
                Array.Copy(data, 0, pageData, pageOffset, data.Length);

                if (currentTransaction != null)
                {
                    lock (syncRoot)
                    {
                        currentTransaction?.TouchPage(pageId);
                    }
                }
            }


            public byte[] ReadNode(int id)
            {
                var pageId = GetPageId(id);
                var pageOffset = GetOffset(id);
                var pageData = GetPage(pageId, 0x140DEFFF, false);
                var data = new byte[NodeData.Size(maxChildren)];
                Array.Copy(pageData, pageOffset, data, 0, data.Length);
                return data;
            }


            private int GetPageId(int id)
            {
                return id / nodePerPage;
            }


            private int GetOffset(int id)
            {
                return NodeData.Size(maxChildren) * (id % nodePerPage);
            }


            private byte[] GetPage(int pageId, uint footer, bool canBeNew)
            {
                byte[] rawBytes;
                if (!pageCache.TryGetValue(pageId, out rawBytes))
                {
                    rawBytes = new byte[pageSize];
                    if (persistentStorage.Length < (pageId + 1) * pageSize)
                    {
                        if (!canBeNew)
                        {
                            throw new InvalidOperationException($"No page {pageId}");
                        }
                        Array.Copy(BitConverter.GetBytes(footer), 0, rawBytes, pageSize - pageFooterSize, pageFooterSize);
                    }
                    else
                    {
                        persistentStorage.ReadAll(CalculatePageOffset(pageId), rawBytes, 0, pageSize);
                    }
                    pageCache.Add(pageId, rawBytes);
                }
                else
                {
                    if (footer != BitConverter.ToUInt32(rawBytes, pageSize - pageFooterSize))
                    {
                        throw new InvalidOperationException("Unexpected data page type");
                    }
                }
                return rawBytes;
            }


            public ITransaction StartTransaction()
            {
                if (currentTransaction == null)
                {
                    lock (syncRoot)
                    {
                        if (currentTransaction == null)
                        {
                            currentTransaction = new Transaction(this);
                            return currentTransaction;
                        }
                    }
                }
                throw new NotSupportedException();
            }


            private void CommitCurrentTransaction(byte[] header, IEnumerable<int> changedPages)
            {
                lock (syncRoot)
                {
                    if (currentTransaction == null)
                    {
                        throw new InvalidOperationException("Not in transaction");
                    }

                    foreach (var pageId in changedPages.OrderBy(p => p))
                    {
                        persistentStorage.WriteAll(CalculatePageOffset(pageId), pageCache[pageId], 0, pageSize);

                        if (pageCache.Count > maxPagesInCache)
                        {
                            pageCache.Remove(pageId);
                        }
                    }

                    SaveHeader(header);

                    persistentStorage.Flush();
                    currentTransaction = null;
                }
            }


            public void ReadHeader(byte[] header)
            {
                persistentStorage.ReadAll(0, header, 0, header.Length);
            }


            public void SaveHeader(byte[] header)
            {
                var headerSizeCalculated = CalculatePageOffset(0);
                if (header.Length != headerSizeCalculated)
                {
                    throw new InvalidOperationException("Bad header size");
                }
                persistentStorage.WriteAll(0, header, 0, header.Length);
            }


            private void RollbackCurrentTransaction()
            {
                lock (syncRoot)
                {
                    if (currentTransaction == null)
                    {
                        throw new InvalidOperationException("Not in transaction");
                    }
                    currentTransaction = null;
                }
            }


            private long CalculatePageOffset(int pageId)
            {
                return headerSize + pageId * pageSize;
            }


            private class Transaction : ITransaction
            {
                private readonly PageDataStorage owner;
                private readonly HashSet<int> pages;
                private bool commited;


                public Transaction(PageDataStorage owner)
                {
                    this.owner = owner;
                    this.pages = new HashSet<int>();
                    this.commited = false;
                }


                public void TouchPage(int pageId)
                {
                    pages.Add(pageId);
                }


                public void Commit(byte[] header)
                {
                    owner.CommitCurrentTransaction(header, pages);
                    commited = true;
                }


                public void Dispose()
                {
                    if (!commited)
                    {
                        owner.RollbackCurrentTransaction();
                    }
                }
            }


            public void Dispose()
            {
                currentTransaction?.Dispose();
                persistentStorage?.Dispose();
            }
        }

        private struct DataLink
        {
            public static readonly int SizeInBytes = 2 * sizeof(ulong);

            public readonly ulong KeyAddress;
            public readonly ulong ValueAddress;

            public DataLink(ulong keyAddress, ulong valueAddress)
            {
                KeyAddress = keyAddress;
                ValueAddress = valueAddress;
            }


            public byte[] GetBytes()
            {
                var buffer = new byte[SizeInBytes];
                Array.Copy(BitConverter.GetBytes(KeyAddress), 0, buffer, 0, sizeof(ulong));
                Array.Copy(BitConverter.GetBytes(ValueAddress), 0, buffer, sizeof(ulong), sizeof(ulong));
                return buffer;
            }


            public static DataLink FromBytes(byte[] buffer, int offset)
            {
               return new DataLink(
                    BitConverter.ToUInt64(buffer, offset),
                    BitConverter.ToUInt64(buffer, offset + sizeof(long)));
            }
        }

        private struct NodeData
        {
            public static readonly int HeaderLength = 3 * sizeof(int);
            public static readonly int DataHeaderLength = HeaderLength + sizeof(int);

            private readonly byte[] data;


            public NodeData(byte[] data)
            {
                this.data = data;
            }


            public byte[] Data => data;

            public int Id => BitConverter.ToInt32(data, 0);

            public int ParentId
            {
                get { return BitConverter.ToInt32(data, sizeof(int)); }
                set { Array.Copy(BitConverter.GetBytes(value), 0, data, sizeof(int), sizeof(int)); }
            }

            public int Count
            {
                get { return BitConverter.ToInt32(data, 2 * sizeof(int)); }
                set { Array.Copy(BitConverter.GetBytes(value), 0, data, 2 * sizeof(int), sizeof(int)); }
            }

            public int DataOffset
            {
                get { return BitConverter.ToInt32(data, 3 * sizeof(int)); }
                set { Array.Copy(BitConverter.GetBytes(value), 0, data, 3 * sizeof(int), sizeof(int)); }
            }

            public bool IsLeaf
            {
                get
                {
                    for (int i = 0; i < Count; ++i)
                    {
                        if (GetLink(i) != NodeManager.NoId)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }


            public int GetLink(int index)
            {
                if (index > Count + 1)
                {
                    throw new ArgumentException(nameof(index));
                }
                return BitConverter.ToInt32(data, HeaderLength + index * sizeof(int));
            }


            public void SetLink(int index, int id)
            {
                if (index > Count + 1)
                {
                    throw new ArgumentException(nameof(index));
                }
                Array.Copy(BitConverter.GetBytes(id), 0, data, HeaderLength + index * sizeof(int), sizeof(int));
            }


            public DataLink GetData(int index, int maxChildren)
            {
                int offset = HeaderLength + (maxChildren + 1) * sizeof(int) + index * DataLink.SizeInBytes;
                return DataLink.FromBytes(data, offset);
            }


            public void SetData(int index, int maxChildren, DataLink location)
            {
                int offset = HeaderLength + (maxChildren + 1) * sizeof(int) + index * DataLink.SizeInBytes;
                var bytes = location.GetBytes();

                if (DataLink.SizeInBytes != bytes.Length)
                {
                    throw new ArgumentException();
                }

                Array.Copy(bytes, 0, data, offset, bytes.Length);
            }


            public static int Size(int maxChildren)
            {
                return HeaderLength + // id + parent id + count
                       (maxChildren + 1) * sizeof(int) + // Link
                       (maxChildren) * DataLink.SizeInBytes; // Data Link
            }


            /// <summary>
            /// Create empty node with a given id
            /// </summary>
            public static NodeData ForId(int id, int maxChildren)
            {
                var data = new byte[Size(maxChildren)];
                Array.Copy(BitConverter.GetBytes(id), 0, data, 0, sizeof(int));
                Array.Copy(BitConverter.GetBytes(0), 0, data, sizeof(int), sizeof(int));
                Array.Copy(BitConverter.GetBytes(0), 0, data, sizeof(int), sizeof(int));
                return new NodeData(data);
            }


            public void Insert(int index, int maxChildren, DataLink dataLink)
            {
                int count = Count;
                if (count + 1 > maxChildren || index > count)
                    throw new InvalidOperationException();

                Count = count + 1;
                for (int i = count; i > index; --i)
                {
                    SetData(i, maxChildren, GetData(i - 1, maxChildren));
                }
                for (int i = count + 1; i > index; --i)
                {
                    SetLink(i, GetLink(i - 1));
                }
                SetLink(index, NodeManager.NoId);
                SetData(index, maxChildren, dataLink);
            }


            public void RemoveAt(int position, int maxChildren)
            {
                int count = Count;
                if (count == 0)
                    throw new InvalidOperationException();

                Count = count - 1;
                for (int i = position; i < count - 1; ++i)
                {
                    SetData(i, maxChildren, GetData(i + 1, maxChildren));
                }
                for (int i = position; i < count; ++i)
                {
                    SetLink(i, GetLink(i + 1));
                }
            }


            public void RemoveLinkAt(int position, int maxChildren)
            {
                int count = Count;
                if (count == 0)
                    throw new InvalidOperationException();

                for (int i = position; i < count; ++i)
                {
                    SetLink(i, GetLink(i + 1));
                }
            }


            public void RemoveDataAt(int position, int maxChildren)
            {
                int count = Count;
                if (count == 0)
                    throw new InvalidOperationException();

                Count = count - 1;
                for (int i = position; i < count - 1; ++i)
                {
                    SetData(i, maxChildren, GetData(i + 1, maxChildren));
                }
            }


            public int IndexOfLink(int id)
            {
                int count = Count;
                for (int i = 0; i < count + 1; ++i)
                {
                    if (GetLink(i) == id)
                    {
                        return i;
                    }
                }
                return -1;
            }


            public void ClearData()
            {
                for (int i = HeaderLength; i < data.Length; ++i)
                {
                    data[i] = 0xFE;
                }
            }
        }

        #endregion
    }
}
