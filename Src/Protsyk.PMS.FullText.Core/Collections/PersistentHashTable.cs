using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core.Collections
{
    public class PersistentHashTable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
    {
        #region Fields
        private static readonly int MinValueBufferSize = 256;
        private static readonly int MinCapacity = 8;
        private static readonly byte[] Header = new byte[] { (byte)'P', (byte)'M', (byte)'S', (byte)'-', (byte)'H', (byte)'A', (byte)'S', (byte)'H' };
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
        private int headerSize;
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
                this.dataStorage.WriteAll(0, Header, 0, HeaderSize);
                this.dataStorage.WriteAll(HeaderSize, BitConverter.GetBytes(capacity), 0, sizeof(int));
                this.dataStorage.WriteAll(HeaderSize + sizeof(int), BitConverter.GetBytes(count), 0, sizeof(int));

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
                this.dataStorage.ReadAll(0, valueBuffer, 0, headerSize);
                for (int i=0; i<HeaderSize; ++i)
                {
                    if (Header[i] != valueBuffer[i])
                    {
                        throw new Exception("Invalid data file");
                    }
                }

                this.capacity = BitConverter.ToInt32(valueBuffer, HeaderSize);
                this.count = BitConverter.ToInt32(valueBuffer, HeaderSize + sizeof(int));
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

            dataStorage.WriteAll(offset, BitConverter.GetBytes(keyBytes.Length), 0, sizeof(int));
            offset += sizeof(int);

            dataStorage.WriteAll(offset, keyBytes, 0, keyBytes.Length);
            offset += keyBytes.Length;

            dataStorage.WriteAll(offset, BitConverter.GetBytes(valueBytes.Length), 0, sizeof(int));
            offset += sizeof(int);

            dataStorage.WriteAll(offset, valueBytes, 0, valueBytes.Length);
            offset += valueBytes.Length;

            // Link
            dataStorage.WriteAll(offset, BitConverter.GetBytes(0L), 0, sizeof(long));
            offset += sizeof(long);

            dataStorage.WriteAll(offset, BitConverter.GetBytes(0), 0, sizeof(int));
            offset += sizeof(int);

            checked
            {
                return (offsetStart, (int)(offset-offsetStart));
            }
        }

        private (long indexOffset, long dataOffset, int dataSize) FindKeyRecord(TKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var index = (long)((ulong)GetKeyHash(key) % (ulong)capacity);
            var offset = headerSize + index * IndexRecordSize;

            dataStorage.ReadAll(offset, valueBuffer, 0, IndexRecordSize);
            var dataOffset = BitConverter.ToInt64(valueBuffer, 0);
            var dataSize = BitConverter.ToInt32(valueBuffer, sizeof(long));

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
                    dataStorage.WriteAll(linkOffset, BitConverter.GetBytes(record.nextOffset), 0, sizeof(long));
                    dataStorage.WriteAll(linkOffset+sizeof(long), BitConverter.GetBytes(record.nextSize), 0, sizeof(int));

                    --count;
                    dataStorage.WriteAll(HeaderSize + sizeof(int), BitConverter.GetBytes(count), 0, sizeof(int));
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

            dataStorage.ReadAll(dataOffset, valueBuffer, 0, dataSize);
 
            var keySize = BitConverter.ToInt32(valueBuffer, 0);
            var dataKey = keySerializer.GetValue(valueBuffer.Skip(sizeof(int)).Take(keySize).ToArray());

            var valueSize = BitConverter.ToInt32(valueBuffer, sizeof(int)+keySize);
            var dataValue = valueSerializer.GetValue(valueBuffer.Skip(sizeof(int)+keySize+sizeof(int)).Take(valueSize).ToArray());

            var nextOffset = BitConverter.ToInt64(valueBuffer, dataSize - sizeof(long) - sizeof(int));
            var nextSize = BitConverter.ToInt32(valueBuffer, dataSize - sizeof(int));
            return (dataKey, dataValue, nextOffset, nextSize);
        }
        #endregion

        #region Api
        public TValue this[TKey key]
        {
            get
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

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
                            dataStorage.WriteAll(linkOffset, BitConverter.GetBytes(record.nextOffset), 0, sizeof(long));
                            dataStorage.WriteAll(linkOffset+sizeof(long), BitConverter.GetBytes(record.nextSize), 0, sizeof(int));
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
                dataStorage.WriteAll(linkOffset, BitConverter.GetBytes(newValueLink.offset), 0, sizeof(long));
                dataStorage.WriteAll(linkOffset+sizeof(long), BitConverter.GetBytes(newValueLink.size), 0, sizeof(int));

                ++count;
                dataStorage.WriteAll(HeaderSize + sizeof(int), BitConverter.GetBytes(count), 0, sizeof(int));
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
            for (var i=0L; i<capacity; ++i)
            {
                var offset = headerSize + i * IndexRecordSize;
                dataStorage.ReadAll(offset, valueBuffer, 0, IndexRecordSize);

                var nextOffset = BitConverter.ToInt64(valueBuffer, 0);
                var nextSize = BitConverter.ToInt32(valueBuffer, sizeof(long));

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
