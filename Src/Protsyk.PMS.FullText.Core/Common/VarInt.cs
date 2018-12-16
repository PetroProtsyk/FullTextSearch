using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common
{
    public static class VarInt
    {
        public static int GetByteSize(ulong value)
        {
            int result = 1;
            while (value > 0x7F)
            {
                value >>= 7;
                result++;
            }
            return result;
        }

        public static int GetByteSize(uint value)
        {
            int result = 1;
            while (value > 0x7F)
            {
                value >>= 7;
                result++;
            }
            return result;
        }

        public static int WriteVUInt64(ulong value, byte[] buffer, int startIndex)
        {
            var index = startIndex;
            while (value > 0x7F)
            {
                buffer[index++] = (byte)(value | 0x80);
                value >>= 7;
            }
            buffer[index++] = (byte)value;
            return index - startIndex;
        }

        public static int WriteVInt64(long value, byte[] buffer, int startIndex)
        {
            return WriteVUInt64((ulong)value, buffer, startIndex);
        }

        public static int WriteVUInt32(uint value, byte[] buffer, int startIndex)
        {
            var index = startIndex;
            while (value > 0x7F)
            {
                buffer[index++] = (byte)(value | 0x80);
                value >>= 7;
            }
            buffer[index++] = (byte)value;
            return index - startIndex;
        }

        public static int WriteVInt32(int value, byte[] buffer, int startIndex)
        {
            return WriteVUInt32((uint)value, buffer, startIndex);
        }

        public static int ReadVUInt64(byte[] buffer, int startIndex, out ulong result)
        {
            int index = startIndex;
            int shift = 0;
            result = 0;
            while ((buffer[index] & 0x80) != 0)
            {
                result |= (((ulong)buffer[index++]) & 0x7FL) << shift;
                shift += 7;
            }
            result |= ((ulong)buffer[index++]) << shift;
            return index - startIndex;
        }

        public static int ReadVInt64(byte[] buffer, int startIndex, out long result)
        {
            var count = ReadVUInt64(buffer, startIndex, out var value);
            result = (long)value;
            return count;
        }

        public static int ReadVUInt32(byte[] buffer, int startIndex, out uint result)
        {
            var index = startIndex;
            var shift = 0;
            result = 0;
            while ((buffer[index] & 0x80) > 0)
            {
                result |= (uint)((buffer[index++] & 0x7F) << shift);
                shift += 7;
            }
            result |= (uint)(buffer[index++] << shift);
            return index - startIndex;
        }

        public static int ReadVInt32(byte[] buffer, int startIndex, out int result)
        {
            var count = ReadVUInt32(buffer, startIndex, out var value);
            result = (int)value;
            return count;
        }
    }
}
