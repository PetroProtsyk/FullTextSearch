using System;

namespace Protsyk.PMS.FullText.Core.Common.Persistance
{
    public interface IPersistentStorage : IDisposable
    {
        long Length { get; }

        /// <summary>
        /// Read count bytes from storage. If less data read, throw exception
        /// </summary>
        void ReadAll(long fileOffset, Span<byte> buffer);

        /// <summary>
        /// Try to read count bytes from storage. Return actual bytes read
        /// </summary>
        int Read(long fileOffset, Span<byte> buffer);

        void WriteAll(long fileOffset, ReadOnlySpan<byte> buffer);

        void Flush();
    }
}
