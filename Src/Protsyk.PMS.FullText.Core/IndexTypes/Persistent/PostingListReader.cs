using System;
using System.IO;
using System.Linq;
using System.Text;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PostingListReader : IOccurrenceReader
    {
        #region Fields
        internal static readonly int ReadBufferSize = 256;

        private readonly IPersistentStorage persistentStorage;
        private readonly IDataSerializer<Occurrence> occurrenceSerializer;
        #endregion

        public PostingListReader(string folder, string fileNamePostingLists)
        {
            this.persistentStorage = new FileStorage(Path.Combine(folder, fileNamePostingLists));
            this.occurrenceSerializer = new TextOccurrenceSerializer();
        }

        #region API
        public IPostingList Get(PostingListAddress address)
        {
            var offset = address.Offset;
            var line = new StringBuilder();
            var buffer = new byte[ReadBufferSize];
            var done = false;

            while (!done)
            {
                int read = persistentStorage.Read(offset, buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                offset += read;

                for (int i=0; i<read; ++i)
                {
                    if (buffer[i] == PostingListWriter.EmptyContinuationAddress[0])
                    {
                        read = persistentStorage.Read(offset - read + i, buffer, 0, PostingListWriter.EmptyContinuationAddress.Length);
                        var nextOffsetText = System.Text.Encoding.UTF8.GetString(buffer, 4, 8);
                        var nextOffset = Convert.ToInt32(nextOffsetText, 16);
                        if (nextOffset < 0)
                        {
                            done = true;
                            break;
                        }

                        line.Append(';');
                        offset = nextOffset;
                        break;
                    }

                    //if (buffer[i] == '\r' || buffer[i] == '\n')
                    //{
                    //    done = true;
                    //    break;
                    //}

                    line.Append((char)buffer[i]);
                }
            }

            return new PostingListArray(line.ToString().Split(';').Select(Occurrence.Parse).ToArray());
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
