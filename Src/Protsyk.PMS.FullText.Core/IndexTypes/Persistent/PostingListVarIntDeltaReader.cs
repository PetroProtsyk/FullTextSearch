using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Protsyk.PMS.FullText.Core.Common;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListVarIntDeltaReader : IOccurrenceReader
    {
        #region Fields
        private readonly IPersistentStorage persistentStorage;
        #endregion

        public PostingListVarIntDeltaReader(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
        {
        }

        public PostingListVarIntDeltaReader(IPersistentStorage storage)
        {
            this.persistentStorage = storage;
        }

        #region API
        public IPostingList Get(PostingListAddress address)
        {
            return new PostingListReaderImpl(persistentStorage, address);
        }
        #endregion

        #region ReaderEnumerator
        private class PostingListReaderImpl : IPostingList, ISkipList
        {
            private readonly IPersistentStorage storage;
            private readonly PostingListAddress address;
            private Occurrence firstOccurrence;

            public PostingListReaderImpl(IPersistentStorage storage, PostingListAddress address)
            {
                this.storage = storage;
                this.address = address;
                this.firstOccurrence = Occurrence.Empty;
            }

            public IEnumerable<Occurrence> LowerBound(Occurrence c)
            {
                firstOccurrence = c;
                return this;
            }

            public IEnumerator<Occurrence> GetEnumerator()
            {
                return new ReaderEnumerator(storage, address, firstOccurrence);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private class ReaderEnumerator : IEnumerator<Occurrence>
        {
            #region Fields
            private const int SkipSearchBlocksThreshold = 8;
            private static readonly int HeaderLength = sizeof(long) + sizeof(int);
            private readonly IPersistentStorage persistentStorage;
            private readonly PostingListAddress address;
            private readonly byte[] buffer;
            private readonly Occurrence firstOccurrence;
            private long readOffset;
            private int dataInBuffer;
            private int indxInBuffer;
            private bool isEof;
            private long continuationOffset;
            private long listEndOffset;
            private Occurrence current;
            private int state;
            #endregion

            #region Methods
            public ReaderEnumerator(IPersistentStorage storage, PostingListAddress address, Occurrence firstOccurrence)
            {
                this.persistentStorage = storage;
                this.address = address;
                this.state = 0;
                this.buffer = new byte[PostingListVarIntDeltaWriter.BlockSize];
                this.firstOccurrence = firstOccurrence;
                Reset();
            }

            private ulong NextNumber()
            {
                if (indxInBuffer >= dataInBuffer)
                {
                    var toRead = buffer.Length;
                    if (listEndOffset - readOffset < toRead)
                    {
                        checked
                        {
                            toRead = (int)(listEndOffset - readOffset);
                        }
                    }

                    if (toRead == 0)
                    {
                        isEof = true;
                        return 0;
                    }

                    persistentStorage.ReadAll(readOffset, buffer, 0, toRead);

                    for (int i=toRead; i<buffer.Length; ++i)
                    {
                        buffer[i] = 0;
                    }

                    readOffset += toRead;
                    dataInBuffer = toRead;
                    indxInBuffer = 0;
                }

                indxInBuffer += VarInt.ReadVUInt64(buffer, indxInBuffer, out var result);
                return result;
            }
            #endregion

            #region IEnumerator
            public Occurrence Current => current;

            object IEnumerator.Current => current;

            public bool MoveNext()
            {
                while (true)
                {
                    if (state == 0)
                    {
                        if (continuationOffset > 0)
                        {
                            readOffset = continuationOffset;
                            isEof = false;
                            dataInBuffer = 0;
                            indxInBuffer = 0;
                        }

                        if (isEof)
                        {
                            return false;
                        }

                        var header = new byte[HeaderLength];
                        persistentStorage.ReadAll(readOffset, header, 0, header.Length);

                        continuationOffset = BitConverter.ToInt64(header, 0);
                        listEndOffset = readOffset + HeaderLength + BitConverter.ToInt32(header, sizeof(long));

                        readOffset += header.Length;
                        state = 17;
                    }

                    if (state == 17)
                    {
                        // Seek state
                        var left = (listEndOffset - readOffset) / buffer.Length;
                        if (left < SkipSearchBlocksThreshold || !(current < firstOccurrence))
                        {
                            state = 1;
                        }
                        else
                        {
                            // Allocate space for two blocks, find two consecutive blocks that contain
                            // occurrence that is requested
                            var t = new byte[buffer.Length * 2];
                            var a = 0L;
                            var b = left;
                            var mid = a;
                            while (a != b)
                            {
                                mid = (a + b) >> 1;

                                // Read two blocks
                                var midOffset = readOffset + buffer.Length * mid;
                                var inList = (listEndOffset - midOffset - buffer.Length);
                                var toRead = inList > t.Length ? t.Length : (int)inList;
                                persistentStorage.ReadAll(midOffset, t, 0, toRead);

                                // Block 1: Full occurrence
                                var j = 0;
                                j += VarInt.ReadVUInt64(t, j, out var o1_1);
                                j += VarInt.ReadVUInt64(t, j, out var o1_2);
                                j += VarInt.ReadVUInt64(t, j, out var o1_3);

                                // Block 2: Full occurrence
                                j = buffer.Length;
                                j += VarInt.ReadVUInt64(t, j, out var o2_1);
                                j += VarInt.ReadVUInt64(t, j, out var o2_2);
                                j += VarInt.ReadVUInt64(t, j, out var o2_3);

                                if (Occurrence.O(o1_1, o1_2, o1_3).CompareTo(firstOccurrence) < 0)
                                {
                                     if (Occurrence.O(o2_1, o2_2, o2_3).CompareTo(firstOccurrence) >= 0)
                                     {
                                         break;
                                     }
                                     else
                                     {
                                         a = mid + 1;
                                     }
                                }
                                else
                                {
                                    b = mid;
                                }
                            }

                            state = 1;
                            indxInBuffer = 0;
                            dataInBuffer = 0;
                            readOffset   = readOffset + buffer.Length * mid;
                        }
                    }

                    if (state == 1)
                    {
                        current = Occurrence.O(NextNumber(),
                                               NextNumber(),
                                               NextNumber());
                        state = 2;

                        if (!(current < firstOccurrence))
                        {
                            return true;
                        }
                    }

                    if (state == 2)
                    {
                        if (indxInBuffer >= dataInBuffer)
                        {
                            if (readOffset < listEndOffset)
                            {
                                // Next block
                                state = 1;
                                continue;
                            }
                        }

                        int delta = (int)NextNumber();

                        switch (delta)
                        {
                            case 0:
                                {
                                    if (readOffset < listEndOffset)
                                    {
                                        if (indxInBuffer < dataInBuffer)
                                        {
                                            // Case when buffer is filled with zeros at the end
                                            indxInBuffer = dataInBuffer;
                                        }
                                        state = 1; // Next block
                                    }
                                    else
                                    {
                                        state = 0; // Continuation or EOF
                                    }
                                    continue;
                                }
                            case 1:
                                {
                                    // Next is the same as current
                                    break;
                                }
                            case 2:
                                {
                                    var deltaToken = NextNumber();
                                    current = Occurrence.O(current.DocumentId,
                                                           current.FieldId,
                                                           current.TokenId + deltaToken);
                                    break;
                                }
                            case 3:
                                {
                                    var deltaFieldId = NextNumber();
                                    var token = NextNumber();
                                    current = Occurrence.O(current.DocumentId,
                                                           current.FieldId + deltaFieldId,
                                                           token);
                                    break;
                                }
                            case 4:
                                {
                                    var deltaDocId = NextNumber();
                                    var fieldId = NextNumber();
                                    var token = NextNumber();

                                    current = Occurrence.O(current.DocumentId + deltaDocId,
                                                           fieldId,
                                                           token);
                                    break;
                                }
                            default:
                                {
                                    throw new Exception("Something wrong");
                                }
                        }

                        if (!(current < firstOccurrence))
                        {
                            return true;
                        }
                    }
                }
            }

            public void Reset()
            {
                state = 0;
                dataInBuffer = 0;
                indxInBuffer = 0;
                readOffset = address.Offset;
                isEof = false;
                listEndOffset = 0;
                continuationOffset = 0;
            }

            public void Dispose()
            {
            }
            #endregion
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
