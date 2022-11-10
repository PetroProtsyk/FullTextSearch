using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core.Collections
{
    public class PersistentHashTable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
    {
        #region Fields
        private const int MinValueBufferSize = 256;
        private const int MinCapacity = 8;
        private static readonly byte[] Header = "PMS-HASH"u8.ToArray();
        private static readonly int HeaderSize = Header.Length;
        private static readonly int IndexRecordSize = sizeof(long) + sizeof(int);

        private readonly IDataSerializer<TKey> keySerializer;
        private readonly IDataSerializer<TValue> valueSerializer;
        private readonly IPersistentStorage dataStorage;
        private readonly byte[] sizeBuffer;
        private readonly IEqualityComparer<TKey> keyComparer;

        private byte[] valueBuffer;
        private int capacity;
        private int count;
        private readonly int headerSize;
        #endregion

        #region Properties
        public int Count => count;
        #endregion

        #region Constructors

        public PersistentHashTable()
            : this(new MemoryStorage(), MinCapacity, EqualityComparer<TKey>.Default) { }

        public PersistentHashTable(IPersistentStorage dataStorage, int capacity, IEqualityComparer<TKey> keyComparer)
        {
            this.dataStorage = dataStorage;
            this.capacity = capacity;
            this.keyComparer = keyComparer;
            this.keySerializer = DataSerializer.GetDefault<TKey>();
            this.valueSerializer = DataSerializer.GetDefault<TValue>();
            this.sizeBuffer = new byte[sizeof(int)];
            this.valueBuffer = new byte[MinValueBufferSize];
            this.headerSize = HeaderSize + 2 * sizeof(int);

            if (this.dataStorage.Length == 0)
            {
                this.count = 0;

                // Header
                this.dataStorage.WriteAll(0, Header.AsSpan(0, HeaderSize));
                this.dataStorage.WriteInt32LittleEndian(HeaderSize, capacity);
                this.dataStorage.WriteInt32LittleEndian(HeaderSize + sizeof(int), count);

                // Index
                var zero = new byte[IndexRecordSize*1024];
                var i = 0;
                while (i < capacity)
                {
                    var toWrite = 1024;
                    if (i + 1024 > capacity)
                    {
                        toWrite = (capacity-i) % 1024;
                    }
                    this.dataStorage.WriteAll(headerSize + i*IndexRecordSize, zero, 0, IndexRecordSize*toWrite);
                    i += toWrite;
                }
            }
            else
            {
                this.dataStorage.ReadAll(0, valueBuffer.AsSpan(0, headerSize));
                for (int i = 0; i < HeaderSize; ++i)
                {
                    if (Header[i] != valueBuffer[i])
                    {
                        throw new Exception("Invalid data file");
                    }
                }

                this.capacity = BinaryPrimitives.ReadInt32LittleEndian(valueBuffer.AsSpan(HeaderSize));
                this.count = BinaryPrimitives.ReadInt32LittleEndian(valueBuffer.AsSpan(HeaderSize + sizeof(int)));
            }
        }

        #endregion

        #region Methods
        private int GetKeyHash(TKey key)
        {
            return keyComparer.GetHashCode(key);
        }

        private bool EqualKeys(TKey a, TKey b)
        {
            return keyComparer.Equals(a, b);
        }

        private (long offset, int size) Append(TKey key, TValue value)
        {
            var keyBytes = keySerializer.GetBytes(key);
            var valueBytes = valueSerializer.GetBytes(value);
            var offsetStart = dataStorage.Length;
            var offset = offsetStart;

            offset += dataStorage.WriteInt32LittleEndian(offset, keyBytes.Length);

            dataStorage.WriteAll(offset, keyBytes);
            offset += keyBytes.Length;

            offset += dataStorage.WriteInt32LittleEndian(offset, valueBytes.Length);

            dataStorage.WriteAll(offset, valueBytes);
            offset += valueBytes.Length;

            // Link
            offset += dataStorage.WriteInt64LittleEndian(offset, 0L);
            offset += dataStorage.WriteInt32LittleEndian(offset, 0);

            checked
            {
                return (offsetStart, (int)(offset-offsetStart));
            }
        }

        private (long indexOffset, long dataOffset, int dataSize) FindKeyRecord(TKey key)
        {
            ArgumentNullException.ThrowIfNull(key);

            var index = (long)((ulong)GetKeyHash(key) % (ulong)capacity);
            var offset = headerSize + index * IndexRecordSize;

            dataStorage.ReadAll(offset, valueBuffer.AsSpan(0, IndexRecordSize));

            var dataOffset = BinaryPrimitives.ReadInt64LittleEndian(valueBuffer);
            var dataSize = BinaryPrimitives.ReadInt32LittleEndian(valueBuffer.AsSpan(8));

            return (offset, dataOffset, dataSize);
        }

        private bool TryRemove(TKey key)
        {
            var keyRecord = FindKeyRecord(key);
            var linkOffset = keyRecord.indexOffset;
            var nextOffset = keyRecord.dataOffset;
            var nextSize = keyRecord.dataSize;

            while (nextOffset != 0)
            {
                var record = ReadDataRecord(nextOffset, nextSize);

                if (EqualKeys(key, record.key))
                {
                    // TODO: Reuse space
                    dataStorage.WriteInt64LittleEndian(linkOffset, record.nextOffset);
                    dataStorage.WriteInt32LittleEndian(linkOffset + sizeof(long), record.nextSize);

                    --count;
                    dataStorage.WriteInt32LittleEndian(HeaderSize + sizeof(int), count);
                    return true;
                }
                else
                {
                    linkOffset = nextOffset + nextSize - IndexRecordSize;
                }

                nextOffset = record.nextOffset;
                nextSize = record.nextSize;
            }
            return false;
        }

        private (TKey key, TValue value, long nextOffset, int nextSize) ReadDataRecord(long dataOffset, int dataSize)
        {
            if (valueBuffer.Length < dataSize)
            {
                valueBuffer = new byte[256 * (1 + (dataSize+255)/256)];
            }

            dataStorage.ReadAll(dataOffset, valueBuffer.AsSpan(0, dataSize));
 
            int keySize = BinaryPrimitives.ReadInt32LittleEndian(valueBuffer);
            var dataKey = keySerializer.GetValue(valueBuffer.Skip(sizeof(int)).Take(keySize).ToArray());

            int valueSize = BinaryPrimitives.ReadInt32LittleEndian(valueBuffer.AsSpan(sizeof(int) + keySize));
            var dataValue = valueSerializer.GetValue(valueBuffer.Skip(sizeof(int)+keySize+sizeof(int)).Take(valueSize).ToArray());

            long nextOffset = BitConverter.ToInt64(valueBuffer, dataSize - sizeof(long) - sizeof(int));
            int nextSize = BitConverter.ToInt32(valueBuffer, dataSize - sizeof(int));
            return (dataKey, dataValue, nextOffset, nextSize);
        }
        #endregion

        #region Api
        public TValue this[TKey key]
        {
            get
            {
                ArgumentNullException.ThrowIfNull(key);

                var keyRecord = FindKeyRecord(key);
                var nextOffset = keyRecord.dataOffset;
                var nextSize = keyRecord.dataSize;

                while (nextOffset != 0)
                {
                    var record = ReadDataRecord(nextOffset, nextSize);
                    if (EqualKeys(key, record.key))
                    {
                        return record.value;
                    }
                    else
                    {
                        nextOffset = record.nextOffset;
                        nextSize = record.nextSize;
                    }
                }

                throw new ArgumentException("Key not found");
            }
            set
            {
                var keyRecord = FindKeyRecord(key);
                var linkOffset = keyRecord.indexOffset;
                var nextOffset = keyRecord.dataOffset;
                var nextSize = keyRecord.dataSize;

                while (nextOffset != 0)
                {
                    var record = ReadDataRecord(nextOffset, nextSize);

                    if (EqualKeys(key, record.key))
                    {
                        // TODO: Reuse space

                        --count;
                        if (record.nextOffset != 0)
                        {
                            dataStorage.WriteInt64LittleEndian(linkOffset, record.nextOffset);
                            dataStorage.WriteInt32LittleEndian(linkOffset + sizeof(long), record.nextSize);
                        }
                    }
                    else
                    {
                        linkOffset = nextOffset + nextSize - IndexRecordSize;
                    }

                    nextOffset = record.nextOffset;
                    nextSize = record.nextSize;
                }

                var newValueLink = Append(key, value);

                // Link
                dataStorage.WriteInt64LittleEndian(linkOffset, newValueLink.offset);
                dataStorage.WriteInt32LittleEndian(linkOffset + sizeof(long), newValueLink.size);

                ++count;
                dataStorage.WriteInt32LittleEndian(HeaderSize + sizeof(int), count);
            }
        }

        public void Remove(TKey key)
        {
            if (!TryRemove(key))
            {
                throw new Exception("Key not found");
            }
        }
        #endregion

        #region IEnumerable
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0L; i < capacity; ++i)
            {
                var offset = headerSize + i * IndexRecordSize;
                dataStorage.ReadAll(offset, valueBuffer, 0, IndexRecordSize);

                long nextOffset = BinaryPrimitives.ReadInt64LittleEndian(valueBuffer);
                int nextSize = BinaryPrimitives.ReadInt32LittleEndian(valueBuffer.AsSpan(8));

                while (nextOffset != 0)
                {
                    var record = ReadDataRecord(nextOffset, nextSize);

                    yield return new KeyValuePair<TKey, TValue>(record.key, record.value);

                    nextOffset = record.nextOffset;
                    nextSize = record.nextSize;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            dataStorage?.Dispose();
        }

        #endregion
    }
}
