using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Protsyk.PMS.FullText.Core.Common.Persistance;
using System.Text;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "Text";

        internal static readonly string EmptyContinuationAddress = " -> FFFFFFFF";

        private static readonly int MemoryBufferThreshold = 65536;

        private readonly IPersistentStorage persistentStorage;
        private readonly StringBuilder buffer;

        private long count;
        private PostingListAddress? currentList;
        #endregion

        public PostingListWriter(string folder, string fileNamePostingLists)
            : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
        {
        }

        public PostingListWriter(IPersistentStorage storage)
        {
            this.persistentStorage = storage;
            this.buffer = new StringBuilder();
            this.buffer.EnsureCapacity(MemoryBufferThreshold);
        }

        #region API
        public void StartList(string token)
        {
            if (currentList != null)
            {
                throw new InvalidOperationException("Previous list was not finished");
            }

            count = 0;
            buffer.Clear();
            currentList = new PostingListAddress(persistentStorage.Length);
        }

        public void AddOccurrence(Occurrence occurrence)
        {
            if (currentList == null)
            {
                throw new InvalidOperationException("Previous list was started");
            }

            if (count != 0)
            {
                buffer.Append(";");
            }

            buffer.AppendFormat("[{0},{1},{2}]", occurrence.DocumentId, occurrence.FieldId, occurrence.TokenId);
            ++count;

            if (buffer.Length + 128 >= MemoryBufferThreshold)
            {
                var bufferData = System.Text.Encoding.UTF8.GetBytes(buffer.ToString());
                persistentStorage.WriteAll(persistentStorage.Length, bufferData, 0, bufferData.Length);
                buffer.Clear();
            }
        }

        public PostingListAddress EndList()
        {
            if (currentList == null)
            {
                throw new InvalidOperationException("Previous list was started");
            }

            if (buffer.Length > 0)
            {
                var bufferData = System.Text.Encoding.UTF8.GetBytes(buffer.ToString());
                persistentStorage.WriteAll(persistentStorage.Length, bufferData, 0, bufferData.Length);
                buffer.Clear();
            }

            // This posting list does not have continuation
            var data = System.Text.Encoding.UTF8.GetBytes(EmptyContinuationAddress);
            persistentStorage.WriteAll(persistentStorage.Length, data, 0, data.Length);

            var listEnd = persistentStorage.Length;

            data = System.Text.Encoding.UTF8.GetBytes(Environment.NewLine);
            persistentStorage.WriteAll(persistentStorage.Length, data, 0, data.Length);

            var result = currentList.Value;

            currentList = null;
            count = 0;

            return result;
        }

        public void UpdateNextList(PostingListAddress address, PostingListAddress nextList)
        {
            var offset = FindContinuationOffset(address);
            if (offset < 0)
            {
                throw new InvalidOperationException("Continuation is not found. The list might already have it");
            }
            var data = System.Text.Encoding.UTF8.GetBytes($" -> {nextList.Offset.ToString("X8")}");
            persistentStorage.WriteAll(offset, data, 0, data.Length);
        }

        private long FindContinuationOffset(PostingListAddress address)
        {
            var offset = address.Offset;
            var buffer = new byte[PostingListReader.ReadBufferSize];
            while (true)
            {
                int read = persistentStorage.Read(offset, buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                offset += read;

                for (int i = 0; i < read; ++i)
                {
                    if (buffer[i] == EmptyContinuationAddress[0])
                    {
                        var readNext = persistentStorage.Read(offset - read + i, buffer, 0, EmptyContinuationAddress.Length);
                        var nextOffsetText = System.Text.Encoding.UTF8.GetString(buffer, 4, 8);
                        var nextOffset = Convert.ToInt32(nextOffsetText, 16);
                        if (nextOffset < 0)
                        {
                            return offset - read + i;
                        }

                        offset = nextOffset;
                        break;
                    }
                }
            }

            return -1;
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
