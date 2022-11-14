using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Protsyk.PMS.FullText.Core.Common;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    // Encodes monotonic sequence of integers into compressed binary representation
    // using delta compression and VarInt encoding.
    public class DeltaVarIntListWriter : IDisposable
    {
        #region Fields
         // Should be at least MaxVarInt
        internal static readonly int BlockSize = 1024;
        private readonly byte[] buffer;
        private readonly IPersistentStorage persistentStorage;

        private int bufferIndex;
        private ulong previous;
        private long listStart;
        private long totalSize;
        private int recordCount;
        private bool first;
        #endregion

        #region Constructors
        public DeltaVarIntListWriter(string folder, string fileName)
            : this(new FileStorage(Path.Combine(folder, fileName)))
        {
        }

        public DeltaVarIntListWriter(IPersistentStorage storage)
        {
            this.buffer = new byte[BlockSize + VarInt.GetByteSize(ulong.MaxValue)];
            this.persistentStorage = storage;
        }
        #endregion

        #region API
        public long StartList()
        {
            recordCount = 0;
            bufferIndex = 0;
            totalSize = 0;
            previous = 0UL;
            first = true;

            listStart = persistentStorage.Length;

            // Write List mark
            persistentStorage.WriteAll(listStart, "L"u8);

            // Reserve space for the length of the list
            persistentStorage.WriteInt32LittleEndian(listStart + 1, 0);

            // Reserve space for the record count
            persistentStorage.WriteInt32LittleEndian(listStart + 1 + sizeof(int), 0);

            return listStart;
        }

        public void AddValue(ulong value)
        {
            if (value == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (first)
            {
                bufferIndex += VarInt.WriteVUInt64(value, buffer, bufferIndex);
                previous = value;
                first = false;
            }
            else
            {
                if (value <= previous)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                var previousIndex = bufferIndex;
                bufferIndex += VarInt.WriteVUInt64((ulong)(value - previous), buffer, bufferIndex);

                if (bufferIndex > BlockSize)
                {
                    // Current value spans blocks:
                    // 1) Reset index to previous value
                    // 2) Flush block
                    // 3) Start new block and write full occurrence to a new block
                    bufferIndex = previousIndex;
                    FlushBlock(false);
                    bufferIndex = VarInt.WriteVUInt64(value, buffer, 0);
                }

                previous = value;
            }
            ++recordCount;
        }

        public long EndList()
        {
            if (bufferIndex > 0)
            {
                FlushBlock(true);
            }

            // Write length of the list
            persistentStorage.WriteAll(listStart + 1, BitConverter.GetBytes(totalSize).AsSpan(0, sizeof(int)));

            // Write record count of the list
            persistentStorage.WriteInt32LittleEndian(listStart + 1 + sizeof(int), recordCount);

            var listEnd = persistentStorage.Length;

            if (listEnd - listStart != totalSize + 1 + sizeof(int) + sizeof(int))
            {
                throw new InvalidOperationException();
            }

            return listEnd;
        }

        public TextWriter StartTextPayload()
        {
            return new PayloadText(persistentStorage);
        }

        class PayloadText : TextWriter
        {
            private readonly IPersistentStorage persistentStorage;

            public override Encoding Encoding => Encoding.UTF8;

            public PayloadText(IPersistentStorage persistentStorage)
            {
                this.persistentStorage = persistentStorage;
            }

            public override void Write(char value)
            {
                Write(stackalloc char[1] { value });
            }

            public override void Write(char[] buffer, int index, int count)
            {
                Write(buffer.AsSpan(index, count));
            }

            public override void Write(ReadOnlySpan<char> chars)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(Encoding.GetMaxByteCount(chars.Length));

                try
                {
                    var byteCount = Encoding.GetBytes(chars, rentedBuffer);

                    persistentStorage.WriteInt32LittleEndian(persistentStorage.Length, byteCount);
                    persistentStorage.WriteAll(persistentStorage.Length, rentedBuffer.AsSpan(0, byteCount));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    persistentStorage?.WriteInt32LittleEndian(persistentStorage.Length, 0);
                }
                base.Dispose(disposing);
            }
        }

        public long StartPayload()
        {
            listStart = persistentStorage.Length;
            return listStart;
        }

        public void WritePayload(ReadOnlySpan<byte> buffer)
        {
            persistentStorage.WriteAll(persistentStorage.Length, buffer);
        }

        public long EndPayload()
        {
            var listEnd = persistentStorage.Length;
            return listEnd;
        }

        private void FlushBlock(bool last)
        {
            // Fill un-used space with zeros (waste)
            for (int i=bufferIndex; i<BlockSize; ++i)
            {
                buffer[i] = 0;
            }

            int writeSize = last ? bufferIndex : BlockSize;
            persistentStorage.WriteAll(persistentStorage.Length, buffer.AsSpan(0, writeSize));
            totalSize += writeSize;
        }

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }

        #endregion

        #region Autotest
        public static void Test()
        {
            int N = 1000000;
            int variation = 10000000;
            long listStart = -1;

            var timer = Stopwatch.StartNew();
            var ar = new ulong[N];
            using (var mem = new MemoryStorage())
            {
                using(var writer = new DeltaVarIntListWriter(mem.GetReference()))
                {
                    var s1 = writer.StartList();
                    var prev = 0UL;
                    var r = new Random(2019);
                    for (int i = 0; i<N; ++i)
                    {
                        prev += 1 + (ulong)r.Next(variation);
                        ar[i] = prev;
                        writer.AddValue(prev);
                    }
                    var e1 = writer.EndList();
                    Console.WriteLine($"Count  : {N}");
                    Console.WriteLine($"Length : {e1 - s1}");

                    listStart = s1;
                }
                Console.WriteLine($"Write  : {timer.Elapsed}");

                timer = Stopwatch.StartNew();
                using(var reader = new DeltaVarIntListReader(mem.GetReference()))
                {
                    int i = 0;
                    foreach (var v in reader.Get(listStart))
                    {
                        if (ar[i] != v)
                        {
                            throw new Exception($"{ar[i]} != {v} at {i}");
                        }
                        ++i;
                    }
                }
                Console.WriteLine($"Read   : {timer.Elapsed}");

                timer = Stopwatch.StartNew();
                using(var reader = new DeltaVarIntListReader(mem.GetReference()))
                {
                    for (int i = 0; i<N; ++i)
                    {
                        var v = reader.GetLowerBound(listStart, ar[i]).First();
                        if (ar[i] != v)
                        {
                            throw new Exception($"{ar[i]} != {v} at {i}");
                        }
                    }
                }
                Console.WriteLine($"Seek   : {timer.Elapsed}");
            }
        }
        #endregion
    }
}
