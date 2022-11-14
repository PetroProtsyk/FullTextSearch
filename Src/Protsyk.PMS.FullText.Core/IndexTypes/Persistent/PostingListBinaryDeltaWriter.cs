using System;
using System.Buffers.Binary;
using System.IO;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    // This posting list uses Group VarInt encoding and delta compression.
    // Group VarInt encoding is described here: http://www.ir.uwaterloo.ca/book/addenda-06-index-compression.html.
    //
    // Binary layout is like this:
    //
    // Data  : [HEADER] [ Selector ] [    I1    ] [    I2    ] [    I3    ] [    I4    ]
    // Length:  8 + 4       byte      1..4 bytes   1..4 bytes   1..4 bytes   1..4 bytes
    //
    // Information is encoded by groups of 4 integers. Each group of integers, is preceeded by a byte value called
    // a Selector byte.
    //
    // Selector byte contains information about sizes of the next 4 integers I1, I2, I3 and I4.
    // Each integer value might require 1,2,3 or 4 bytes to store.
    // This is encoded using two bits: 00 - 1 byte, 01 - 2 bytes, 10 - 3 bytes, 11 - 4 bytes.
    // In 8 bits of a selector byte encoder can store exactly 4 sizes and it does it like this:
    // [AABBCCDD] where AA is the size of I1, BB the size of I2, CC the size of I3 and DD the size of I4.
    //
    // Occurrences are encoded as a sequence of integers like this:
    //
    // Int Data : [First Occurrence] [ Delta Selector] [   Delta1   ] ... [   Delta16   ]
    // Length   :      3 * int              int           1..3 ints
    //
    // Delta Selector is used to encoded sizes of the 16 deltas using the same logic as Selector byte described above.
    // The meaning of delta selector bits is as following:
    //
    //  00 - Not used. The last Delta Selector can have unused bits. As such it is not possible to say 
    //                 if occurrence should be emitted or this is end of the list.
    //  01 - Delta is one integer, i.e difference between TokenIds.
    //  10 - Delta is two integers, i.e difference between FieldIds and TokenId.
    //  11 - Delta is three integers, i.e difference between DocumentIds, then FieldId and TokenId.
    //
    // Encoder uses the array of integers as a buffer of fixed size. Once it is filled with the data, 
    // it can be encoded using Group VarInt and flushed to persistent storage. Group VarInt encodes data by
    // groups of 4 integers. Therefore depending on how much data is currently in the buffer, 0,1,2 or 3 integers
    // might be carried on to the next iteration.
    public class PostingListBinaryDeltaWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "BinaryCompressed";

        private static readonly int BlocksInMemory = 128;
        internal static readonly int FlushThreshold = 3 /* Remainder from encoding 1,2,3 bytes */ + 3 /* full occurrence */ + BlocksInMemory * (1 /* deltaSelector */ + 16 /* 16 deltas in one block */ * 3 /* delta can take up to 3 bytes */);

        private readonly int[] buffer;
        private readonly byte[] flushBuffer;
        private readonly IPersistentStorage persistentStorage;
        private Occurrence previous;
        private int deltaSelector;
        private int deltaSelectorOffset;
        private int deltaSelectorIndex;
        private bool first;
        private int bufferIndex;
        private long listStart;
        private long totalSize;

        private int remainingBlocks;
        #endregion

        public PostingListBinaryDeltaWriter(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
        {
        }

        public PostingListBinaryDeltaWriter(IPersistentStorage storage)
        {
            this.buffer = new int[FlushThreshold * 2];
            this.flushBuffer = new byte[GroupVarint.GetMaxEncodedSize(buffer.Length)];
            this.persistentStorage = storage;
        }

        #region API
        public void StartList(string token)
        {
            bufferIndex = 0;
            totalSize = 0;
            first = true;
            deltaSelector = 0;
            deltaSelectorOffset = 0;
            deltaSelectorIndex = 0;
            previous = Occurrence.Empty;
            remainingBlocks = BlocksInMemory;

            listStart = persistentStorage.Length;

            // Reserve space for continuation offset
            persistentStorage.WriteInt64LittleEndian(listStart, 0L);

            // Reserve space for the length of the list
            persistentStorage.WriteInt32LittleEndian(listStart + sizeof(long), 0);
        }

        public void AddOccurrence(Occurrence occurrence)
        {
            if (first)
            {
                checked
                {
                    buffer[bufferIndex++] = (int)occurrence.DocumentId;
                    buffer[bufferIndex++] = (int)occurrence.FieldId;
                    buffer[bufferIndex++] = (int)occurrence.TokenId;
                }

                previous = occurrence;
                first = false;
                deltaSelectorIndex = bufferIndex;
                bufferIndex++;
            }
            else
            {
                int n;
                if (previous.DocumentId == occurrence.DocumentId)
                {
                    if (previous.FieldId == occurrence.FieldId)
                    {
                        n = 1;
                        checked
                        {
                            buffer[bufferIndex++] = (int)occurrence.TokenId - (int)previous.TokenId;
                        }

                        // NOTE: Removed zero value as it will lead to extra trailing occurrences
                        //       because the last deltaSelector might have unsed bits,
                        //       i.e. when deltaSelectorOffset < 32
                        // if (previous.TokenId == occurrence.TokenId)
                        // {
                        //     n = 0;
                        // }
                        // else
                        // {
                        //     n = 1;
                        //     checked
                        //     {
                        //         buffer[bufferIndex++] = (int)occurrence.TokenId - (int)previous.TokenId;
                        //     }
                        // }
                    }
                    else
                    {
                        n = 2;
                        checked
                        {
                            buffer[bufferIndex++] = (int)occurrence.FieldId - (int)previous.FieldId;
                            buffer[bufferIndex++] = (int)occurrence.TokenId;
                        }
                    }
                }
                else
                {
                    n = 3;
                    checked
                    {
                        buffer[bufferIndex++] = (int)occurrence.DocumentId - (int)previous.DocumentId;
                        buffer[bufferIndex++] = (int)occurrence.FieldId;
                        buffer[bufferIndex++] = (int)occurrence.TokenId;
                    }
                }

                previous = occurrence;
                deltaSelector |= (n << deltaSelectorOffset);
                deltaSelectorOffset += 2;

                if (deltaSelectorOffset == 32)
                {
                    buffer[deltaSelectorIndex] = deltaSelector;
                    deltaSelector = 0;
                    deltaSelectorOffset = 0;

                    if (remainingBlocks == 1)
                    {
                        // Write data
                        var toKeep = bufferIndex % 4;
                        var toEncode = bufferIndex - toKeep;

                        var encodedSize = GroupVarint.Encode(buffer, 0, toEncode, flushBuffer, 0);
                        persistentStorage.WriteAll(persistentStorage.Length, flushBuffer.AsSpan(0, encodedSize));
                        totalSize += encodedSize;

                        // Copy not encoded bytes (0,1,2,3)
                        Array.Copy(buffer, bufferIndex - toKeep, buffer, 0, toKeep);
                        bufferIndex = toKeep;

                        deltaSelectorIndex = bufferIndex;
                        bufferIndex++;
                        remainingBlocks = BlocksInMemory;
                    }
                    else
                    {
                        // Reserve space for delta selector
                        deltaSelectorIndex = bufferIndex;
                        bufferIndex++;
                        remainingBlocks--;
                    }
                }
            }
        }

        public PostingListAddress EndList()
        {
            if (bufferIndex > 0)
            {
                buffer[deltaSelectorIndex] = deltaSelector;

                var encodedSize = GroupVarint.Encode(buffer, 0, bufferIndex, flushBuffer, 0);
                persistentStorage.WriteAll(persistentStorage.Length, flushBuffer.AsSpan(0, encodedSize));

                totalSize += encodedSize;
            }

            // Write length of the list
            // POSSIBLE BUG -- writing 4 bytes, when totalSize is 8 bytes
            persistentStorage.WriteAll(listStart + sizeof(long), BitConverter.GetBytes(totalSize).AsSpan(0, sizeof(int)));

            var listEnd = persistentStorage.Length;

            if (listEnd - listStart != totalSize + sizeof(long) + sizeof(int))
            {
                throw new InvalidOperationException();
            }

            return new PostingListAddress(listStart);
        }

        public void UpdateNextList(PostingListAddress address, PostingListAddress nextList)
        {
            var offset = address.Offset;
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

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }

        #endregion
    }
}
