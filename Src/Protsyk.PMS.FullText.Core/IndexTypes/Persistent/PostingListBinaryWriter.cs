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
        private readonly IDataSerializer<Occurrence> occurrenceSerializer;
        private readonly List<int> occurrences;
        #endregion

        public PostingListBinaryWriter(string folder, string fileNamePostingLists)
        {
            this.persistentStorage = new FileStorage(Path.Combine(folder, PersistentIndex.FileNamePostingLists));
            this.occurrenceSerializer = new TextOccurrenceSerializer();
            this.occurrences = new List<int>();
        }

        #region API
        public void StartList(string token)
        {
            occurrences.Clear();
        }

        public void AddOccurrence(Occurrence occurrence)
        {
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
            persistentStorage.WriteAll(listStart + sizeof(long), BitConverter.GetBytes(data.Length), 0, sizeof(int));

            // Write data
            persistentStorage.WriteAll(listStart + sizeof(long) + sizeof(int), data, 0, data.Length);

            var listEnd = persistentStorage.Length;
            occurrences.Clear();
            return new PostingListAddress(listStart);
        }

        public void UpdateNextList(PostingListAddress address, PostingListAddress nextList)
        {
            persistentStorage.WriteAll(address.Offset, BitConverter.GetBytes(nextList.Offset), 0, sizeof(long));
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
