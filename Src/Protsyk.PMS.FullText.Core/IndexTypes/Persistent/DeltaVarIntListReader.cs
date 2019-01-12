using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Protsyk.PMS.FullText.Core.Common;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class DeltaVarIntListReader : IDisposable
    {
        #region Fields
        private readonly IPersistentStorage persistentStorage;
        #endregion

        public DeltaVarIntListReader(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
        {
        }

        public DeltaVarIntListReader(IPersistentStorage storage)
        {
            this.persistentStorage = storage;
        }

        #region API
        public IEnumerable<ulong> Get(long listStart)
        {
            return new DeltaListReaderImpl(persistentStorage, listStart, 0UL);
        }

        public IEnumerable<ulong> GetLowerBound(long listStart, ulong value)
        {
            return new DeltaListReaderImpl(persistentStorage, listStart, value);
        }
        #endregion

        #region ReaderEnumerator
        private class DeltaListReaderImpl : IEnumerable<ulong>
        {
            private readonly IPersistentStorage storage;
            private readonly long listStart;
            private readonly ulong firstValue;

            public DeltaListReaderImpl(IPersistentStorage storage, long listStart, ulong firstValue)
            {
                this.storage = storage;
                this.listStart = listStart;
                this.firstValue = firstValue;
            }

            public IEnumerator<ulong> GetEnumerator()
            {
                return new ReaderEnumerator(storage, listStart, firstValue);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private class ReaderEnumerator : IEnumerator<ulong>
        {
            #region Fields
            private const int SkipSearchBlocksThreshold = 8;
            private static readonly int HeaderLength = 1 + sizeof(int) + sizeof(int);
            private readonly IPersistentStorage persistentStorage;
            private readonly long listStart;
            private readonly byte[] buffer;
            private readonly ulong firstValue;
            private long readOffset;
            private int dataInBuffer;
            private int indxInBuffer;
            private bool isEof;
            private long listEndOffset;
            private ulong current;
            private int state;
            #endregion

            #region Methods
            public ReaderEnumerator(IPersistentStorage storage, long listStart, ulong firstValue)
            {
                this.persistentStorage = storage;
                this.listStart = listStart;
                this.state = 0;
                this.firstValue = firstValue;
                this.buffer = new byte[DeltaVarIntListWriter.BlockSize];
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
            public ulong Current => current;

            object IEnumerator.Current => current;

            public bool MoveNext()
            {
                while (true)
                {
                    if (state == 0)
                    {
                        if (isEof)
                        {
                            return false;
                        }

                        var header = new byte[HeaderLength];
                        persistentStorage.ReadAll(readOffset, header, 0, header.Length);

                        if (header[0] != (byte)'L')
                        {
                            throw new Exception("Invalid List Header");
                        }

                        var listLength = BitConverter.ToInt32(header, 1);
                        var recordCount = BitConverter.ToInt32(header, 1 + sizeof(int));

                        listEndOffset = readOffset + HeaderLength + listLength;

                        readOffset += header.Length;
                        state = 17;
                    }

                    if (state == 17)
                    {
                        // Seek state
                        var left = (listEndOffset - readOffset) / buffer.Length;
                        if (left < SkipSearchBlocksThreshold || !(current < firstValue))
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

                                // Block 1: Full value
                                var j = 0;
                                j += VarInt.ReadVUInt64(t, j, out var v1);

                                // Block 2: Full value
                                j = buffer.Length;
                                j += VarInt.ReadVUInt64(t, j, out var v2);

                                if (v1 < firstValue)
                                {
                                     if (v2 >= firstValue)
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
                        current = NextNumber();
                        state = 2;

                        if (!(current < firstValue))
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

                        var delta = NextNumber();
                        if (delta == 0)
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
                                state = 0; // EOF
                            }
                            continue;
                        }

                        current += delta;
                        if (!(current < firstValue))
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
                readOffset = listStart;
                isEof = false;
                listEndOffset = 0;
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
