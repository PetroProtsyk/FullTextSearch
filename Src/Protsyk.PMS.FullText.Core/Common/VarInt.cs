using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PMS.Common.Encodings
{
    public static class VarInt
    {
        private static int[] values = { 2, 3, 4, 5, 6, 7, 8, 10 };

        public static int GetMaxByteLength(int bytesInValue)
        {
            if (bytesInValue <= 0 || bytesInValue > sizeof(ulong))
                throw new ArgumentOutOfRangeException(nameof(bytesInValue));

            return values[bytesInValue - 1];
        }

        public static int CalculateMaxByteLength(int bytesInValue)
        {
            if (bytesInValue <= 0 || bytesInValue > sizeof(ulong))
                throw new ArgumentOutOfRangeException(nameof(bytesInValue));

            ulong maxValue = Numeric.MaxValue(bytesInValue);
            return GetByteLength(maxValue);
        }

        public static int GetByteLength(ulong value)
        {
            int result = 1;
            while (value >= 0x80)
            {
                value >>= 7;
                result++;
            }
            return result;
        }

        public static int WriteUInt64Checked(ulong value, byte[] output, int offset)
        {
            int index = offset;
            while (value >= 0x80)
            {
                if (index >= output.Length) return 0;
                output[index++] = ((byte)(value | 0x80));
                value >>= 7;
            }
            if (index >= output.Length) return 0;
            output[index++] = (byte)value;
            return index - offset;
        }

        public static int WriteUInt32(uint value, byte[] output, int index)
        {
            var i = 0;
            while (value > 0x7F)
            {
                output[index++] = (byte)(value | 0x80);
                value >>= 7;
                ++i;
            }
            if ((value > 0) || (i == 0))
            {
                output[index] = (byte)value;
                ++i;
            }
            return i;
        }

        public static int WriteUInt32Checked(uint x, byte[] output, int index)
        {
            if (x < (1u << 7))
            {
                output[index] = (byte)x;
                return 1;
            }
            else
            {
                if (x < (1 << 21))
                {
                    if (x < (1 << 14))
                    {
                        output[index] = (byte)(x | 0x80);
                        output[index + 1] = (byte)(x >> 7);
                        return 2;
                    }
                    else
                    {
                        output[index] = (byte)(x | 0x80);
                        output[index + 1] = (byte)((x >> 7) | 0x80);
                        output[index + 2] = (byte)(x >> 14);
                        return 3;
                    }
                }
                else
                {
                    if (x < (1 << 28))
                    {
                        output[index] = (byte)(x | 0x80);
                        output[index + 1] = (byte)((x >> 7) | 0x80);
                        output[index + 2] = (byte)((x >> 14) | 0x80);
                        output[index + 3] = (byte)(x >> 21);
                        return 4;
                    }
                    else
                    {
                        output[index] = (byte)(x | 0x80);
                        output[index + 1] = (byte)((x >> 7) | 0x80);
                        output[index + 2] = (byte)((x >> 14) | 0x80);
                        output[index + 3] = (byte)((x >> 21) | 0x80);
                        output[index + 4] = (byte)(x >> 28);
                        return 5;
                    }
                }
            }
        }

        public static byte[] GetBytes(uint x)
        {
            if (x < (1u << 7))
            {
                return new byte[] { (byte)x };
            }
            else
            {
                if (x < (1 << 21))
                {
                    if (x < (1 << 14))
                    {
                        return new byte[] { (byte)(x | 0x80), (byte)(x >> 7) };
                    }
                    else
                    {
                        return new byte[] { (byte)(x | 0x80), (byte)((x >> 7) | 0x80), (byte)(x >> 14) };
                    }
                }
                else
                {
                    if (x < (1 << 28))
                    {
                        return new byte[] { (byte)(x | 0x80), (byte)((x >> 7) | 0x80), (byte)((x >> 14) | 0x80), (byte)(x >> 21) };
                    }
                    else
                    {
                        return new byte[] { (byte)(x | 0x80), (byte)((x >> 7) | 0x80), (byte)((x >> 14) | 0x80), (byte)((x >> 21) | 0x80), (byte)(x >> 28) };
                    }
                }
            }
        }

        public static byte[] GetBytes(ulong value)
        {
            var bytes = new List<byte>(2 * sizeof(long));
            while (value >= 0x80)
            {
                bytes.Add((byte)(value | 0x80));
                value >>= 7;
            }
            bytes.Add((byte)value);
            return bytes.ToArray();
        }

        public static ulong ReadUInt64(this BinaryReader reader)
        {
            checked
            {
                int shift = 0;
                ulong result = 0;
                byte next = reader.ReadByte();
                while ((next & 0x80) != 0)
                {
                    result |= (((ulong)next) & 0x7FL) << shift;
                    shift += 7;
                    next = reader.ReadByte();
                }
                result |= ((ulong)next) << shift;
                return result;
            }
        }

        public static int ToUInt64(byte[] buffer, int offset, out ulong result)
        {
            int index = offset;
            int shift = 0;
            result = 0;
            while ((index < buffer.Length) && ((buffer[index] & 0x80) != 0))
            {
                result |= (((ulong)buffer[index]) & 0x7FL) << shift;
                shift += 7;
                ++index;
            }
            if (index >= buffer.Length)
            {
                return 0;
            }
            result |= ((ulong)buffer[index++]) << shift;
            return index - offset;
        }
    }
}
