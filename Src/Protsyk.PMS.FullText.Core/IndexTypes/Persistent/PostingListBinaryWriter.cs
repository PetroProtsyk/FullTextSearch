using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListBinaryWriter : IOccurrenceWriter
    {
        #region Fields
        public static readonly string Id = "Binary";

        private readonly IPersistentStorage persistentStorage;
        private readonly List<int> occurrences;
        #endregion

        public PostingListBinaryWriter(string folder, string fileNamePostingLists)
        {
            this.persistentStorage = new FileStorage(Path.Combine(folder, PersistentIndex.FileNamePostingLists));
            this.occurrences = new List<int>();
        }

        #region API
        public void StartList(string token)
        {
            occurrences.Clear();
        }

        public void AddOccurrence(Occurrence occurrence)
        {
            // TODO: Use write buffer and flush periodically

            checked
            {
                occurrences.Add((int)occurrence.DocumentId);
                occurrences.Add((int)occurrence.FieldId);
                occurrences.Add((int)occurrence.TokenId);
            }
        }

        public PostingListAddress EndList()
        {
            var data = GroupVarint.Encode(occurrences);

            var listStart = persistentStorage.Length;

            // Write continuation offset
            persistentStorage.WriteAll(listStart, BitConverter.GetBytes(0L), 0, sizeof(long));

            // Write length of the list
            persistentStorage.WriteAll(listStart + sizeof(long), BitConverter.GetBytes(data.Count), 0, sizeof(int));

            // Write data
            persistentStorage.WriteAll(listStart + sizeof(long) + sizeof(int), data.ToArray(), 0, data.Count);

            var listEnd = persistentStorage.Length;
            occurrences.Clear();
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
        #endregion

        #region IDisposable
        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
        #endregion
    }
}
