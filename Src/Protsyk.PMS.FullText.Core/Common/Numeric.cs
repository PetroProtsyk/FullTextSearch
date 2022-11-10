using System;

namespace Protsyk.PMS.FullText.Core.Common
{
    public static class Numeric
    {
        public static ulong MaxValue(int typeSize)
        {
            if (typeSize <= 0 || typeSize > sizeof(long))
                throw new ArgumentOutOfRangeException();

            switch (typeSize)
            {
                case 8: return ulong.MaxValue;
                case 4: return uint.MaxValue;
                case 2: return ushort.MaxValue;
                case 1: return byte.MaxValue;
            }

            checked
            {
                ulong maxValue = 1;
                for (int i = 1; i < typeSize * 8; i++)
                {
                    maxValue = (maxValue << 1) | 1;
                }
                return maxValue;
            }
        }

        public static int GetByteSize(int value) => sizeof(int);

        public static int GetByteSize(uint value) => sizeof(uint);

        public static int ReadInt(byte[] buffer, int startIndex, out int value)
        {
            value = BitConverter.ToInt32(buffer, startIndex);
            return sizeof(int);
        }

        public static int ReadUInt(byte[] buffer, int startIndex, out uint value)
        {
            value = BitConverter.ToUInt32(buffer, startIndex);
            return sizeof(uint);
        }

        public static int WriteInt(int value, byte[] buffer, int startIndex)
        {
            buffer[startIndex] = (byte)value;
            buffer[startIndex + 1] = (byte)(value >> 8);
            buffer[startIndex + 2] = (byte)(value >> 16);
            buffer[startIndex + 3] = (byte)(value >> 24);
            return 4;
        }

        public static int WriteUInt(uint x, byte[] buffer, int startIndex)
        {
            buffer[startIndex] = (byte)x;
            buffer[startIndex + 1] = (byte)(x >> 8);
            buffer[startIndex + 2] = (byte)(x >> 16);
            buffer[startIndex + 3] = (byte)(x >> 24);
            return 4;
        }
    }
}
