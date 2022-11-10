using System;
using System.Buffers;
using System.Buffers.Binary;
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

        public static int WriteInt32LittleEndian(this IPersistentStorage storage, long fileOffset, int value)
        {
            Span<byte> buffer = stackalloc byte[4];

            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);

            storage.WriteAll(fileOffset, buffer);

            return 4;
        }

        public static int WriteInt64LittleEndian(this IPersistentStorage storage, long fileOffset, long value)
        {
            Span<byte> buffer = stackalloc byte[8];

            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);

            storage.WriteAll(fileOffset, buffer);

            return 8;
        }

        public static int AppendInt32LittleEndian(this IPersistentStorage storage, int value)
        {
            return WriteInt32LittleEndian(storage, storage.Length, value);
        }

        public static int AppendInt64LittleEndian(this IPersistentStorage storage, long value)
        {
            return WriteInt64LittleEndian(storage, storage.Length, value);
        }
    }
}