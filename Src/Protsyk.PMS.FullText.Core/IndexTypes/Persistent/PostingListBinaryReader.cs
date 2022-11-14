using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListBinaryReader : IOccurrenceReader
    {
        #region Fields
        internal static readonly int ReadBufferSize = 4096;

        private readonly IPersistentStorage persistentStorage;
        #endregion

        public PostingListBinaryReader(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
        {
        }

        public PostingListBinaryReader(IPersistentStorage storage)
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
            private int selector1;
            private int selector2;
            private int selector3;
            private int selector4;
            private int state;
            private Occurrence current;

            public ReaderEnumerator(IPersistentStorage storage, PostingListAddress address)
            {
                this.persistentStorage = storage;
                this.address = address;
                this.buffer = new byte[ReadBufferSize];
                Reset();
            }

            public Occurrence Current => current;

            object IEnumerator.Current => current;

            public void Dispose()
            {
            }

            private bool EnsureBuffer(int size)
            {
                if (isEof)
                {
                    return (size <= (dataInBuffer - indxInBuffer));
                }

                if (size > buffer.Length)
                {
                    throw new Exception("Resize buffer");
                }

                if (indxInBuffer + size >= dataInBuffer)
                {
                    Array.Copy(buffer, indxInBuffer, buffer, 0, dataInBuffer - indxInBuffer);
                    dataInBuffer -= indxInBuffer;
                    indxInBuffer = 0;
                    var toRead = (int)Math.Min(buffer.Length - dataInBuffer, (listEndOffset - readOffset));
                    if (toRead == 0)
                    {
                        isEof = true;
                    }
                    else
                    {
                        persistentStorage.ReadAll(readOffset, buffer.AsSpan(dataInBuffer, toRead));
                        readOffset += toRead;
                        dataInBuffer += toRead;
                    }
                    return (size <= dataInBuffer);
                }

                return true;
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

                        var buffer = new byte[HeaderLength];
                        persistentStorage.ReadAll(readOffset, buffer);

                        continuationOffset = BitConverter.ToInt64(buffer, 0);
                        listEndOffset = readOffset + HeaderLength + BitConverter.ToInt32(buffer, sizeof(long));

                        readOffset += buffer.Length;
                        state = 1;
                    }

                    if (state == 1)
                    {
                        if (!ReadSelectors())
                        {
                            throw new Exception("Wrong data");
                        }

                        EnsureBuffer(selector1 + selector2 + selector3);

                        var docId = GroupVarint.ReadInt(buffer, indxInBuffer, selector1);
                        indxInBuffer += selector1;

                        var fieldId = GroupVarint.ReadInt(buffer, indxInBuffer, selector2);
                        indxInBuffer += selector2;

                        var tokenId = GroupVarint.ReadInt(buffer, indxInBuffer, selector3);
                        indxInBuffer += selector3;

                        state = (isEof && indxInBuffer >= dataInBuffer) ? 0 : 4;

                        current = Occurrence.O((ulong)docId, (ulong)fieldId, (ulong)tokenId);
                        return true;
                    }

                    if (state == 4)
                    {
                        EnsureBuffer(selector4);

                        var docId = GroupVarint.ReadInt(buffer, indxInBuffer, selector4);
                        indxInBuffer += selector4;

                        if (!ReadSelectors())
                        {
                            throw new Exception("Wrong data");
                        }

                        EnsureBuffer(selector1 + selector2);

                        var fieldId = GroupVarint.ReadInt(buffer, indxInBuffer, selector1);
                        indxInBuffer += selector1;

                        var tokenId = GroupVarint.ReadInt(buffer, indxInBuffer, selector2);
                        indxInBuffer += selector2;

                        state = (isEof && indxInBuffer >= dataInBuffer) ? 0 : 3;

                        current = Occurrence.O((ulong)docId, (ulong)fieldId, (ulong)tokenId);
                        return true;
                    }

                    if (state == 3)
                    {
                        EnsureBuffer(selector3 + selector4);

                        var docId = GroupVarint.ReadInt(buffer, indxInBuffer, selector3);
                        indxInBuffer += selector3;

                        var fieldId = GroupVarint.ReadInt(buffer, indxInBuffer, selector4);
                        indxInBuffer += selector4;

                        if (!ReadSelectors())
                        {
                            throw new Exception("Wrong data");
                        }

                        EnsureBuffer(selector1);

                        var tokenId = GroupVarint.ReadInt(buffer, indxInBuffer, selector1);
                        indxInBuffer += selector1;

                        state = (isEof && indxInBuffer >= dataInBuffer) ? 0 : 2;

                        current = Occurrence.O((ulong)docId, (ulong)fieldId, (ulong)tokenId);
                        return true;
                    }

                    if (state == 2)
                    {
                        EnsureBuffer(selector2 + selector3 + selector4);

                        var docId = GroupVarint.ReadInt(buffer, indxInBuffer, selector2);
                        indxInBuffer += selector2;

                        var fieldId = GroupVarint.ReadInt(buffer, indxInBuffer, selector3);
                        indxInBuffer += selector3;

                        var tokenId = GroupVarint.ReadInt(buffer, indxInBuffer, selector4);
                        indxInBuffer += selector4;

                        state = (isEof && indxInBuffer >= dataInBuffer) ? 0 : 1;

                        current = Occurrence.O((ulong)docId, (ulong)fieldId, (ulong)tokenId);
                        return true;
                    }

                    throw new Exception("What?");
                }
            }

            private bool ReadSelectors()
            {
                if (!EnsureBuffer(1))
                {
                    return false;
                }

                int selector = (int)buffer[indxInBuffer++];

                selector4 = (selector & 0b11) + 1;
                selector3 = ((selector >> 2) & 0b11) + 1;
                selector2 = ((selector >> 4) & 0b11) + 1;
                selector1 = ((selector >> 6) & 0b11) + 1;

                return true;
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
