using System;
using System.Buffers.Binary;
using System.IO;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListBinaryWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "Binary";
        private readonly int MemoryBufferSize = 4 * 4096;

        private readonly IPersistentStorage persistentStorage;
        private readonly int[] buffer;
        private readonly byte[] flushBuffer;
        private int bufferIndex;

        private long listStart;

        private int totalSize;
        #endregion

        public PostingListBinaryWriter(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
        {
        }

        public PostingListBinaryWriter(IPersistentStorage storage)
        {
            this.persistentStorage = storage;
            this.buffer = new int[MemoryBufferSize];
            this.flushBuffer = new byte[GroupVarint.GetMaxEncodedSize(MemoryBufferSize)];
            this.bufferIndex = 0;
        }

        #region API
        public void StartList(string token)
        {
            totalSize = 0;
            bufferIndex = 0;
            listStart = persistentStorage.Length;

            // Reserve space for continuation offset
            persistentStorage.WriteInt64LittleEndian(listStart, 0L);

            // Reserve space for the length of the list
            persistentStorage.WriteInt32LittleEndian(listStart + sizeof(long), 0);
        }

        public void AddOccurrence(Occurrence occurrence)
        {
            if (bufferIndex == buffer.Length)
            {
                FlushBuffer();
            }

            if (bufferIndex + 2 < buffer.Length)
            {
                checked
                {
                    buffer[bufferIndex++] = (int)occurrence.DocumentId;
                    buffer[bufferIndex++] = (int)occurrence.FieldId;
                    buffer[bufferIndex++] = (int)occurrence.TokenId;
                }
            }
            else if (bufferIndex + 1 < buffer.Length)
            {
                checked
                {
                    buffer[bufferIndex++] = (int)occurrence.DocumentId;
                    buffer[bufferIndex++] = (int)occurrence.FieldId;
                }

                FlushBuffer();

                checked
                {
                    buffer[bufferIndex++] = (int)occurrence.TokenId;
                }
            }
            else if (bufferIndex < buffer.Length)
            {
                checked
                {
                    buffer[bufferIndex++] = (int)occurrence.DocumentId;
                }

                FlushBuffer();

                checked
                {
                    buffer[bufferIndex++] = (int)occurrence.FieldId;
                    buffer[bufferIndex++] = (int)occurrence.TokenId;
                }
            }
            else
            {
                throw new Exception("Should not happen");
            }
        }
        public void FlushBuffer()
        {
            var encodedSize = GroupVarint.Encode(buffer.AsSpan(0, bufferIndex), flushBuffer);

            // Write data
            persistentStorage.Append(flushBuffer.AsSpan(0, encodedSize));

            totalSize += encodedSize;
            bufferIndex = 0;
        }

        public PostingListAddress EndList()
        {
            FlushBuffer();

            // Write the length of the list
            persistentStorage.WriteInt32LittleEndian(listStart + sizeof(long), totalSize);

            var listEnd = persistentStorage.Length;

            if (listEnd - listStart != totalSize + sizeof(long) + sizeof(int))
            {
                throw new InvalidOperationException();
            }

            return new PostingListAddress(listStart);
        }

        public void UpdateNextList(PostingListAddress address, PostingListAddress nextList)
        {
            long offset = address.Offset;
            while (true)
            {
                long continuationOffset = persistentStorage.ReadInt64LittleEndian(offset);

                if (continuationOffset == 0)
                {
                    persistentStorage.WriteInt64LittleEndian(offset, nextList.Offset);
                    break;
                }
                else
                {
                    offset = continuationOffset;
                }
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
        #endregion
    }
}
