using System;
using System.IO;
using Protsyk.PMS.FullText.Core.Common;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListVarIntDeltaWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "VarIntCompressed";

        internal static readonly int BlockSize = 1024; // Should be at least 3 * MaxVarInt

        private readonly byte[] buffer;
        private readonly IPersistentStorage persistentStorage;
        private int bufferIndex;
        private Occurrence previous;
        private long listStart;
        private long totalSize;

        private bool first;
        #endregion

        public PostingListVarIntDeltaWriter(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, PersistentIndex.FileNamePostingLists)))
        {
        }

        public PostingListVarIntDeltaWriter(IPersistentStorage storage)
        {
            this.buffer = new byte[BlockSize + 4 * VarInt.GetByteSize(ulong.MaxValue)];
            this.persistentStorage = storage;
        }

        #region API
        public void StartList(string token)
        {
            bufferIndex = 0;
            totalSize = 0;
            previous = Occurrence.Empty;
            first = true;

            listStart = persistentStorage.Length;

            // Reserve space for continuation offset
            persistentStorage.WriteAll(listStart, BitConverter.GetBytes(0L), 0, sizeof(long));

            // Reserve space for the length of the list
            persistentStorage.WriteAll(listStart + sizeof(long), BitConverter.GetBytes(0), 0, sizeof(int));
        }

        public void AddOccurrence(Occurrence occurrence)
        {
            if (first)
            {
                bufferIndex = WriteFullOccurrence(occurrence);
                previous = occurrence;
                first = false;
            }
            else
            {
                var previousIndex = bufferIndex;

                if (previous.DocumentId == occurrence.DocumentId)
                {
                    if (previous.FieldId == occurrence.FieldId)
                    {
                        if (previous.TokenId == occurrence.TokenId)
                        {
                            bufferIndex += VarInt.WriteVUInt32(1, buffer, bufferIndex);
                        }
                        else
                        {
                            bufferIndex += VarInt.WriteVUInt32(2, buffer, bufferIndex);
                            bufferIndex += VarInt.WriteVUInt64((ulong)(occurrence.TokenId - previous.TokenId), buffer, bufferIndex);
                        }
                    }
                    else
                    {
                        bufferIndex += VarInt.WriteVUInt32(3, buffer, bufferIndex);
                        bufferIndex += VarInt.WriteVUInt64((ulong)(occurrence.FieldId - previous.FieldId), buffer, bufferIndex);
                        bufferIndex += VarInt.WriteVUInt64(occurrence.TokenId, buffer, bufferIndex);
                    }
                }
                else
                {
                    bufferIndex += VarInt.WriteVUInt32(4, buffer, bufferIndex);
                    bufferIndex += VarInt.WriteVUInt64((ulong)(occurrence.DocumentId - previous.DocumentId), buffer, bufferIndex);
                    bufferIndex += VarInt.WriteVUInt64(occurrence.FieldId, buffer, bufferIndex);
                    bufferIndex += VarInt.WriteVUInt64(occurrence.TokenId, buffer, bufferIndex);
                }

                if (bufferIndex > BlockSize)
                {
                    // Current occurrence spans blocks:
                    // 1) Reset index to previous value
                    // 2) Flush block
                    // 3) Start new block and write full occurrence to a new block
                    bufferIndex = previousIndex;
                    FlushBlock(false);
                    bufferIndex = WriteFullOccurrence(occurrence);
                }

                previous = occurrence;
            }
        }

        public PostingListAddress EndList()
        {
            if (bufferIndex > 0)
            {
                FlushBlock(true);
            }

            // Write length of the list
            persistentStorage.WriteAll(listStart + sizeof(long), BitConverter.GetBytes(totalSize), 0, sizeof(int));

            var listEnd = persistentStorage.Length;

            if (listEnd - listStart != totalSize + sizeof(long) + sizeof(int))
            {
                throw new InvalidOperationException();
            }

            return new PostingListAddress(listStart);
        }

        public void UpdateNextList(PostingListAddress address, PostingListAddress nextList)
        {
            var buffer = new byte[sizeof(long)];
            var offset = address.Offset;
            while (true)
            {
                persistentStorage.ReadAll(offset, buffer, 0, buffer.Length);
                long continuationOffset = BitConverter.ToInt64(buffer, 0);

                if (continuationOffset == 0)
                {
                    persistentStorage.WriteAll(offset, BitConverter.GetBytes(nextList.Offset), 0, sizeof(long));
                    break;
                }
                else
                {
                    offset = continuationOffset;
                }
            }
        }

        private int WriteFullOccurrence(Occurrence occurrence)
        {
            int index = 0;
            index += VarInt.WriteVUInt64(occurrence.DocumentId, buffer, index);
            index += VarInt.WriteVUInt64(occurrence.FieldId, buffer, index);
            index += VarInt.WriteVUInt64(occurrence.TokenId, buffer, index);
            return index;
        }

        private void FlushBlock(bool last)
        {
            // Fill un-used space with zeros (waste)
            for (int i=bufferIndex; i<BlockSize; ++i)
            {
                buffer[i] = 0;
            }

            int writeSize = last ? bufferIndex : BlockSize;
            persistentStorage.WriteAll(persistentStorage.Length, buffer, 0, writeSize);
            totalSize += writeSize;
        }

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }

        #endregion
    }
}
