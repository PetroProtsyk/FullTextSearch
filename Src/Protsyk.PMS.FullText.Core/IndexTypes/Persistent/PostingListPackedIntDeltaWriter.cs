﻿using System;
using System.IO;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    // This posting list uses Packed Int encoding and delta compression.
    public class PostingListPackedIntDeltaWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "PackedInt";

        private static readonly int BlocksInMemory = 128;
        internal static readonly int FlushThreshold = 3 /* Remainder from encoding 1,2,3 bytes */ + 3 /* full occurrence */ + BlocksInMemory * (1 /* deltaSelector */ + 16 /* 16 deltas in one block */ * 3 /* delta can take up to 3 bytes */);

        private readonly int[] buffer;
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

        public PostingListPackedIntDeltaWriter(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
        {
        }

        public PostingListPackedIntDeltaWriter(IPersistentStorage storage)
        {
            this.buffer = new int[FlushThreshold * 2];
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
            persistentStorage.WriteAll(listStart, BitConverter.GetBytes(0L), 0, sizeof(long));

            // Reserve space for the length of the list
            persistentStorage.WriteAll(listStart + sizeof(long), BitConverter.GetBytes(0), 0, sizeof(int));
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

                // NOTE: Use less 16 bits of delta selector, so that PackedInts can compress better
                //       Might be worth experimenting with other values 4, 8 bits or even dynamic 
                //       based on the contents of the buffer.
                if (deltaSelectorOffset == 16)
                {
                    buffer[deltaSelectorIndex] = deltaSelector;
                    deltaSelector = 0;
                    deltaSelectorOffset = 0;

                    if (remainingBlocks == 1)
                    {
                        var packed = PackedInts.Convert(buffer, 0, bufferIndex).GetBytes();
                        persistentStorage.WriteAll(persistentStorage.Length, packed, 0, packed.Length);
                        totalSize += packed.Length;

                        deltaSelectorIndex = 0;
                        bufferIndex = 1;
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

                var packed = PackedInts.Convert(buffer, 0, bufferIndex).GetBytes();
                persistentStorage.WriteAll(persistentStorage.Length, packed, 0, packed.Length);

                totalSize += packed.Length;
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

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }

        #endregion
    }
}
