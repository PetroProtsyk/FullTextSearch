﻿using System.Buffers.Binary;
using System.Collections;
using System.IO;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core;

public class PostingListBinaryDeltaReader : IOccurrenceReader
{
    internal const int ReadBufferSize = 4_096;

    private readonly IPersistentStorage persistentStorage;

    public PostingListBinaryDeltaReader(string folder, string fileNamePostingLists)
        : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
    {
    }

    public PostingListBinaryDeltaReader(IPersistentStorage storage)
    {
        this.persistentStorage = storage;
    }

    #region API
    public IPostingList Get(PostingListAddress address)
    {
        // return GetBasic(address);
        return new PostingListReaderImpl(persistentStorage, address);
    }
    #endregion

    #region ReaderEnumerator
    private sealed class PostingListReaderImpl : IPostingList
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

    private sealed class ReaderEnumerator : IEnumerator<Occurrence>
    {
        private const int HeaderLength = sizeof(long) + sizeof(int);
        private readonly IPersistentStorage persistentStorage;
        private readonly PostingListAddress address;
        private long readOffset;
        private readonly byte[] buffer;
        private readonly int[] selectors;
        private int dataInBuffer;
        private int indxInBuffer;
        private bool isEof;
        private long continuationOffset;
        private long listEndOffset;
        private uint deltaSelector;
        private int selectorIndex; // Selector for GroupVarInt
        private int state;
        private Occurrence current;

        public ReaderEnumerator(IPersistentStorage storage, PostingListAddress address)
        {
            this.persistentStorage = storage;
            this.address = address;
            this.buffer = new byte[ReadBufferSize];
            this.selectors = new int[4];
            this.selectorIndex = 4;
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

        private int NextInteger()
        {
            if (selectorIndex == 4)
            {
                if (!EnsureBuffer(1))
                {
                    throw new Exception("Wrong data");
                }

                int selector = (int)buffer[indxInBuffer++];
                selectors[3] = (selector & 0b11) + 1;
                selectors[2] = ((selector >> 2) & 0b11) + 1;
                selectors[1] = ((selector >> 4) & 0b11) + 1;
                selectors[0] = ((selector >> 6) & 0b11) + 1;
                selectorIndex = 0;
            }

            if (!EnsureBuffer(selectors[selectorIndex]))
            {
                throw new Exception("Wrong data");
            }

            var result = GroupVarint.ReadInt(buffer.AsSpan(indxInBuffer), selectors[selectorIndex]);
            indxInBuffer += selectors[selectorIndex];
            selectorIndex++;
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

                    var buffer = new byte[HeaderLength];
                    persistentStorage.ReadAll(readOffset, buffer);

                    continuationOffset = BitConverter.ToInt64(buffer, 0);
                    listEndOffset = readOffset + HeaderLength + BitConverter.ToInt32(buffer, sizeof(long));

                    readOffset += buffer.Length;
                    state = 1;
                    selectorIndex = 4;
                }

                if (state == 1)
                {
                    current = new Occurrence((ulong)NextInteger(), (ulong)NextInteger(), (ulong)NextInteger());
                    state = 2;
                    return true;
                }

                if (state == 2)
                {
                    if (isEof && indxInBuffer >= dataInBuffer)
                    {
                        state = 0;
                    }
                    else
                    {
                        deltaSelector = (uint)NextInteger();

                        if (deltaSelector == 0)
                        {
                            state = 0;
                        }
                        else
                        {
                            state = 3;
                        }
                    }
                }

                if (state == 3)
                {
                    int delta = (int)(deltaSelector & 0b00000011);
                    deltaSelector >>= 2;

                    switch (delta)
                    {
                        case 0:
                            {
                                throw new Exception("Zero delta is not used, see comments in DeltaWriter");
                            }
                        case 1:
                            {
                                var deltaToken = (ulong)NextInteger();
                                current = new Occurrence(current.DocumentId,
                                                       current.FieldId,
                                                       current.TokenId + deltaToken);
                                break;
                            }
                        case 2:
                            {
                                var deltaFieldId = (ulong)NextInteger();
                                var token = (ulong)NextInteger();
                                current = new Occurrence(current.DocumentId,
                                                         current.FieldId + deltaFieldId,
                                                         token);
                                break;
                            }
                        case 3:
                            {
                                var deltaDocId = (ulong)NextInteger();
                                var fieldId = (ulong)NextInteger();
                                var token = (ulong)NextInteger();

                                current = new Occurrence(current.DocumentId + deltaDocId, fieldId, token);
                                break;
                            }
                        default:
                            {
                                throw new Exception("Something wrong");
                            }
                    }

                    if (deltaSelector == 0)
                    {
                        state = 2;
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
            selectorIndex = 4;
        }
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
        persistentStorage?.Dispose();
    }
    #endregion

    #region API
    // TODO: Need unit test for this
    public IPostingList GetBasic(PostingListAddress address)
    {
        var offset = address.Offset;
        Span<byte> buffer = stackalloc byte[8 + 4];
        var occurrences = new List<Occurrence>();

        while (true)
        {
            persistentStorage.ReadAll(offset, buffer);

            long continuationOffset = BinaryPrimitives.ReadInt64LittleEndian(buffer);
            int length = BinaryPrimitives.ReadInt32LittleEndian(buffer[8..]);

            var dataBuffer = new byte[length];
            persistentStorage.ReadAll(offset + sizeof(long) + sizeof(int), dataBuffer);

            ParseBufferTo(dataBuffer, occurrences);

            if (continuationOffset == 0)
            {
                break;
            }
            else
            {
                offset = continuationOffset;
            }
        }

        return new PostingListArray(occurrences.ToArray());
    }

    private static void ParseBufferTo(ReadOnlySpan<byte> buffer, List<Occurrence> occurrences)
    {
        var numbers = GroupVarint.Decode(buffer);

        var o = new Occurrence((ulong)numbers[0],
                               (ulong)numbers[1],
                               (ulong)numbers[2]);
        occurrences.Add(o);
        int i = 3;
        while (i < numbers.Count)
        {
            uint deltaSelector = (uint)numbers[i];
            ++i;
            while (deltaSelector > 0)
            {
                int delta = (int)(deltaSelector & 0b00000011);
                deltaSelector >>= 2;

                if (i + delta > numbers.Count)
                {
                    throw new Exception("Attempt to read above data");
                }

                switch (delta)
                {
                    case 0:
                        {
                            throw new Exception("Zero delta is not used, see comments in DeltaWriter");
                        }
                    case 1:
                        {
                            o = new Occurrence(o.DocumentId, o.FieldId, o.TokenId + (ulong)numbers[i]);
                            i += 1;
                            occurrences.Add(o);
                            break;
                        }
                    case 2:
                        {
                            o = new Occurrence(o.DocumentId, o.FieldId + (ulong)numbers[i], (ulong)numbers[i + 1]);
                            i += 2;
                            occurrences.Add(o);
                            break;
                        }
                    case 3:
                        {
                            o = new Occurrence(o.DocumentId + (ulong)numbers[i], (ulong)numbers[i + 1], (ulong)numbers[i + 2]);
                            i += 3;
                            occurrences.Add(o);
                            break;
                        }
                    default:
                        {
                            throw new Exception("Something wrong");
                        }
                }
            }
        }
    }

    #endregion
}
