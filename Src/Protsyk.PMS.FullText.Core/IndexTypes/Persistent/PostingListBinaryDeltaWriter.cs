using System;
using System.IO;
using System.Collections.Generic;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListBinaryDeltaWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "BinaryCompressed";

        private static readonly int flushThershold = 4096;

        private readonly List<int> buffer;
        private readonly List<int> deltaBuffer;
        private readonly List<byte> resultBuffer;
        private readonly FileStorage persistentStorage;
        private int deltaSelector;
        private Occurrence previous;
        private int deltaSelectorOffset;
        private bool first;
        #endregion

        public PostingListBinaryDeltaWriter(string folder, string fileNamePostingLists)
        {
            this.buffer = new List<int>(flushThershold * 2);
            this.deltaBuffer = new List<int>(16 /* number of deltas encoded in deltaSelector 32bit/2 */ * 3 /* Size of delta - max 3 */);
            this.resultBuffer = new List<byte>();
            this.persistentStorage = new FileStorage(Path.Combine(folder, PersistentIndex.FileNamePostingLists));
        }

        #region API
        public void StartList(string token)
        {
            buffer.Clear();
            deltaBuffer.Clear();

            // Reserve space for continuation offset
            resultBuffer.AddRange(BitConverter.GetBytes(0L));

            // Reserve space for length of the list
            resultBuffer.AddRange(BitConverter.GetBytes(0));

            first = true;
            deltaSelectorOffset = 0;
            deltaSelector = 0;
            previous = Occurrence.Empty;
        }

        public void AddOccurrence(Occurrence occurrence)
        {
            if (first)
            {
                checked
                {
                    buffer.Add((int)occurrence.DocumentId);
                    buffer.Add((int)occurrence.FieldId);
                    buffer.Add((int)occurrence.TokenId);
                }

                previous = occurrence;
                first = false;
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
                            deltaBuffer.Add((int)occurrence.TokenId - (int)previous.TokenId);
                        }

                        // NOTE: Removed zero value as it will lead to extra trailing occurrences
                        //       because the last deltaSelector might have unsed bits, i.e. when oi < 32
                        // if (previous.TokenId == occurrence.TokenId)
                        // {
                        //     n = 0;
                        // }
                        // else
                        // {
                        //     n = 1;
                        //     checked
                        //     {
                        //         deltaBuffer.Add((int)occurrence.TokenId - (int)previous.TokenId);
                        //     }
                        // }
                    }
                    else
                    {
                        n = 2;
                        checked
                        {
                            deltaBuffer.Add((int)occurrence.FieldId - (int)previous.FieldId);
                            deltaBuffer.Add((int)occurrence.TokenId);
                        }
                    }
                }
                else
                {
                    n = 3;
                    checked
                    {
                        deltaBuffer.Add((int)occurrence.DocumentId - (int)previous.DocumentId);
                        deltaBuffer.Add((int)occurrence.FieldId);
                        deltaBuffer.Add((int)occurrence.TokenId);
                    }
                }

                previous = occurrence;
                deltaSelector |= (n << deltaSelectorOffset);
                deltaSelectorOffset += 2;

                if (deltaSelectorOffset == 32)
                {
                    buffer.Add(deltaSelector);
                    buffer.AddRange(deltaBuffer);

                    deltaBuffer.Clear();
                    deltaSelector = 0;
                    deltaSelectorOffset = 0;

                    if (buffer.Count > flushThershold)
                    {
                        // Time to flush buffer
                        // GroupVar int encodes groups of 4 integers
                        while (buffer.Count % 4 != 0)
                        {
                            deltaBuffer.Add(buffer[buffer.Count - 1]);
                            buffer.RemoveAt(buffer.Count - 1);
                        }

                        GroupVarint.EncodeTo(buffer, resultBuffer);
                        buffer.Clear();

                        deltaBuffer.Reverse();
                        buffer.AddRange(deltaBuffer);
                        deltaBuffer.Clear();
                    }

                }
            }
        }

        public PostingListAddress EndList()
        {
            if (deltaSelectorOffset > 0)
            {
                buffer.Add(deltaSelector);
                buffer.AddRange(deltaBuffer);

                deltaBuffer.Clear();
                deltaSelector = 0;
                deltaSelectorOffset = 0;
            }

            GroupVarint.EncodeTo(buffer, resultBuffer);

            // Write length of the list
            var lengthBytes = BitConverter.GetBytes(resultBuffer.Count-sizeof(long)-sizeof(int));
            resultBuffer[sizeof(long) + 0] = lengthBytes[0];
            resultBuffer[sizeof(long) + 1] = lengthBytes[1];
            resultBuffer[sizeof(long) + 2] = lengthBytes[2];
            resultBuffer[sizeof(long) + 3] = lengthBytes[3];

            var listStart = persistentStorage.Length;

            persistentStorage.WriteAll(listStart, resultBuffer.ToArray(), 0, resultBuffer.Count);

            var listEnd = persistentStorage.Length;

            resultBuffer.Clear();
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
