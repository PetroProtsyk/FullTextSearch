using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common;
using Protsyk.PMS.FullText.Core.Common.Persistance;
using StringComparer = System.StringComparer;

namespace Protsyk.PMS.FullText.Core.Automata
{
    public static class FSTExtensions
    {
        public static bool TryMatch<T>(this FST<T> fst, IEnumerable<char> input, out T value)
        {
            var outputType = fst.OutputType;
            var v = outputType.Zero();
            var s = fst.Initial;
            foreach (var c in input)
            {
                if (fst.TryMove(s, c, out var to, out var o))
                {
                    s = to;
                    v = outputType.Sum(v, o);
                }
                else
                {
                    value = outputType.Zero();
                    return false;
                }
            }

            value = v;
            return fst.IsFinal(s);
        }
    }

    public class FSTBuilder<T> : IDisposable
    {
        #region Fields
        private const int InitialWordSize = 64;

        private int minimizedStateCacheSize = 65000;

        private readonly IDictionary<int, StateWithTransitions> frozenStates;

        private readonly IDictionary<int, List<LinkedListNode<int>>> minimalTransducerStatesDictionary;

        private readonly LinkedList<int> usageQueue;

        private readonly IFSTOutput<T> outputType;

        private IPersistentStorage storage;

        private int maxWordSize;

        private StateWithTransitions[] tempState;

        private string previousWord;

        private byte[] writeBuffer;

        private FSTBuilderStat stat;
        #endregion

        public FSTBuilder(IFSTOutput<T> outputType)
            : this(outputType, 65000, new MemoryStorage())
        { }

        public FSTBuilder(IFSTOutput<T> outputType, int cacheSize, IPersistentStorage storage)
        {
            ArgumentNullException.ThrowIfNull(outputType);
            ArgumentNullException.ThrowIfNull(storage);

            if (storage.Length != 0)
            {
                throw new InvalidOperationException("Storage is not empty");
            }

            this.frozenStates = new Dictionary<int, StateWithTransitions>();
            this.minimalTransducerStatesDictionary = new Dictionary<int, List<LinkedListNode<int>>>();
            this.usageQueue = new LinkedList<int>();
            this.minimizedStateCacheSize = cacheSize;
            this.outputType = outputType;
            this.storage = storage;
            this.writeBuffer = new byte[4096];
        }

        private class StateWithTransitions
        {
            private static int NextId = 0;

            public int Id { get; private set; }

            // When state is fronzen. Offset in the output file.
            public long Offset { get; set; }

            public bool IsFronzen { get; set; }

            public bool IsFinal { get; set; }

            public List<Transition> Arcs { get; private set; }

            public StateWithTransitions()
            {
                Id = Interlocked.Increment(ref NextId);
                IsFronzen = false;
                Offset = 0;

                IsFinal = false;
                Arcs = new List<Transition>();
            }

            public int GetDedupHash()
            {
                var result = 0;

                for (int i = 0; i < Arcs.Count; ++i)
                {
                    result = HashCode.Combine(result, Arcs[i].ToId,  Arcs[i].Input, Arcs[i].Output);
                }

                return HashCode.Combine(result, IsFinal ? 1 : 0);
            }

            public bool IsEquivalent(StateWithTransitions other)
            {
                if (IsFinal != other.IsFinal)
                    return false;

                if (Arcs.Count != other.Arcs.Count)
                    return false;

                foreach (var arc in Arcs)
                {
                    bool found = false;
                    foreach (var otherArc in other.Arcs)
                    {
                        if (arc.ToId == otherArc.ToId &&
                            arc.Input == otherArc.Input &&
                            arc.Output.Equals(otherArc.Output))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private readonly struct Transition
        {
            public int ToId { get; init; }

            public long ToOffset { get; init; }

            public char Input { get; init; }

            public T Output { get; init; }
        }

        private static StateWithTransitions CopyOf(StateWithTransitions s)
        {
            var t = new StateWithTransitions();
            t.IsFinal = s.IsFinal;
            t.Arcs.AddRange(s.Arcs);
            return t;
        }

        private StateWithTransitions FreezeState(StateWithTransitions s)
        {
            //TODO: Frozen state should only have toStateOffset in Arcs
            //      therefore other type
            var r = CopyOf(s);
            r.IsFronzen = true;
            r.Offset = WriteState(r);
            frozenStates.Add(r.Id, r);
            return r;
        }

        private long WriteState(StateWithTransitions s)
        {
            if (!s.IsFronzen) throw new Exception("What?");

            var startOffset = storage.Length;
            var size = 0;
            var ts = s.Arcs;

            if (ts.Count > 0)
            {
                size += VarInt.GetByteSize(((uint)ts.Count << 1) | (s.IsFinal ? 1u : 0u));
                var prev = 0;
                for (int j = 0; j < ts.Count; ++j)
                {
                    var next = (int)ts[j].Input;
                    size += VarInt.GetByteSize((uint)(next - prev));
                    size += outputType.GetByteSize(ts[j].Output);
                    // NOTE: All "To" states should have been written earlier. Therefore
                    //       their offset should be smaller than start offset.
                    //       Most of these offsets should be close to current offset.
                    var toStateOffset = ts[j].ToOffset;
                    if (startOffset - toStateOffset < toStateOffset)
                    {
                        size += VarInt.GetByteSize((ulong)(((startOffset - toStateOffset) << 1) | 0));
                    }
                    else
                    {
                        size += VarInt.GetByteSize((ulong)(((toStateOffset) << 1) | 1));
                    }
                    prev = next;
                }
            }
            else
            {
                size += VarInt.GetByteSize(s.IsFinal ? 1u : 0u);
            }

            var toWrite = size;
            var writeIndex = 0;

            if (outputType.MaxByteSize() > 64)
            {
                toWrite += VarInt.GetByteSize((ulong)size);
            }

            if (toWrite > writeBuffer.Length)
            {
                writeBuffer = new byte[((4095 + toWrite) / 4096) * 4096];
            }

            if (outputType.MaxByteSize() > 64)
            {
                writeIndex += VarInt.WriteVInt32(size, writeBuffer, writeIndex);
            }

            if (ts.Count > 0)
            {
                writeIndex += VarInt.WriteVInt32((ts.Count << 1) | (s.IsFinal ? 1 : 0), writeBuffer, writeIndex);
                var prev = 0;
                for (int j = 0; j < ts.Count; ++j)
                {
                    var toStateOffset = ts[j].ToOffset;
                    if (toStateOffset <= 0) throw new Exception("What?");

                    var next = (int)ts[j].Input;
                    writeIndex += VarInt.WriteVInt32((next - prev), writeBuffer, writeIndex);
                    writeIndex += outputType.WriteTo(ts[j].Output, writeBuffer, writeIndex);
                    if (startOffset - toStateOffset < toStateOffset)
                    {
                        writeIndex += VarInt.WriteVInt64(((startOffset - toStateOffset) << 1) | 0, writeBuffer, writeIndex);
                    }
                    else
                    {
                        writeIndex += VarInt.WriteVInt64(((toStateOffset) << 1) | 1, writeBuffer, writeIndex);
                    }
                    prev = next;
                }
            }
            else
            {
                writeIndex += VarInt.WriteVInt32(s.IsFinal ? 1 : 0, writeBuffer, writeIndex);
            }

            if (writeIndex != toWrite)
            {
                throw new Exception($"What is going on? {writeIndex} != {toWrite}");
            }

            storage.WriteAll(startOffset, writeBuffer, 0, toWrite);
            stat.States++;
            return startOffset;
        }

        private StateWithTransitions FindMinimized(StateWithTransitions s)
        {
            bool minimize = true;
            if (!minimize)
            {
                var r = FreezeState(s);
                return r;
            }
            else
            {
                var dedupHash = s.GetDedupHash();

                // Try get cached state
                {
                    if (!minimalTransducerStatesDictionary.TryGetValue(dedupHash, out var statesWithSameHash))
                    {
                        statesWithSameHash = new List<LinkedListNode<int>>();
                        minimalTransducerStatesDictionary.Add(dedupHash, statesWithSameHash);
                    }

                    for (int i = 0; i < statesWithSameHash.Count; ++i)
                    {
                        var frozenState = frozenStates[statesWithSameHash[i].Value];
                        if (frozenState.IsEquivalent(s))
                        {
                            usageQueue.Remove(statesWithSameHash[i]);
                            usageQueue.AddFirst(statesWithSameHash[i]);
                            return frozenState;
                        }
                    }
                }

                // Clean cache
                if (usageQueue.Count >= minimizedStateCacheSize)
                {
                    while (usageQueue.Count >= 1 + (0.75 * minimizedStateCacheSize)) // Clear 20% of cache
                    {
                        var last = usageQueue.Last;
                        var frozenState = frozenStates[last.Value];
                        var lastHash = frozenState.GetDedupHash();

                        if (!minimalTransducerStatesDictionary.TryGetValue(lastHash, out var listToRemove))
                        {
                            throw new Exception();
                        }

                        if (!listToRemove.Remove(last))
                        {
                            throw new Exception();
                        }

                        if (listToRemove.Count == 0)
                        {
                            minimalTransducerStatesDictionary.Remove(lastHash);
                        }

                        frozenStates.Remove(last.Value);
                        usageQueue.RemoveLast();
                    }
                }

                var r = FreezeState(s);

                // Create new frozen state and cache it
                {
                    if (!minimalTransducerStatesDictionary.TryGetValue(dedupHash, out var listToAdd))
                    {
                        listToAdd = new List<LinkedListNode<int>>();
                        minimalTransducerStatesDictionary.Add(dedupHash, listToAdd);
                    }
                    listToAdd.Add(usageQueue.AddFirst(r.Id));
                }

                return r;
            }
        }

        private void SetTransition(StateWithTransitions from, char c, StateWithTransitions to)
        {
            if (from.IsFronzen) throw new Exception("What?");

            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                if (from.Arcs[i].Input == c)
                {
                    from.Arcs[i] = new Transition
                    {
                        Output = from.Arcs[i].Output,
                        Input = c,
                        ToId = to.Id,
                        ToOffset = to.Offset
                    };
                    return;
                }
            }

            from.Arcs.Add(new Transition
            {
                Output = outputType.Zero(),
                Input = c,
                ToId = to.Id,
                ToOffset = to.Offset
            });
        }

        private static T GetOutput(StateWithTransitions from, char c)
        {
            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                if (from.Arcs[i].Input == c)
                {
                    return from.Arcs[i].Output;
                }
            }
            throw new Exception("Nothing");
        }

        private static void SetOutput(StateWithTransitions from, char c, T output)
        {
            if (from.IsFronzen) throw new Exception("What?");

            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                if (from.Arcs[i].Input == c)
                {
                    from.Arcs[i] = new Transition
                    {
                        Output = output,
                        Input = from.Arcs[i].Input,
                        ToId = from.Arcs[i].ToId,
                        ToOffset = from.Arcs[i].ToOffset
                    };
                    return;
                }
            }
            throw new Exception("Nothing");
        }

        private static void PropagateOutput(StateWithTransitions from, T output, IFSTOutput<T> outputType)
        {
            if (from.IsFronzen) throw new Exception("What?");

            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                from.Arcs[i] = new Transition
                {
                    Output = outputType.Sum(from.Arcs[i].Output, output),
                    Input = from.Arcs[i].Input,
                    ToId = from.Arcs[i].ToId,
                    ToOffset = from.Arcs[i].ToOffset
                };
            }
        }

        private static void ClearState(StateWithTransitions s)
        {
            if (s.IsFronzen) throw new Exception("What?");

            s.IsFinal = false;
            s.Arcs.Clear();
        }

        private static void SetFinal(StateWithTransitions s)
        {
            if (s.IsFronzen) throw new Exception("What?");

            s.IsFinal = true;
        }

        public FST<T> FromList(string[] inputs, T[] outputs)
        {
            Begin();
            for (int j = 0; j < inputs.Length; ++j)
            {
                Add(inputs[j], outputs[j]);
            }
            End();

            var data = new byte[storage.Length];
            storage.ReadAll(0, data, 0, data.Length);
            return FST<T>.FromBytesCompressed(data, outputType);
        }

        public void Begin()
        {
            stat = new FSTBuilderStat();
            maxWordSize = InitialWordSize;
            tempState = new StateWithTransitions[maxWordSize];
            for (int i = 0; i < maxWordSize; ++i)
            {
                tempState[i] = new StateWithTransitions();
            }
            previousWord = string.Empty;
            UpdateHeader(long.MinValue, stat);
        }

        public void Add(string currentWord, T currentOutput)
        {
            if (currentWord.Length + 1 > tempState.Length)
            {
                var newTemp = new StateWithTransitions[currentWord.Length + 1];
                for (int i = 0; i < maxWordSize; ++i)
                {
                    newTemp[i] = tempState[i];
                }
                for (int i = maxWordSize; i < newTemp.Length; ++i)
                {
                    newTemp[i] = new StateWithTransitions();
                }
                maxWordSize = currentWord.Length;
                tempState = newTemp;
            }

            if (StringComparer.Ordinal.Compare(currentWord, previousWord) <= 0)
            {
                throw new Exception($"Input should be ordered and each item should be unique: {previousWord} < {currentWord}");
            }

            var prefixLengthPlusOne = 1 + Utils.LCP(previousWord, currentWord);

            if (prefixLengthPlusOne == 1 + currentWord.Length)
            {
                throw new Exception($"Duplicate input {currentWord}");
            }

            // Minimize states from the suffix of the previous word
            for (int i = previousWord.Length; i >= prefixLengthPlusOne; --i)
            {
                SetTransition(tempState[i - 1],
                              previousWord[i - 1],
                              FindMinimized(tempState[i]));
            }

            // Initialize tail states for the current word
            for (int i = prefixLengthPlusOne; i < currentWord.Length + 1; ++i)
            {
                ClearState(tempState[i]);
                SetTransition(tempState[i - 1],
                              currentWord[i - 1],
                              tempState[i]);
            }

            SetFinal(tempState[currentWord.Length]);

            // Set outputs
            for (int i = 1; i < prefixLengthPlusOne; ++i)
            {
                var output = GetOutput(tempState[i - 1], currentWord[i - 1]);
                if (tempState[i].IsFinal)
                {
                    currentOutput = outputType.Sub(currentOutput, output);
                }
                else
                {
                    var commonOutput = outputType.Min(output, currentOutput);
                    if (!commonOutput.Equals(output))
                    {
                        var suffixOutput = outputType.Sub(output, commonOutput);
                        SetOutput(tempState[i - 1], currentWord[i - 1], commonOutput);
                        if (!suffixOutput.Equals(outputType.Zero()))
                        {
                            if (tempState[i].IsFinal || tempState[i].Arcs.Count == 0)
                            {
                                throw new Exception($"Unexpected final state");
                            }
                            else
                            {
                                PropagateOutput(tempState[i], suffixOutput, outputType);
                            }
                        }
                    }
                    currentOutput = outputType.Sub(currentOutput, commonOutput);
                }
            }

            SetOutput(tempState[prefixLengthPlusOne - 1], currentWord[prefixLengthPlusOne - 1], currentOutput);

            previousWord = currentWord;

            stat.MaxLength = Math.Max(stat.MaxLength, currentWord.Length);
            stat.TermCount++;
        }

        public void End()
        {
            for (int i = previousWord.Length; i > 0; --i)
            {
                SetTransition(tempState[i - 1],
                              previousWord[i - 1],
                              FindMinimized(tempState[i]));
            }

            var initial = FindMinimized(tempState[0]);
            UpdateHeader(initial.Offset, stat);
        }

        private void UpdateHeader(long initialOffset, FSTBuilderStat stat)
        {
            Span<byte> data = stackalloc byte[64];

            "FST-02S"u8.CopyTo(data); // Compressed Stream

            int offset = 7;

            BinaryPrimitives.WriteInt64LittleEndian(data[offset..], initialOffset);
            offset += sizeof(long);

            BinaryPrimitives.WriteInt64LittleEndian(data[offset..], stat.States);
            offset += sizeof(long);

            BinaryPrimitives.WriteInt64LittleEndian(data[offset..], stat.TermCount);
            offset += sizeof(long);

            BinaryPrimitives.WriteInt32LittleEndian(data[offset..], stat.MaxLength);
            offset += sizeof(int);

            storage.WriteAll(0, data);
        }

        public void Dispose()
        {
            if (storage != null)
            {
                storage.Dispose();
                storage = null;
            }
        }
    }

    public interface IFST<T>
    {
        int Initial { get; set; }

        bool IsFinal(int stateId);

        bool TryMove(int fromId, char c, out int toId, out T o);

        State AddState();

        void AddTransition(int fromId, char c, int toId, T output);

        void SetFinal(int stateId, bool isFinal);
    }

    public sealed class FSTBuilderStat
    {
        public long TermCount { get; set; }

        public int MaxLength { get; set; }

        public long States { get; set; }
    }

    public class PersistentFST<T> : IDisposable
    {
        #region Fields
        private const int readAheadSize = 128;

        private const int readCacheSize = 32000;

        private const int inMemorySize = 1024 * 1024;

        private const int blockSize = 4096;

        private static readonly int MaxSizeV32 = VarInt.GetByteSize(uint.MaxValue);

        private static readonly int MaxSizeV64 = VarInt.GetByteSize(ulong.MaxValue);

        private readonly IFSTOutput<T> outputType;

        private IPersistentStorage storage;

        private long storageLength;

        private readonly long initial;

        private byte[] stateData;

        private long readOffset;

        private int readSize;

        private readonly Dictionary<long, LinkedListNode<(bool, ArcOffset<T>[], long)>> cache = new();

        private readonly LinkedList<(bool, ArcOffset<T>[], long)> cacheOrder = new();
        #endregion

        #region Properties
        public IFSTOutput<T> OutputType => outputType;

        public FSTBuilderStat Header { get; set; }
        #endregion

        #region Methods
        public PersistentFST(IFSTOutput<T> outputType, IPersistentStorage storage)
        {
            ArgumentNullException.ThrowIfNull(outputType);
            ArgumentNullException.ThrowIfNull(storage);

            if (storage.Length == 0)
            {
                throw new InvalidOperationException("Storage is empty");
            }

            this.outputType = outputType;
            this.storage = storage;
            this.stateData = new byte[readAheadSize];
            this.initial = ReadHeader(storage);
            this.storageLength = storage.Length;
            if (storageLength < inMemorySize)
            {
                Ensure(0, (int)storageLength);
            }
        }

        private long ReadHeader(IPersistentStorage storage)
        {
            Span<byte> data = stackalloc byte[7 + sizeof(long)];
            storage.ReadAll(0, data);

            if ((data[0] != (byte)'F') ||
                (data[1] != (byte)'S') ||
                (data[2] != (byte)'T') ||
                (data[3] != (byte)'-') ||
                (data[4] != (byte)'0') ||
                ((data[5] != (byte)'1') && (data[5] != (byte)'2')) ||
                (data[6] != (byte)'S'))
            {
                throw new Exception("Wrong header");
            }

            if (data[5] == (byte)'2')
            {
                data = stackalloc byte[64];
                storage.ReadAll(0, data);

                Header = new FSTBuilderStat
                {
                    States = BinaryPrimitives.ReadInt64LittleEndian(data[15..]),
                    TermCount = BinaryPrimitives.ReadInt64LittleEndian(data[23..]),
                    MaxLength = BinaryPrimitives.ReadInt32LittleEndian(data[31..]),
                };
            }

            return BinaryPrimitives.ReadInt64LittleEndian(data[7..]);
        }

        private int Ensure(long offset, int size)
        {
            if (offset > storageLength)
            {
                throw new Exception();
            }

            if (offset + size > storageLength)
            {
                size = (int)(storageLength - offset);
            }

            if (offset >= readOffset && offset <= (readOffset + readSize) && (offset + size) <= (readOffset + readSize))
            {
                // Have this data in buffer
                return (int)(offset - readOffset);
            }

            var startBlock = offset / blockSize;
            var endBlock = (offset + size + (blockSize - 1)) / blockSize;
            var sizeAdjusted = (int)(blockSize * (endBlock - startBlock));

            if (sizeAdjusted > stateData.Length)
            {
                stateData = new byte[sizeAdjusted];
            }

            readOffset = blockSize * startBlock;
            readSize = storage.Read(readOffset, stateData, 0, sizeAdjusted);
            if (readSize == 0)
            {
                throw new Exception("What?");
            }

            return (int)(offset - readOffset);
        }

        private (bool isFinal, ArcOffset<T>[] arcs) ReadState(long offset)
        {
            if (cache.TryGetValue(offset, out var cached))
            {
                if (cacheOrder.First != cached)
                {
                    cacheOrder.Remove(cached);
                    cacheOrder.AddFirst(cached);
                }
                return (cached.Value.Item1, cached.Value.Item2);
            }

            var readIndex = Ensure(offset, readAheadSize);
            var hasSize = false;

            if (outputType.MaxByteSize() > 64)
            {
                var temp = VarInt.ReadVInt32(stateData, readIndex, out int size);
                if (size == 0)
                {
                    throw new Exception("Incorrect size. Maybe in-memory format");
                }
                readIndex = Ensure(offset + temp, size);
                hasSize = true;
            }
            else
            {
                readIndex = Ensure(offset, MaxSizeV32);
            }

            var delta = VarInt.ReadVInt32(stateData, readIndex, out var v);
            bool isFinal = ((v & 1) == 1);

            int tsCount = (int)(v >> 1);
            if (tsCount == 0)
            {
                return (isFinal, null);
            }

            var arcs = new ArcOffset<T>[tsCount];
            if (tsCount > 0)
            {
                int prev = 0;
                if (hasSize)
                {
                    //NOTE: Delta will be the index
                    delta += readIndex;
                }
                else
                {
                    //NOTE: Guess how many bytes to read ahead for a better performance. Nothing more
                    readIndex = Ensure(offset + delta, 2 * tsCount * MaxSizeV32);
                }

                for (int i = 0; i < tsCount; ++i)
                {
                    readIndex = hasSize ? delta : Ensure(offset + delta, MaxSizeV32);
                    delta += VarInt.ReadVInt32(stateData, readIndex, out var input);

                    readIndex = hasSize ? delta : Ensure(offset + delta, outputType.MaxByteSize());
                    delta += outputType.ReadFrom(stateData, readIndex, out var output);

                    readIndex = hasSize ? delta : Ensure(offset + delta, MaxSizeV64);
                    delta += VarInt.ReadVInt64(stateData, readIndex, out var toOffset);

                    prev = (int)(input + prev);

                    if ((toOffset & 1) == 1)
                    {
                        toOffset >>= 1;
                    }
                    else
                    {
                        toOffset = offset - (toOffset >> 1);
                    }

                    if (toOffset >= offset || offset < 0)
                    {
                        throw new Exception("We look only backwards by construction");
                    }

                    var arc = new ArcOffset<T>
                    {
                        Input = (char)(prev),
                        ToOffset = toOffset,
                        Output = output
                    };
                    arcs[i] = arc;
                }
            }

            while (cache.Count >= readCacheSize)
            {
                cache.Remove(cacheOrder.Last.Value.Item3);
                cacheOrder.RemoveLast();
            }
            cache.Add(offset, cacheOrder.AddFirst((isFinal, arcs, offset)));

            return (isFinal, arcs);
        }

        public void ToDotNotation(IPersistentStorage outputStorage)
        {
            var result = new StringBuilder();
            result.AppendLine("digraph DFA {");
            result.AppendLine("rankdir = LR;");
            result.AppendLine("orientation = Portrait;");

            var seen = new HashSet<long>();
            var stack = new Stack<long>();
            stack.Push(initial);
            seen.Add(initial);

            while (stack.Count > 0)
            {
                var stateOffset = stack.Pop();
                var (isFinal, ts) = ReadState(stateOffset);

                if (stateOffset == initial)
                {
                    result.Append(CultureInfo.InvariantCulture, $"{stateOffset}[label = \"{stateOffset}\", shape = circle, style = bold, fontsize = 14]");
                    result.AppendLine();
                }
                else if (isFinal)
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = doublecircle, style = bold, fontsize = 14]", stateOffset);
                    result.AppendLine();
                }
                else
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = circle, style = solid, fontsize = 14]", stateOffset);
                    result.AppendLine();
                }

                if (ts != null)
                {
                    // Enumerate transitions in the reverse order because actions in
                    // stack will reverse them again.
                    for (int i = ts.Length - 1; i >= 0; --i)
                    {
                        var t = ts[i];
                        if (!seen.Contains(t.ToOffset))
                        {
                            stack.Push(t.ToOffset);
                            seen.Add(t.ToOffset);
                        }
                    }

                    for (int i = 0; i < ts.Length; i++)
                    {
                        var t = ts[i];
                        result.AppendFormat("{0}->{1} [label = \"{2} | {3}\", fontsize = 14];", stateOffset, t.ToOffset, t.Input, t.Output);
                        result.AppendLine();
                    }
                }

                if (result.Length > 65_536)
                {
                    outputStorage.AppendUtf8Bytes(result.ToString());
                    result.Clear();
                }
            }

            result.AppendLine("}");
            outputStorage.AppendUtf8Bytes(result.ToString());
        }

        #endregion

        #region IFST
        public bool TryMatch(IEnumerable<char> input, out T value)
        {
            var v = outputType.Zero();
            var (isFinal, arcs) = ReadState(initial);
            foreach (var c in input)
            {
                if (TryMove(arcs, c, out var toOffset, out var o))
                {
                    (isFinal, arcs) = ReadState(toOffset);
                    v = outputType.Sum(v, o);
                }
                else
                {
                    value = outputType.Zero();
                    return false;
                }
            }

            value = v;
            return isFinal;
        }

        public bool TryMove(ArcOffset<T>[] ts, char c, out long toOffset, out T o)
        {
            if (ts != null)
            {
                if (ts.Length < 8)
                {
                    foreach (ref readonly var t in ts.AsSpan())
                    {
                        if (t.Input == c)
                        {
                            toOffset = t.ToOffset;
                            o = t.Output;
                            return true;
                        }
                    }
                }
                else
                {
                    var a = 0;
                    var b = ts.Length;
                    while (a != b)
                    {
                        var mid = (a + b) >> 1;
                        var s = ts[mid];
                        if (s.Input > c)
                        {
                            b = mid;
                        }
                        else if (s.Input < c)
                        {
                            a = mid + 1; //TODO: Verify
                        }
                        else
                        {
                            toOffset = s.ToOffset;
                            o = s.Output;
                            return true;
                        }
                    }
                }
            }

            toOffset = -1;
            o = default(T);
            return false;
        }

        public IEnumerable<string> Match(IDfaMatcher<char> matcher)
        {
            var stack = new Stack<ValueTuple<int, long, char>>();
            var prefix = new List<char>();
            stack.Push(new ValueTuple<int, long, char>(0, initial, '\0'));

            while (stack.Count > 0)
            {
                var (ac, stateOffset, ch) = stack.Pop();

                if (ac == 0)
                {
                    var (isFinal, ts) = ReadState(stateOffset);

                    if (isFinal && matcher.IsFinal())
                    {
                        yield return CollectionsMarshal.AsSpan(prefix).ToString();
                    }

                    if (ts != null)
                    {
                        // Enumerate transitions in the reverse order because actions in
                        // stack will reverse them again.
                        for (int i = ts.Length - 1; i >= 0; --i)
                        {
                            var t = ts[i];
                            if (matcher.Next(t.Input))
                            {
                                matcher.Pop();

                                // Reverse order of actions
                                // 1 - Add to prefix
                                // 0 - Go to the state
                                // 2 - Remove from prefix
                                stack.Push(new ValueTuple<int, long, char>(2, 0, '\0'));
                                stack.Push(new ValueTuple<int, long, char>(0, t.ToOffset, '\0'));
                                stack.Push(new ValueTuple<int, long, char>(1, 0, t.Input));
                            }
                        }
                    }
                }
                else if (ac == 1)
                {
                    prefix.Add(ch);
                    if (!matcher.Next(ch))
                    {
                        throw new Exception("What?");
                    }
                }
                else if (ac == 2)
                {
                    prefix.RemoveAt(prefix.Count - 1);
                    matcher.Pop();
                }
                else
                {
                    throw new Exception("What?");
                }
            }
        }

        public IEnumerable<string> MatchRecursive(IDfaMatcher<char> matcher)
        {
            var result = new List<string>();
            var prefix = new List<char>();
            MatchRecursive(matcher, initial, result, prefix);
            return result;
        }

        private void MatchRecursive(IDfaMatcher<char> matcher, long stateOffset, List<string> result, List<char> prefix)
        {
            var (isFinal, ts) = ReadState(stateOffset);

            if (isFinal && matcher.IsFinal())
            {
                result.Add(CollectionsMarshal.AsSpan(prefix).ToString());
            }

            if (ts != null)
            {
                foreach (var t in ts)
                {
                    if (matcher.Next(t.Input))
                    {
                        prefix.Add(t.Input);

                        MatchRecursive(matcher, t.ToOffset, result, prefix);

                        prefix.RemoveAt(prefix.Count - 1);
                        matcher.Pop();
                    }
                }
            }
        }

        public void Dispose()
        {
            if (storage != null)
            {
                storage.Dispose();
                storage = null;
            }
        }
        #endregion
    }

    public class FST<T> : IFST<T>
    {
        #region Fields
        private readonly List<State> states;

        private readonly HashSet<int> final;

        private readonly Dictionary<int, List<Arc<T>>> trans;

        private readonly IFSTOutput<T> outputType;
        #endregion

        #region Properties
        public IFSTOutput<T> OutputType => outputType;
        #endregion

        #region Methods
        public FST(IFSTOutput<T> outputType)
        {
            this.states = new List<State>();
            this.final = new HashSet<int>();
            this.trans = new Dictionary<int, List<Arc<T>>>();
            this.outputType = outputType;

            Initial = 0;
        }
        #endregion

        #region Serialization
        public byte[] GetBytes()
        {
            var size = Numeric.GetByteSize(Initial);
            for (int i = 0; i < states.Count; ++i)
            {
                size += Numeric.GetByteSize(states[i].Id | (IsFinal(states[i].Id) ? 0x40000000 : 0)) /* State Id + Flag: IsFinal */;
                if (trans.TryGetValue(states[i].Id, out var ts))
                {
                    size += Numeric.GetByteSize(ts.Count) /* Transition count */;
                    foreach (var t in ts)
                    {
                        size += Numeric.GetByteSize(t.Input) +
                                Numeric.GetByteSize(t.To) +
                                outputType.GetByteSize(t.Output);
                    }
                }
                else
                {
                    size += Numeric.GetByteSize(0) /* Transition count */;
                }
            }

            var result = new byte[size];
            var writeIndex = 0;
            writeIndex += Numeric.WriteInt(Initial, result, writeIndex);
            for (int i = 0; i < states.Count; ++i)
            {
                // To read id (v & 0x3FFFFFFF)
                // To check for final (v & 0x40000000) == 0x40000000
                writeIndex += Numeric.WriteInt(states[i].Id | (IsFinal(states[i].Id) ? 0x40000000 : 0), result, writeIndex);
                if (trans.TryGetValue(states[i].Id, out var ts))
                {
                    writeIndex += Numeric.WriteInt(ts.Count, result, writeIndex);
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        writeIndex += Numeric.WriteInt(ts[j].Input, result, writeIndex);
                        writeIndex += outputType.WriteTo(ts[j].Output, result, writeIndex);
                        writeIndex += Numeric.WriteInt(ts[j].To, result, writeIndex);
                    }
                }
                else
                {
                    writeIndex += Numeric.WriteInt(0, result, writeIndex);
                }
            }
            if (writeIndex != result.Length)
            {
                throw new Exception($"What is going on? {writeIndex} != {result.Length}");
            }
            return result;
        }

        public static FST<T> FromBytes(byte[] data, IFSTOutput<T> outputType)
        {
            var fst = new FST<T>(outputType);
            var readIndex = 0;
            readIndex += Numeric.ReadInt(data, readIndex, out var v);
            fst.Initial = v;
            while (readIndex != data.Length)
            {
                readIndex += Numeric.ReadInt(data, readIndex, out v);
                var sId = v & 0x3FFFFFFF;
                var s = fst.AddState();
                if (s.Id != sId)
                {
                    throw new Exception("Read error");
                }

                if ((v & 0x40000000) == 0x40000000)
                {
                    fst.SetFinal(sId, true);
                }

                readIndex += Numeric.ReadInt(data, readIndex, out var tsCount);
                for (int i = 0; i < tsCount; ++i)
                {
                    readIndex += Numeric.ReadInt(data, readIndex, out var input);
                    readIndex += outputType.ReadFrom(data, readIndex, out var output);
                    readIndex += Numeric.ReadInt(data, readIndex, out var toId);

                    fst.AddTransition(sId, (char)input, toId, output);
                }
            }
            return fst;
        }

        public byte[] GetBytesCompressed()
        {
            var names = new Dictionary<int, long>();
            var sizes = new Dictionary<int, int>();

            var size = 7 + sizeof(long);
            for (int i = 0; i < states.Count; ++i)
            {
                var startOffset = size;
                names.Add(states[i].Id, startOffset);

                var nodeSize = 0;
                if (trans.TryGetValue(states[i].Id, out var ts) && (ts.Count > 0))
                {
                    nodeSize += VarInt.GetByteSize(((uint)ts.Count << 1) | (IsFinal(states[i].Id) ? 1u : 0u));
                    var prev = 0;
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        var next = (int)ts[j].Input;
                        nodeSize += VarInt.GetByteSize((uint)(next - prev));
                        nodeSize += outputType.GetByteSize(ts[j].Output);
                        var toOffset = names[ts[j].To];
                        if (startOffset - toOffset < toOffset)
                        {
                            nodeSize += VarInt.GetByteSize((ulong)(((startOffset - toOffset) << 1) | 0));
                        }
                        else
                        {
                            nodeSize += VarInt.GetByteSize((ulong)(((toOffset) << 1) | 1));
                        }
                        prev = next;
                    }
                }
                else
                {
                    nodeSize += VarInt.GetByteSize(IsFinal(states[i].Id) ? 1u : 0u);
                }

                if (outputType.MaxByteSize() > 64)
                {
                    sizes.Add(states[i].Id, nodeSize);
                    size += VarInt.GetByteSize((ulong)nodeSize);
                }

                size += nodeSize;
            }

            var result = new byte[size];
            result[0] = (byte)'F';
            result[1] = (byte)'S';
            result[2] = (byte)'T';
            result[3] = (byte)'-';
            result[4] = (byte)'0';
            result[5] = (byte)'1';
            result[6] = (byte)'S';
            Array.Copy(BitConverter.GetBytes(names[Initial]), 0, result, 7, sizeof(long));
            var writeIndex = 7 + sizeof(long);

            for (int i = 0; i < states.Count; ++i)
            {
                var startOffset = names[states[i].Id];
                if (outputType.MaxByteSize() > 64)
                {
                    var nodeSize = sizes[states[i].Id];
                    writeIndex += VarInt.WriteVInt32(nodeSize, result, writeIndex);
                }
                if (trans.TryGetValue(states[i].Id, out var ts) && (ts.Count > 0))
                {
                    writeIndex += VarInt.WriteVInt32((ts.Count << 1) | (IsFinal(states[i].Id) ? 1 : 0), result, writeIndex);
                    var prev = 0;
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        var next = (int)ts[j].Input;
                        writeIndex += VarInt.WriteVInt32((next - prev), result, writeIndex);
                        writeIndex += outputType.WriteTo(ts[j].Output, result, writeIndex);
                        var toOffset = names[ts[j].To];
                        if (startOffset - toOffset < toOffset)
                        {
                            writeIndex += VarInt.WriteVInt64(((startOffset - toOffset) << 1) | 0, result, writeIndex);
                        }
                        else
                        {
                            writeIndex += VarInt.WriteVInt64(((toOffset) << 1) | 1, result, writeIndex);
                        }
                        prev = next;
                    }
                }
                else
                {
                    writeIndex += VarInt.WriteVInt32(IsFinal(states[i].Id) ? 1 : 0, result, writeIndex);
                }
            }
            if (writeIndex != result.Length)
            {
                throw new Exception("What is going on?");
            }
            return result;
        }

        public static FST<T> FromBytesCompressed(byte[] data, IFSTOutput<T> outputType)
        {
            var names = new Dictionary<long, int>(); // Maps offset to state id;
            var fst = new FST<T>(outputType);
            var readIndex = 0;

            if ((data[0] != (byte)'F') ||
                (data[1] != (byte)'S') ||
                (data[2] != (byte)'T') ||
                (data[3] != (byte)'-') ||
                (data[4] != (byte)'0') ||
                ((data[5] != (byte)'1') && (data[5] != (byte)'2')) ||
                (data[6] != (byte)'S'))
            {
                throw new Exception("Wrong header");
            }
            readIndex += 7;

            var initialOffset = BitConverter.ToInt64(data, readIndex);
            readIndex += sizeof(long);

            if (data[5] == (byte)'2')
            {
                readIndex = 64;
            }

            // First cycle - create states
            var temp = readIndex;
            var stateList = new List<int>();

            while (readIndex != data.Length)
            {
                var sOffset = readIndex;
                var s = fst.AddState();
                names.Add(sOffset, s.Id);
                stateList.Add(s.Id);

                if (outputType.MaxByteSize() > 64)
                {
                    readIndex += VarInt.ReadVInt32(data, readIndex, out var nodeSize);
                }

                readIndex += VarInt.ReadVInt32(data, readIndex, out var v);
                if ((v & 1) == 1)
                {
                    fst.SetFinal(s.Id, true);
                }

                int tsCount = (int)(v >> 1);
                if (tsCount > 0)
                {
                    for (int i = 0; i < tsCount; ++i)
                    {
                        readIndex += VarInt.ReadVInt32(data, readIndex, out var input);
                        readIndex += outputType.ReadFrom(data, readIndex, out var output);
                        readIndex += VarInt.ReadVInt64(data, readIndex, out var toOffset);

                        if ((toOffset & 1) == 1)
                        {
                            toOffset >>= 1;
                        }
                        else
                        {
                            toOffset = sOffset - (toOffset >> 1);
                        }
                    }
                }
            }

            // First cycle - create transitions
            readIndex = temp;
            var stateIndex = 0;
            while (readIndex != data.Length)
            {
                var sOffset = readIndex;

                if (outputType.MaxByteSize() > 64)
                {
                    readIndex += VarInt.ReadVInt32(data, readIndex, out var nodeSize);
                }

                readIndex += VarInt.ReadVInt32(data, readIndex, out var v);

                int tsCount = (int)(v >> 1);
                if (tsCount > 0)
                {
                    int prev = 0;
                    for (int i = 0; i < tsCount; ++i)
                    {
                        readIndex += VarInt.ReadVInt32(data, readIndex, out var input);
                        readIndex += outputType.ReadFrom(data, readIndex, out var output);
                        readIndex += VarInt.ReadVInt64(data, readIndex, out var toOffset);

                        if ((toOffset & 1) == 1)
                        {
                            toOffset >>= 1;
                        }
                        else
                        {
                            toOffset = sOffset - (toOffset >> 1);
                        }

                        fst.AddTransition(stateList[stateIndex], (char)(input + prev), names[toOffset], output);
                        prev = (int)(input + prev);
                    }
                }

                ++stateIndex;
            }

            fst.Initial = names[initialOffset];

            return fst;
        }
        #endregion

        #region IFST
        public int Initial { get; set; }

        public State AddState()
        {
            var s = new State { Id = states.Count };
            states.Add(s);
            trans.Add(s.Id, new List<Arc<T>>());
            return s;
        }

        public void AddTransition(int fromId, char c, int toId, T output)
        {
            if (!trans.TryGetValue(fromId, out var t))
            {
                throw new Exception("Bad state");
            }

            if (t.Count > 0 && t[t.Count - 1].Input >= c)
            {
                throw new Exception("Bad transition: should be ordered and unique");
            }

            t.Add(new Arc<T>
            {
                From = fromId,
                Input = c,
                To = toId,
                Output = output
            });
        }

        public void SetFinal(int stateId, bool isFinal)
        {
            if (isFinal)
            {
                final.Add(stateId);
            }
            else
            {
                final.Remove(stateId);
            }
        }

        public bool IsFinal(int stateId)
        {
            return final.Contains(stateId);
        }

        public bool TryMove(int fromId, char c, out int toId, out T o)
        {
            if (trans.TryGetValue(fromId, out var ts))
            {
                if (ts.Count < 8)
                {
                    foreach (var t in ts)
                    {
                        if (t.Input == c)
                        {
                            toId = t.To;
                            o = t.Output;
                            return true;
                        }
                    }
                }
                else
                {
                    var a = 0;
                    var b = ts.Count;
                    while (a != b)
                    {
                        var mid = (a + b) >> 1;
                        var s = ts[mid];
                        if (s.Input > c)
                        {
                            b = mid;
                        }
                        else if (s.Input < c)
                        {
                            a = mid + 1; //TODO: Verify
                        }
                        else
                        {
                            toId = s.To;
                            o = s.Output;
                            return true;
                        }
                    }
                }
            }

            toId = -1;
            o = default(T);
            return false;
        }

        public IEnumerable<string> Match(IDfaMatcher<char> matcher)
        {
            var result = new List<string>();
            var prefix = new List<char>();
            MatchRecursive(matcher, Initial, result, prefix);
            return result;
        }

        private void MatchRecursive(IDfaMatcher<char> matcher, int s, List<string> result, List<char> prefix)
        {
            if (IsFinal(s) && matcher.IsFinal())
            {
                result.Add(CollectionsMarshal.AsSpan(prefix).ToString());
            }

            if (trans.TryGetValue(s, out var ts))
            {
                foreach (var t in ts)
                {
                    if (matcher.Next(t.Input))
                    {
                        prefix.Add(t.Input);

                        MatchRecursive(matcher, t.To, result, prefix);

                        prefix.RemoveAt(prefix.Count - 1);
                        matcher.Pop();
                    }
                }
            }
        }
        #endregion
    }

    public readonly struct State : IEquatable<State>
    {
        public static readonly State NoState = new State { Id = -1 };

        public int Id { get; init; }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(State other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return Equals((State)obj);
        }
    }

    public readonly struct Arc<T> : IEquatable<Arc<T>>
    {
        public int From { get; init; }

        public int To { get; init; }

        public char Input { get; init; }

        public T Output { get; init; }

        public override int GetHashCode()
        {
            return HashCode.Combine(From, To, Input, Output);
        }

        public bool Equals(Arc<T> other)
        {
            return From.Equals(other.From) &&
                   To.Equals(other.To) &&
                   Input.Equals(other.Input) &&
                   Output.Equals(other.Output);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return Equals((Arc<T>)obj);
        }
    }

    public readonly struct ArcOffset<T> : IEquatable<ArcOffset<T>>
    {
        public long ToOffset { get; init; }

        public char Input { get; init; }

        public T Output { get; init; }

        public override int GetHashCode()
        {
            return HashCode.Combine(ToOffset, Input, Output);
        }

        public bool Equals(ArcOffset<T> other)
        {
            return ToOffset.Equals(other.ToOffset) &&
                   Input.Equals(other.Input) &&
                   Output.Equals(other.Output);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return Equals((ArcOffset<T>)obj);
        }
    }

    public interface IFSTOutput<T>
    {
        T Zero();

        T Sum(T a, T b);

        T Sub(T a, T b);

        T Min(T a, T b);

        int GetByteSize(T value);

        int MaxByteSize();

        int ReadFrom(byte[] buffer, int startIndex, out T result);

        int WriteTo(T value, byte[] buffer, int startIndex);
    }

    public abstract class FSTIntOutputBase : IFSTOutput<int>
    {
        protected FSTIntOutputBase() { }

        public int Min(int a, int b) => Math.Min(a, b);

        public int Sub(int a, int b) => a - b;

        public int Sum(int a, int b) => a + b;

        public int Zero() => 0;

        public abstract int MaxByteSize();

        public abstract int GetByteSize(int value);

        public abstract int ReadFrom(byte[] buffer, int startIndex, out int result);

        public abstract int WriteTo(int value, byte[] buffer, int startIndex);
    }

    public class FSTVarIntOutput : FSTIntOutputBase
    {
        public static readonly FSTVarIntOutput Instance = new FSTVarIntOutput();

        private static int maxByteSize = VarInt.GetByteSize(uint.MaxValue);

        public override int GetByteSize(int value)
        {
            return VarInt.GetByteSize((uint)value);
        }

        public override int MaxByteSize()
        {
            return maxByteSize;
        }

        public override int ReadFrom(byte[] buffer, int startIndex, out int result)
        {
            return VarInt.ReadVInt32(buffer, startIndex, out result);
        }

        public override int WriteTo(int value, byte[] buffer, int startIndex)
        {
            return VarInt.WriteVInt32(value, buffer, startIndex);
        }
    }

    public class FSTIntOutput : FSTIntOutputBase
    {
        public static readonly FSTIntOutput Instance = new FSTIntOutput();

        public override int GetByteSize(int value)
        {
            return Numeric.GetByteSize(value);
        }

        public override int MaxByteSize()
        {
            return Numeric.GetByteSize(int.MaxValue);
        }

        public override int ReadFrom(byte[] buffer, int startIndex, out int result)
        {
            return Numeric.ReadInt(buffer, startIndex, out result);
        }

        public override int WriteTo(int value, byte[] buffer, int startIndex)
        {
            return Numeric.WriteInt(value, buffer, startIndex);
        }
    }

    public class FSTStringOutput : IFSTOutput<string>
    {
        public static readonly FSTStringOutput Instance = new FSTStringOutput();

        private FSTStringOutput() { }

        public string Min(string a, string b)
        {
            return a.Substring(0, Utils.LCP(a, b));
        }

        public string Sub(string a, string b)
        {
            if (b == string.Empty) return a;
            if (a.Length == b.Length) return string.Empty;
            if (a.Length < b.Length) throw new ArgumentException();
            return a.Substring(b.Length);
        }

        public string Sum(string a, string b)
        {
            return a + b;
        }

        public string Zero() => string.Empty;

        public int MaxByteSize() => int.MaxValue;

        public int GetByteSize(string value)
        {
            var size = Encoding.UTF8.GetByteCount(value);
            return VarInt.GetByteSize((ulong)size) + size;
        }

        public int ReadFrom(byte[] buffer, int startIndex, out string result)
        {
            var size = VarInt.ReadVInt32(buffer, startIndex, out var byteCount);
            result = Encoding.UTF8.GetString(buffer, startIndex + size, byteCount);
            return size + byteCount;
        }

        public int WriteTo(string value, byte[] buffer, int startIndex)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var size = VarInt.WriteVInt32(bytes.Length, buffer, startIndex);
            Array.Copy(bytes, 0, buffer, startIndex + size, bytes.Length);
            return size + bytes.Length;
        }
    }

    internal static class Utils
    {
        // Calculate length of the longest common prefix
        public static int LCP(string a, string b)
        {
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i])
            {
                ++i;
            }
            return i;
        }
    }
}
