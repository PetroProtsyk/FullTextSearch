using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core;

public class PostingListReader : IOccurrenceReader
{
    #region Fields
    internal static readonly int ReadBufferSize = 4096;

    private readonly IPersistentStorage persistentStorage;
    #endregion

    public PostingListReader(string folder, string fileNamePostingLists)
        : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
    {
    }

    public PostingListReader(IPersistentStorage storage)
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
        private readonly IPersistentStorage persistentStorage;
        private readonly PostingListAddress address;
        private long readOffset;

        private readonly byte[] buffer;
        private int dataInBuffer;
        private int indxInBuffer;
        private bool isEof;

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

        private char PeekChar()
        {
            if (isEof)
            {
                return '\0';
            }

            return (char)buffer[indxInBuffer];
        }

        private bool NextChar()
        {
            if (indxInBuffer + 1 >= dataInBuffer)
            {
                int read = persistentStorage.Read(readOffset, buffer);
                if (read == 0)
                {
                    isEof = true;
                    return false;
                }

                readOffset += read;
                dataInBuffer = read;
                indxInBuffer = 0;
                return true;
            }

            indxInBuffer++;
            return true;
        }

        private ulong ParseNumber()
        {
            var r = 0UL;
            var c = PeekChar();
            while (char.IsDigit(c))
            {
                r = r * 10 + (ulong)((int)c - (int)'0');

                if (!NextChar())
                {
                    break;
                }

                c = PeekChar();
            }
            return r;
        }

        public bool MoveNext()
        {
            if (isEof)
            {
                return false;
            }

            while (true)
            {
                if (!NextChar())
                {
                    throw new Exception("Wrong data");
                }

                if (PeekChar() != '[')
                {
                    throw new Exception("Wrong data");
                }
                else
                {
                    NextChar();
                }

                ulong docId = ParseNumber();

                if (PeekChar() != ',')
                {
                    throw new Exception("Wrong data");
                }
                else
                {
                    NextChar();
                }

                ulong fieldId = ParseNumber();

                if (PeekChar() != ',')
                {
                    throw new Exception("Wrong data");
                }
                else
                {
                    NextChar();
                }

                ulong tokenId = ParseNumber();

                if (PeekChar() != ']')
                {
                    throw new Exception("Wrong data");
                }
                else
                {
                    NextChar();
                }

                if (PeekChar() == ';')
                {
                    // There are should be more occurrences
                }
                else if (PeekChar() == PostingListWriter.EmptyContinuationAddress[0])
                {
                    bool empty = true;
                    long nextOffset = 0;
                    for (int i = 0; i < PostingListWriter.EmptyContinuationAddress.Length; ++i)
                    {
                        var c = PeekChar();
                        if (c != PostingListWriter.EmptyContinuationAddress[i])
                        {
                            empty = false;
                        }

                        if (char.IsDigit(c))
                        {
                            nextOffset = (nextOffset << 4) | (long)((int)c - (int)'0');
                        }
                        else if (c >= 'A' && c <= 'F')
                        {
                            nextOffset = (nextOffset << 4) | (long)(10 + (int)c - (int)'A');
                        }

                        if (!NextChar() && (i + 1) != PostingListWriter.EmptyContinuationAddress.Length)
                        {
                            throw new Exception("Wrong data");
                        }
                    }

                    if (empty)
                    {
                        isEof = true;
                    }
                    else
                    {
                        readOffset = nextOffset;
                        dataInBuffer = 0;
                        indxInBuffer = 0;
                    }
                }
                else
                {
                    throw new Exception("Wrong data");
                }

                current = Occurrence.O(docId, fieldId, tokenId);
                return true;
            }
        }

        public void Reset()
        {
            dataInBuffer = 0;
            indxInBuffer = 0;
            readOffset = address.Offset;
            isEof = false;
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
