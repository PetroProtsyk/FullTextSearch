using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core.Collections
{
    /// <summary>
    /// Persistent Dictionary based on the Ternary Search Tree
    /// 
    /// https://en.wikipedia.org/wiki/Ternary_search_tree
    /// http://www.cs.princeton.edu/~rs/strings/
    /// </summary>
    public class TernaryDictionary<TKey, TValue> : IDisposable
    {
        #region Fields

        private readonly IFixedSizeDataSerializer<TKey> keySerializer;
        private readonly IFixedSizeDataSerializer<TValue> valueSerializer;

        private readonly IComparer<TKey> comparer;
        private readonly NodeManager nodeManager;
        #endregion

        #region Properties

        public int Count
        {
            get { return nodeManager.Count; }
        }

        #endregion

        #region Constructors

        public TernaryDictionary()
            : this(new MemoryStorage(), Comparer<TKey>.Default) { }

        public TernaryDictionary(IPersistentStorage persistentStorage)
            : this(persistentStorage, Comparer<TKey>.Default) { }

        public TernaryDictionary(IComparer<TKey> comparer)
            : this(new MemoryStorage(), comparer) { }

        public TernaryDictionary(IPersistentStorage persistentStorage, IComparer<TKey> comparer)
        {
            this.comparer = comparer;
            this.keySerializer = (IFixedSizeDataSerializer<TKey>)DataSerializer.GetDefault<TKey>();
            this.valueSerializer = (IFixedSizeDataSerializer<TValue>)DataSerializer.GetDefault<TValue>();
            this.nodeManager = new NodeManager(persistentStorage, keySerializer, valueSerializer);
        }

        #endregion

        #region Methods

        public IUpdate StartUpdate()
        {
            return new Update(nodeManager.StartUpdate());
        }

        /// <summary>
        // Add element
        /// </summary>
        public bool Add(IEnumerable<TKey> item, TValue value)
        {
            return AddOrGet(item, value, out var currentValue);
        }

        /// <summary>
        // Add element or get current
        /// </summary>
        public bool AddOrGet(IEnumerable<TKey> item, TValue value, out TValue currentValue)
        {
            ArgumentNullException.ThrowIfNull(item);

            var sequence = item.GetEnumerator();
            if (!sequence.MoveNext())
            {
                throw new ArgumentNullException(nameof(item));
            }

            return InsertNonRecursive(sequence, value, out currentValue);
        }

        /// <summary>
        /// Non recursive version
        /// </summary>
        private bool InsertNonRecursive(IEnumerator<TKey> sequence, TValue value, out TValue currentValue)
        {
            using (var update = StartUpdate())
            {
                var transaction = ((Update) update).GetTransaction();
                var current = transaction.Get(transaction.RootNodeId);
                if (transaction.RootNodeId == NodeManager.NewId)
                {
                    transaction.RootNodeId = current.Id;
                    current.Split = sequence.Current;
                }

                bool inserted = false;

                while (true)
                {
                    var label = sequence.Current;
                    while (true)
                    {
                        var next = comparer.Compare(label, current.Split);
                        if (next == 0)
                        {
                            break;
                        }

                        if (next < 0)
                        {
                            if (current.Lokid == NodeManager.NoId)
                            {
                                var newNode = transaction.Get(NodeManager.NewId);

                                newNode.Split = label;
                                current.Lokid = newNode.Id;
                            }

                            current = transaction.Get(current.Lokid);
                        }
                        else
                        {
                            if (current.Hikid == NodeManager.NoId)
                            {
                                var newNode = transaction.Get(NodeManager.NewId);
                                newNode.Split = label;
                                current.Hikid = newNode.Id;
                            }

                            current = transaction.Get(current.Hikid);
                        }
                    }

                    if (!sequence.MoveNext())
                    {
                        if (!current.IsFinal)
                        {
                            current.IsFinal = true;
                            current.Value = value;

                            transaction.Count++;
                            inserted = true;
                            currentValue = value;
                        }
                        else
                        {
                            currentValue = current.Value;
                        }
                        break;
                    }
                    else
                    {
                        if (current.Eqkid == NodeManager.NoId)
                        {
                            var newNode = transaction.Get(NodeManager.NewId);
                            newNode.Split = sequence.Current;

                            current.Eqkid = newNode.Id;
                        }

                        current = transaction.Get(current.Eqkid);
                    }
                }

                if (inserted)
                {
                    update.Commit();
                }

                return inserted;
            }
        }

        /// <summary>
        /// Match values in the tree
        /// </summary>
        public IEnumerable<IEnumerable<TKey>> Match(IDfaMatcher<TKey> matcher)
        {
            var prefix = new List<TKey>();
            var stack = new Stack<KeyValuePair<int, bool>>();

            if (nodeManager.RootNodeId != NodeManager.NoId &&
                nodeManager.RootNodeId != NodeManager.NewId)
            {
                stack.Push(new KeyValuePair<int, bool>(nodeManager.RootNodeId, false));
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current.Key == NodeManager.NoId)
                {
                    matcher.Pop();
                    prefix.RemoveAt(prefix.Count - 1);
                    continue;
                }

                var node = nodeManager.Get(current.Key);

                if (current.Value)
                {
                    matcher.Next(node.Split);
                    prefix.Add(node.Split);

                    if (node.IsFinal && matcher.IsFinal())
                    {
                        yield return prefix;
                    }
                    continue;
                }

                if (node.Hikid != NodeManager.NoId)
                {
                    stack.Push(new KeyValuePair<int, bool>(node.Hikid, false));
                }

                if (matcher.Next(node.Split))
                {
                    matcher.Pop();

                    stack.Push(new KeyValuePair<int, bool>(NodeManager.NoId, false));

                    if (node.Eqkid != NodeManager.NoId)
                    {
                        stack.Push(new KeyValuePair<int, bool>(node.Eqkid, false));
                    }

                    stack.Push(new KeyValuePair<int, bool>(current.Key, true));
                }

                if (node.Lokid != NodeManager.NoId)
                {
                    stack.Push(new KeyValuePair<int, bool>(node.Lokid, false));
                }
            }
        }


        /// <summary>
        /// Check if item is in the tree
        /// </summary>
        public bool Contains(IEnumerable<TKey> s)
        {
            return TryGet(s, out _);
        }

        public bool TryGet(IEnumerable<TKey> s, out TValue value)
        {
            if (nodeManager.RootNodeId == NodeManager.NoId ||
                nodeManager.RootNodeId == NodeManager.NewId)
            {
                value = default;
                return false;
            }

            var parent = default(NodeData);
            var currentId = nodeManager.RootNodeId;
            foreach (var label in s)
            {
                while (true)
                {
                    if (currentId == NodeManager.NoId)
                    {
                        value = default;
                        return false;
                    }

                    var current = nodeManager.Get(currentId);
                    var compare = comparer.Compare(label, current.Split);
                    if (compare == 0)
                    {
                        parent = current;
                        currentId = current.Eqkid;
                        break;
                    }

                    currentId = compare < 0 ? current.Lokid : current.Hikid;
                }
            }

            if (parent.Data == null || !parent.IsFinal)
            {
                value = default;
                return false;
            }

            value = parent.Value;
            return true;
        }
        #endregion

        #region Visualization

        public string ToDotNotation()
        {
            var text = new StringBuilder();
            text.AppendLine("digraph g {");
            text.AppendLine("node[shape = circle];");

            var labels = new Dictionary<int, int>();
            // Nodes
            foreach (var node in Visit())
            {
                int index = 0;
                if (!labels.TryGetValue(node.Id, out index))
                {
                    index = labels.Count + 1;
                    labels.Add(node.Id, index);
                }

                if (node.IsFinal)
                {
                    text.AppendLine($"node{index}[shape = doublecircle, style = bold, label=\"{node.Split}\"]");
                }
                else
                {
                    text.AppendLine($"node{index}[label=\"{node.Split}-{node.Id}\"]");
                }

                if (node.Lokid != NodeManager.NoId)
                {
                    int childIndex;
                    if (!labels.TryGetValue(node.Lokid, out childIndex))
                    {
                        childIndex = labels.Count + 1;
                        labels.Add(node.Lokid, childIndex);
                    }
                    text.AppendLine($"node{index} -> node{childIndex}");
                }

                if (node.Eqkid != NodeManager.NoId)
                {
                    if (!labels.TryGetValue(node.Eqkid, out int childIndex))
                    {
                        childIndex = labels.Count + 1;
                        labels.Add(node.Eqkid, childIndex);
                    }
                    text.AppendLine($"node{index} -> node{childIndex}");
                }

                if (node.Hikid != NodeManager.NoId)
                {
                    int childIndex;
                    if (!labels.TryGetValue(node.Hikid, out childIndex))
                    {
                        childIndex = labels.Count + 1;
                        labels.Add(node.Hikid, childIndex);
                    }
                    text.AppendLine($"node{index} -> node{childIndex}");
                }
            }

            text.AppendLine("}");
            return text.ToString();
        }

        private IEnumerable<NodeData> Visit()
        {
            var stack = new Stack<KeyValuePair<int, bool>>();

            if (nodeManager.RootNodeId != NodeManager.NoId)
            {
                stack.Push(new KeyValuePair<int, bool>(nodeManager.RootNodeId, false));
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var node = nodeManager.Get(current.Key);

                if (current.Value)
                {
                    yield return node;
                    continue;
                }

                if (node.Hikid != NodeManager.NoId)
                {
                    stack.Push(new KeyValuePair<int, bool>(node.Hikid, false));
                }

                if (node.Eqkid != NodeManager.NoId)
                {
                    stack.Push(new KeyValuePair<int, bool>(node.Eqkid, false));
                }

                stack.Push(new KeyValuePair<int, bool>(current.Key, true));

                if (node.Lokid != NodeManager.NoId)
                {
                    stack.Push(new KeyValuePair<int, bool>(node.Lokid, false));
                }
            }
        }
        #endregion

        #region Types
        private readonly struct NodeData
        {
            private readonly byte[] data;
            private readonly IFixedSizeDataSerializer<TKey> keySerializer;
            private readonly IFixedSizeDataSerializer<TValue> valueSerializer;

            public byte[] Data => data;

            public NodeData(IFixedSizeDataSerializer<TKey> keySerializer, IFixedSizeDataSerializer<TValue> valueSerializer, byte[] data)
            {
                this.data = data;
                this.keySerializer = keySerializer;
                this.valueSerializer = valueSerializer;
            }

            public TKey Split
            {
                get { return keySerializer.GetValue(data); }
                set
                {
                    var splitData = keySerializer.GetBytes(value);

                    Array.Copy(splitData, 0, data, 0, splitData.Length);
                }
            }

            public TValue Value
            {
                get { return valueSerializer.GetValue(data, keySerializer.Size); }
                set
                {
                    var splitData = valueSerializer.GetBytes(value);
                    Array.Copy(splitData, 0, data, keySerializer.Size, splitData.Length);
                }
            }

            public int Id
            {
                get { return GetInt(0); }
                set { SetInt(value, 0); }
            }

            public int Lokid
            {
                get { return GetInt(1); }
                set { SetInt(value, 1); }
            }

            public int Eqkid
            {
                get { return GetInt(2); }
                set { SetInt(value, 2); }
            }

            public int Hikid
            {
                get { return GetInt(3); }
                set { SetInt(value, 3); }
            }

            public bool IsFinal
            {
                get { return data[valueSerializer.Size + keySerializer.Size] != 0; }
                set { data[valueSerializer.Size + keySerializer.Size] = (byte)(value ? 1 : 0); }
            }

            private void SetInt(int value, int index)
            {
                int offset = valueSerializer.Size + keySerializer.Size + 1 + sizeof(int) * index;

                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
            }

            private int GetInt(int index)
            {
                return BitConverter.ToInt32(data, valueSerializer.Size + keySerializer.Size + 1 + sizeof(int) * index);
            }

            public static int Size(int dataSize)
            {
                return dataSize + 1 + 4 * sizeof(int) + 1;
            }
        }

        private sealed class Header
        {
            private static ReadOnlySpan<byte> HeaderBytes => "TDict-v01"u8;
            private readonly byte[] headerData;

            public byte[] Data
            {
                get { return headerData; }
            }

            private string Text
            {
                get { return Encoding.UTF8.GetString(headerData, 0, HeaderBytes.Length); }
            }

            public int Count
            {
                get { return GetInHeader(1); }
                set { SetInHeader(value, 1); }
            }

            public int RootNodeId
            {
                get { return GetInHeader(2); }
                set { SetInHeader(value, 2); }
            }

            public int NextId
            {
                get { return GetInHeader(3); }
                set { SetInHeader(value, 3); }
            }

            private void SetInHeader(int value, int index)
            {
                Array.Copy(BitConverter.GetBytes(value), 0, headerData, HeaderBytes.Length + sizeof(int) * index, sizeof(int));
            }

            private int GetInHeader(int index)
            {
                return BitConverter.ToInt32(headerData, HeaderBytes.Length + sizeof(int) * index);
            }

            public Header()
                : this(new byte[HeaderBytes.Length + 4 * sizeof(int)])
            {
                CleanHeader();
            }

            public Header(byte[] headerData)
            {
                ArgumentNullException.ThrowIfNull(headerData);

                this.headerData = headerData;
            }

            public Header Copy()
            {
                var data = new byte[headerData.Length];

                headerData.CopyTo(data.AsSpan());

                return new Header(data);
            }

            public void CleanHeader()
            {
                HeaderBytes.CopyTo(headerData);
                Count = 0;
                NextId = 1;
                RootNodeId = NodeManager.NewId;
            }

            public void ReadHeader(IPersistentStorage persistentStorage)
            {
                persistentStorage.ReadAll(0, headerData);

                if (!headerData.AsSpan(0, HeaderBytes.Length).SequenceEqual(HeaderBytes))
                {
                    throw new InvalidOperationException("Header text mismatch");
                }
            }

            public void SaveHeader(IPersistentStorage persistentStorage)
            {
                persistentStorage.WriteAll(0, headerData);
            }
        }

        private interface ITransaction
        {
            int RootNodeId { get; set; }
            int Count { get; set; }
            NodeData Get(int index);
            void Commit();
            void Rollback();
        }

        private class Update : IUpdate
        {
            private readonly ITransaction transaction;
            private bool finalized;

            public Update(ITransaction transaction)
            {
                this.transaction = transaction;
                this.finalized = false;
            }

            public void Dispose()
            {
                Rollback();
            }

            public void Commit()
            {
                if (finalized)
                {
                    throw new InvalidOperationException();
                }

                try
                {
                    transaction.Commit();
                }
                finally
                {
                    finalized = true;
                }
            }


            public void Rollback()
            {
                if (finalized)
                {
                    return;
                }

                try
                {
                    transaction.Rollback();
                }
                finally
                {
                    finalized = true;
                }
            }

            public ITransaction GetTransaction()
            {
                return transaction;
            }
        }

        private sealed class NodeManager : IDisposable
        {
            public const int NewId = -1;
            public const int NoId = 0;

            private readonly IPersistentStorage persistentStorage;
            private readonly IFixedSizeDataSerializer<TKey> keySerializer;
            private readonly IFixedSizeDataSerializer<TValue> valueSerializer;
            private readonly Dictionary<int, NodeData> cache = new();
            private readonly object syncRoot;

            private Transaction activeTransaction;

            public Header Header
            {
                get;
            }

            public int Count => Header.Count;

            public int RootNodeId => Header.RootNodeId;

            public int NextId => Header.NextId;

            public NodeManager(IPersistentStorage persistentStorage, IFixedSizeDataSerializer<TKey> keySerializer, IFixedSizeDataSerializer<TValue> valueSerializer)
            {
                ArgumentNullException.ThrowIfNull(persistentStorage);
                ArgumentNullException.ThrowIfNull(keySerializer);
                ArgumentNullException.ThrowIfNull(valueSerializer);

                this.syncRoot = new object();
                this.Header = new Header();
                this.persistentStorage = persistentStorage;
                this.keySerializer = keySerializer;
                this.valueSerializer = valueSerializer;

                if (persistentStorage.Length == 0)
                {
                    Header.SaveHeader(persistentStorage);
                }
                else
                {
                    Header.ReadHeader(persistentStorage);
                }
            }


            public ITransaction StartUpdate()
            {
                lock (syncRoot)
                {
                    if (activeTransaction == null)
                    {
                        activeTransaction = new Transaction(this, Header);
                    }
                    else
                    {
                        activeTransaction.AddRef();
                    }
                }

                return activeTransaction;
            }

            private void FinishUpdate()
            {
                lock (syncRoot)
                {
                    activeTransaction = null;
                    cache.Clear();
                }
            }

            public NodeData Get(int index)
            {
                if (index == NewId || index == NoId)
                {
                    throw new ArgumentException();
                }

                NodeData result;
                if (!cache.TryGetValue(index, out result))
                {
                    result = Read(index);
                    cache.Add(index, result);
                }

                return result;
            }

            private NodeData Read(int id)
            {
                var offset = CalculateNodeOffset(id);
                var data = new byte[NodeData.Size(keySerializer.Size + valueSerializer.Size)];

                persistentStorage.ReadAll(offset, data);
                return new NodeData(keySerializer, valueSerializer, data);
            }

            private void Save(in NodeData node)
            {
                var offset = CalculateNodeOffset(node.Id);
                persistentStorage.WriteAll(offset, node.Data);
            }

            private void SaveHeader(Header newHeader)
            {
                newHeader.SaveHeader(persistentStorage);

                Header.ReadHeader(persistentStorage);
            }

            private long CalculateNodeOffset(long id)
            {
                return Header.Data.Length + id * NodeData.Size(keySerializer.Size + valueSerializer.Size);
            }

            public void Dispose()
            {
                persistentStorage?.Dispose();
            }

            private sealed class Transaction : ITransaction
            {
                private readonly Header header;
                private readonly NodeManager nodeManager;
                private readonly IFixedSizeDataSerializer<TKey> keySerializer;
                private readonly IFixedSizeDataSerializer<TValue> valueSerializer;
                private readonly Dictionary<int, NodeData> cache = new();
                private int depth;
                private bool finalized;

                public int Count
                {
                    get { return header.Count; }
                    set { header.Count = value; }
                }

                public int RootNodeId
                {
                    get { return header.RootNodeId; }
                    set { header.RootNodeId = value; }
                }

                public Transaction(NodeManager nodeManager, Header header)
                {
                    this.header = header.Copy();
                    this.nodeManager = nodeManager;
                    this.keySerializer = nodeManager.keySerializer;
                    this.valueSerializer = nodeManager.valueSerializer;
                    this.depth = 1;
                    this.finalized = false;
                }

                public NodeData Get(int index)
                {
                    if (finalized)
                    {
                        throw new InvalidOperationException();
                    }

                    if (index == NodeManager.NewId)
                    {
                        var data = new byte[NodeData.Size(keySerializer.Size + valueSerializer.Size)];
                        var newNode = new NodeData(keySerializer, valueSerializer, data);
                        newNode.Id = header.NextId;
                        header.NextId++;
                        cache.Add(newNode.Id, newNode);
                        return newNode;
                    }

                    NodeData result;
                    if (!cache.TryGetValue(index, out result))
                    {
                        result = nodeManager.Get(index);
                        cache.Add(index, result);
                    }

                    return result;
                }


                public void Commit()
                {
                    Finalize(true);
                }

                public void Rollback()
                {
                    Finalize(false);
                }

                private void Finalize(bool save)
                {
                    if (finalized)
                    {
                        throw new InvalidOperationException();
                    }

                    var result = Interlocked.Decrement(ref depth);

                    if (result < 0)
                    {
                        throw new InvalidOperationException();
                    }

                    if (result > 0)
                    {
                        return;
                    }

                    try
                    {
                        if (save)
                        {
                            foreach (var nodeData in cache.OrderBy(d => d.Key))
                            {
                                nodeManager.Save(nodeData.Value);
                            }

                            nodeManager.SaveHeader(header);
                        }

                        nodeManager.FinishUpdate();
                        cache?.Clear();
                    }
                    finally
                    {
                        finalized = true;
                    }
                }

                public void AddRef()
                {
                    Interlocked.Increment(ref depth);
                }
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            nodeManager?.Dispose();
        }
        #endregion
    }

    /// <summary>
    /// Transaction, allows to make batch updates to dictionary efficiently
    /// </summary>
    public interface IUpdate : IDisposable
    {
        void Commit();

        void Rollback();
    }
}
