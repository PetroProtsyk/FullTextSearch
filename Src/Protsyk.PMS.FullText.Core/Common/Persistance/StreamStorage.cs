using System;
using System.IO;

namespace Protsyk.PMS.FullText.Core.Common.Persistance
{
    public abstract class StreamStorage<TStream> : IPersistentStorage
        where TStream : Stream
    {
        protected readonly TStream stream;

        public StreamStorage(TStream stream)
        {
            this.stream = stream;
        }

        public long Length
        {
            get { return stream.Length; }
        }

        public void ReadAll(long fileOffset, byte[] buffer, int offset, int count)
        {
            if (count != Read(fileOffset, buffer, offset, count))
            {
                throw new InvalidOperationException();
            }
        }

        public int Read(long fileOffset, byte[] buffer, int offset, int count)
        {
            var position = stream.Seek(fileOffset, SeekOrigin.Begin);
            if (position != fileOffset)
            {
                throw new InvalidOperationException();
            }

            int totalRead = 0;
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);
                if (read == 0)
                {
                    break;
                }
                count -= read;
                offset += read;
                totalRead += read;
            }

            return totalRead;
        }

        public void WriteAll(long fileOffset, byte[] buffer, int offset, int count)
        {
            stream.Seek(fileOffset, SeekOrigin.Begin);
            stream.Write(buffer, offset, count);
        }

        public virtual void Flush()
        {
            stream.Flush();
        }

        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}
