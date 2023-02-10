using System.Collections;
using System.IO;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core;

// TODO: PostingListPackedIntDelta and PostingListBinaryDelta Reader/Writer have a lot in common
//       Refactor these classes to have common encoding scheme in a base class
public class PostingListPackedIntDeltaReader : IOccurrenceReader
{
    #region Fields
    // Should be more than Writer can write in one PackedInt block.
    // Here an approximation is used - a value greater than FlushThreshold in Writer.
    internal static readonly int ReadBufferSize = 2 * 4096;

    private readonly IPersistentStorage persistentStorage;
    #endregion

    public PostingListPackedIntDeltaReader(string folder, string fileNamePostingLists)
        : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
    {
    }

    public PostingListPackedIntDeltaReader(IPersistentStorage storage)
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
        private static readonly int HeaderLength = sizeof(long) + sizeof(int);
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
        private IPackedInts data;
        private int dataIndex;
        private int state;
        private Occurrence current;

        public ReaderEnumerator(IPersistentStorage storage, PostingListAddress address)
        {
            this.persistentStorage = storage;
            this.address = address;
            this.buffer = new byte[ReadBufferSize];
            this.selectors = new int[4];
            this.data = null;
            this.dataIndex = 0;
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

        private IPackedInts ReadPacked()
        {
            if (!EnsureBuffer(1 + 4))
            {
                throw new Exception("Wrong data");
            }

            var bits = (int)buffer[indxInBuffer];
            var count = (int)buffer[indxInBuffer + 1] | ((int)buffer[indxInBuffer + 2] << 8) | ((int)buffer[indxInBuffer + 3] << 16) | ((int)buffer[indxInBuffer + 4] << 24);
            var size = 1 + 4 + (7 + count * bits) / 8;

            if (!EnsureBuffer(size))
            {
                throw new Exception("Wrong data");
            }

            // NOTE: Instead of creating instance of packed integers,
            //       one can decode data from the read buffer directly.
            var packed = PackedInts.Load(buffer.AsSpan(indxInBuffer, size));
            indxInBuffer += size;
            return packed;
        }

        private int NextInteger()
        {
            if (data is null || dataIndex >= data.Length)
            {
                data = ReadPacked();
                dataIndex = 0;
            }

            return data.Get(dataIndex++);
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

                    var headerBuffer = new byte[HeaderLength];
                    persistentStorage.ReadAll(readOffset, headerBuffer);

                    continuationOffset = BitConverter.ToInt64(headerBuffer, 0);
                    listEndOffset = readOffset + HeaderLength + BitConverter.ToInt32(headerBuffer, sizeof(long));

                    readOffset += headerBuffer.Length;
                    state = 1;
                    data = null;
                    dataIndex = 0;
                }

                if (state == 1)
                {
                    current = new Occurrence((ulong)NextInteger(),
                                           (ulong)NextInteger(),
                                           (ulong)NextInteger());
                    state = 2;
                    return true;
                }

                if (state == 2)
                {
                    if (isEof && indxInBuffer >= dataInBuffer && (data is null || dataIndex >= data.Length))
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

                                current = new Occurrence(current.DocumentId + deltaDocId,
                                                       fieldId,
                                                       token);
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
            data = null;
            dataIndex = 0;
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
