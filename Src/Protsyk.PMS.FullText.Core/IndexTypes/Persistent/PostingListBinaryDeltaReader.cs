using System;
using System.Collections.Generic;
using System.IO;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListBinaryDeltaReader : IOccurrenceReader
    {
        #region Fields
        internal static readonly int ReadBufferSize = 256;

        private readonly IPersistentStorage persistentStorage;
        private readonly IDataSerializer<Occurrence> occurrenceSerializer;
        #endregion

        public PostingListBinaryDeltaReader(string folder, string fileNamePostingLists)
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

        private static void ParseBufferTo(byte[] buffer, IList<Occurrence> occurrences)
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
                                o = Occurrence.O(o.DocumentId, o.FieldId, o.TokenId + (ulong)numbers[i]);
                                i += 1;
                                occurrences.Add(o);
                                break;
                            }
                        case 2:
                            {
                                o = Occurrence.O(o.DocumentId, o.FieldId + (ulong)numbers[i], (ulong)numbers[i + 1]);
                                i += 2;
                                occurrences.Add(o);
                                break;
                            }
                        case 3:
                            {
                                o = Occurrence.O(o.DocumentId + (ulong)numbers[i], (ulong)numbers[i + 1], (ulong)numbers[i + 2]);
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

        #region IDisposable
        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
        #endregion
    }
}
