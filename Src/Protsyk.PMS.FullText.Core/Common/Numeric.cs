using System;

namespace PMS.Common.Encodings
{
    public static class Numeric
    {
        public static ulong MaxValue(int typeSize)
        {
            if (typeSize <= 0 || typeSize > sizeof(long))
                throw new ArgumentOutOfRangeException();

            if (typeSize == 8) return ulong.MaxValue;
            if (typeSize == 4) return uint.MaxValue;
            if (typeSize == 2) return ushort.MaxValue;
            if (typeSize == 1) return byte.MaxValue;

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

        public static int WriteUint(uint x, byte[] output, int index)
        {
            output[index] = (byte)x;
            output[index + 1] = (byte)(x >> 8);
            output[index + 2] = (byte)(x >> 16);
            output[index + 3] = (byte)(x >> 24);
            return 4;
        }
    }
}
