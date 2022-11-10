using System;
using System.Buffers;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Persistance
{
    internal static class IPersistentStorageExtensions
    {
        public static int AppendUtf8Bytes(this IPersistentStorage storage, ReadOnlySpan<char> chars)
        {
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(chars.Length));

            try
            {
                var byteCount = Encoding.UTF8.GetBytes(chars, rentedBuffer);

                storage.WriteAll(storage.Length, rentedBuffer.AsSpan(0, byteCount));

                return byteCount;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }
}