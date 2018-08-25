using System;
using System.Collections.Generic;
using System.IO;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListBinaryReader : IOccurrenceReader
    {
        #region Fields
        internal static readonly int ReadBufferSize = 256;

        private readonly IPersistentStorage persistentStorage;
        private readonly IDataSerializer<Occurrence> occurrenceSerializer;
        #endregion

        public PostingListBinaryReader(string folder, string fileNamePostingLists)
        {
            this.persistentStorage = new FileStorage(Path.Combine(folder, fileNamePostingLists));
            this.occurrenceSerializer = new TextOccurrenceSerializer();
        }

        #region API
        public IPostingList Get(PostingListAddress address)
        {
            var offset = address.Offset;
            var buffer = new byte[sizeof(long) + sizeof(int)];
            var occurrences = new List<Occurrence>();

            while (true)
            {
                persistentStorage.ReadAll(offset, buffer, 0, buffer.Length);

                long continuationOffset = BitConverter.ToInt64(buffer, 0);
                int length = BitConverter.ToInt32(buffer, sizeof(long));

                var dataBuffer = new byte[length];
                persistentStorage.ReadAll(offset + sizeof(long) + sizeof(int), dataBuffer, 0, dataBuffer.Length);

                var numbers = GroupVarint.Decode(dataBuffer);
                if (numbers.Count % 3 != 0)
                {
                    throw new InvalidDataException();
                }

                for (int i = 0; i < numbers.Count; i += 3)
                {
                    occurrences.Add(new Occurrence((ulong)numbers[i],
                                                   (ulong)numbers[i + 1],
                                                   (ulong)numbers[i + 2]));
                }

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
        #endregion

        #region IDisposable
        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
        #endregion
    }
}
