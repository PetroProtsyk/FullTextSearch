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
        private class PostingListReaderImpl : IPostingList
        {
            private readonly IPersistentStorage storage;
            private readonly PostingListAddress address;

            public PostingListReaderImpl(IPersistentStorage storage, PostingListAddress address)
            {
                this.storage = storage;
                this.address = address;
            }

            public IEnumerator<Occurrence> GetEnumerator()
            {
                return new ReaderEnumerator(storage, address);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private class ReaderEnumerator : IEnumerator<Occurrence>
        {
            private static readonly int HeaderLength = sizeof(long) + sizeof(int);
            private readonly IPersistentStorage persistentStorage;
            private readonly PostingListAddress address;
            private long readOffset;
            private readonly byte[] buffer;
            private int dataInBuffer;
            private int indxInBuffer;
            private bool isEof;
            private long continuationOffset;
            private long listEndOffset;
            private Occurrence current;
            private int state;

            public ReaderEnumerator(IPersistentStorage storage, PostingListAddress address)
            {
                this.persistentStorage = storage;
                this.address = address;
                this.state = 0;
                this.buffer = new byte[PostingListVarIntDeltaWriter.BlockSize];
                Reset();
            }

            public Occurrence Current => current;

            object IEnumerator.Current => current;

            public void Dispose()
            {
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
                        state = 1;
                    }

                    if (state == 1)
                    {
                        current = Occurrence.O(NextNumber(),
                                               NextNumber(),
                                               NextNumber());
                        state = 2;
                        return true;
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

                        return true;
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
