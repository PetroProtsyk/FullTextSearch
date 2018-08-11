using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "Text";

        internal static readonly string EmptyContinuationAddress = " -> FFFFFFFF";

        private readonly IPersistentStorage persistentStorage;
        private readonly IDataSerializer<Occurrence> occurrenceSerializer;
        private readonly List<Occurrence> occurrences;

        private PostingListAddress? currentList;
        #endregion

        public PostingListWriter(string folder, string fileNamePostingLists)
        {
            this.persistentStorage = new FileStorage(Path.Combine(folder, PersistentIndex.FileNamePostingLists));
            this.occurrenceSerializer = new TextOccurrenceSerializer();
            this.occurrences = new List<Occurrence>();
        }

        #region API
        public void StartList(string token)
        {
            if (currentList != null)
            {
                throw new InvalidOperationException("Previous list was not finished");
            }

            occurrences.Clear();
            currentList = new PostingListAddress(persistentStorage.Length);
        }

        public void AddOccurrence(Occurrence occurrence)
        {
            if (currentList == null)
            {
                throw new InvalidOperationException("Previous list was started");
            }

            occurrences.Add(occurrence);
        }

        public PostingListAddress EndList()
        {
            if (currentList == null)
            {
                throw new InvalidOperationException("Previous list was started");
            }

            var data = System.Text.Encoding.UTF8.GetBytes(string.Join(";", occurrences.Select(o => o.ToString())));
            persistentStorage.WriteAll(persistentStorage.Length, data, 0, data.Length);

            // This posting list does not have continuation
            data = System.Text.Encoding.UTF8.GetBytes(EmptyContinuationAddress);
            persistentStorage.WriteAll(persistentStorage.Length, data, 0, data.Length);

            var listEnd = persistentStorage.Length;

            data = System.Text.Encoding.UTF8.GetBytes(Environment.NewLine);
            persistentStorage.WriteAll(persistentStorage.Length, data, 0, data.Length);

            var result = currentList.Value;

            currentList = null;
            occurrences.Clear();

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
