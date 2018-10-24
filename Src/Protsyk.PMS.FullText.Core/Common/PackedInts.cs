using System;
using System.Runtime.CompilerServices;

namespace PMS.Common.Encodings
{
    public static class PackedInts
    {
        public static int GetBitSize(int value)
        {
            var v = (uint)value;
            v >>= 1;
            var size = 1;
            while (v != 0)
            {
                v >>= 1;
                ++size;
            }
            return size;
        }

        public static int GetBitSize(long value)
        {
            var v = (ulong)value;
            v >>= 1;
            var size = 1;
            while (v != 0)
            {
                v >>= 1;
                ++size;
            }
            return size;
        }

        public static IPackedInts Get(int bits, int count)
        {
            if (bits < 8)
                return new PackedIntN8(bits, count);

            if (bits == 8)
                return new PackedInt8(count);

            if (bits < 16)
                return new PackedIntN16(bits, count);

            if (bits == 16)
                return new PackedInt16(count);

            if (bits < 32)
                return new PackedIntN32(bits, count);

            if (bits == 32)
                return new PackedInt32(count);

            if (bits < 64)
                return new PackedIntN64(bits, count);

            if (bits == 64)
                return new PackedInt64(count);

            throw new NotSupportedException();
        }

        public static IPackedInts Load(byte[] bytes)
        {
            var bits = (int)bytes[0];
            var count = (int)bytes[1] | ((int)bytes[2] << 8) | ((int)bytes[3] << 16) | ((int)bytes[4] << 24);

            if (bits <= 8)
            {
                var data = new byte[(count * bits + 7) / 8];
                for (int i = 0; i < data.Length; ++i)
                {
                    data[i] = (byte)bytes[5 + i];
                }

                return bits == 8 ? (IPackedInts)new PackedInt8(count, data) : new PackedIntN8(bits, count, data);
            }

            if (bits <= 16)
            {
                var data = new ushort[(count * bits + 15) / 16];
                for (int i = 0; i < data.Length; ++i)
                {
                    var r = (uint)bytes[5 + 2 * i];
                    if (5 + 2 * i + 1 < bytes.Length)
                    {
                        r |= (uint)(bytes[5 + 2 * i + 1] << 8);
                    }
                    //if (i == data.Length - 1)
                    //{
                    //    r <<= 16 - (count * bits) % 16;
                    //}
                    data[i] = (ushort)r;
                }
                return bits == 16 ? (IPackedInts)new PackedInt16(count, data) : new PackedIntN16(bits, count, data);
            }

            if (bits <= 32)
            {
                var data = new uint[(count * bits + 31) / 32];
                for (int i = 0; i < data.Length; i++)
                {
                    var r = (uint)bytes[5 + 4 * i];
                    if (5 + 4 * i + 1 < bytes.Length)
                    {
                        r |= (uint)bytes[5 + 4 * i + 1] << 8;
                    }
                    if (5 + 4 * i + 2 < bytes.Length)
                    {
                        r |= (uint)bytes[5 + 4 * i + 2] << 16;
                    }
                    if (5 + 4 * i + 3 < bytes.Length)
                    {
                        r |= (uint)bytes[5 + 4 * i + 3] << 24;
                    }
                    if (i == data.Length - 1)
                    {
                        r <<= 32 - (count * bits) % 32;
                    }
                    data[i] = (uint)r;
                }
                return bits == 32 ? (IPackedInts)new PackedInt32(count, data) : new PackedIntN32(bits, count, data);
            }

            if (bits <= 64)
            {
                var data = new ulong[(count * bits + 63) / 64];
                for (int i = 0; i < data.Length; i++)
                {
                    var r = (ulong)bytes[5 + 8 * i];
                    if (5 + 8 * i + 1 < bytes.Length)
                    {
                        r |= (ulong)bytes[5 + 8 * i + 1] << 8;
                    }
                    if (5 + 8 * i + 2 < bytes.Length)
                    {
                        r |= (ulong)bytes[5 + 8 * i + 2] << 16;
                    }
                    if (5 + 8 * i + 3 < bytes.Length)
                    {
                        r |= (ulong)bytes[5 + 8 * i + 3] << 24;
                    }
                    if (5 + 8 * i + 4 < bytes.Length)
                    {
                        r |= (ulong)bytes[5 + 8 * i + 4] << 32;
                    }
                    if (5 + 8 * i + 5 < bytes.Length)
                    {
                        r |= (ulong)bytes[5 + 8 * i + 5] << 40;
                    }
                    if (5 + 8 * i + 6 < bytes.Length)
                    {
                        r |= (ulong)bytes[5 + 8 * i + 6] << 48;
                    }
                    if (5 + 8 * i + 7 < bytes.Length)
                    {
                        r |= (ulong)bytes[5 + 8 * i + 7] << 56;
                    }
                    if (i == data.Length - 1)
                    {
                        r <<= 64 - (count * bits) % 64;
                    }
                    data[i] = r;
                }
                return bits == 64 ? (IPackedInts)new PackedInt64(count, data) : new PackedIntN64(bits, count, data);
            }

            throw new NotSupportedException();
        }

        public static IPackedInts Convert(int[] values)
        {
            var size = 1;
            for (int i = 0; i < values.Length; ++i)
            {
                size = Math.Max(size, GetBitSize(values[i]));
            }

            var result = Get(size, values.Length);
            for (int i = 0; i < values.Length; ++i)
            {
                result.Set(i, values[i]);
            }

            return result;
        }

        public static IPackedInts Convert(long[] values)
        {
            var size = 1;
            for (int i = 0; i < values.Length; ++i)
            {
                size = Math.Max(size, GetBitSize(values[i]));
            }

            var result = Get(size, values.Length);
            for (int i = 0; i < values.Length; ++i)
            {
                result.SetLong(i, values[i]);
            }

            return result;
        }
    }

    public interface IPackedInts : IPackedLongs
    {
        int BitSize { get; }

        int Length { get; }

        int Get(int index);

        void Set(int index, int value);

        byte[] GetBytes();
    }

    public interface IPackedLongs
    {
        long GetLong(int index);

        void SetLong(int index, long value);
    }

    public class PackedIntN8 : IPackedInts
    {
        private const int BitsInBaseType = 8;
        private const int LastBitIndexInBaseType = 7;

        private static readonly uint[] mask = new uint[]{
        0b0000_0000,
        0b0000_0001,
        0b0000_0011,
        0b0000_0111,
        0b0000_1111,
        0b0001_1111,
        0b0011_1111,
        0b0111_1111,
        0b1111_1111,
    };

        private readonly byte[] data;
        private readonly int bits;
        private readonly int count;

        public PackedIntN8(int bits, int count)
            : this(bits, count, new byte[(count * bits + LastBitIndexInBaseType) / BitsInBaseType])
        {
        }

        public PackedIntN8(int bits, int count, byte[] data)
        {
            this.data = data;
            this.bits = bits;
            this.count = count;
        }

        public int BitSize => bits;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            var i = (index * bits) / BitsInBaseType;
            var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            if (b >= bits)
            {
                return (int)(data[i] & mask[b]) >> (b - bits);
            }
            else
            {
                var r1 = ((uint)data[i] & mask[b]) << (bits - b);
                var r2 = ((uint)data[i + 1] >> (BitsInBaseType - bits + b));
                return (int)(r1 | r2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            var i = (index * bits) / BitsInBaseType;
            var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            if (b >= bits)
            {
                data[i] |= (byte)(value << (b - bits));
            }
            else
            {
                data[i] |= (byte)(value >> (bits - b));
                data[i + 1] |= (byte)(value << (BitsInBaseType - bits + b));
            }
        }

        public long GetLong(int index)
        {
            return Get(index);
        }

        public void SetLong(int index, long value)
        {
            checked
            {
                Set(index, (int)value);
            }
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + (7 + count * bits) / 8];
            r[0] = (byte)bits;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length && k < r.Length; ++i)
            {
                r[k++] = data[i];
            }
            return r;
        }
    }

    public class PackedInt8 : IPackedInts
    {
        private readonly byte[] data;

        private readonly int count;

        public PackedInt8(int count)
            : this(count, new byte[count])
        {
        }

        public PackedInt8(int count, byte[] data)
        {
            this.data = data;
            this.count = count;
        }

        public int BitSize => 8;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            return data[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            checked
            {
                data[index] = (byte)value;
            }
        }

        public long GetLong(int index)
        {
            return Get(index);
        }

        public void SetLong(int index, long value)
        {
            checked
            {
                Set(index, (int)value);
            }
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + data.Length];
            r[0] = 8;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length; ++i)
            {
                r[k++] = data[i];
            }
            return r;
        }
    }

    public class PackedIntN16 : IPackedInts
    {
        private const int BitsInBaseType = 16;
        private const int LastBitIndexInBaseType = 15;

        private static readonly uint[] mask = new uint[]{
        0b0000_0000_0000_0000,
        0b0000_0000_0000_0001,
        0b0000_0000_0000_0011,
        0b0000_0000_0000_0111,
        0b0000_0000_0000_1111,
        0b0000_0000_0001_1111,
        0b0000_0000_0011_1111,
        0b0000_0000_0111_1111,
        0b0000_0000_1111_1111,
        0b0000_0001_1111_1111,
        0b0000_0011_1111_1111,
        0b0000_0111_1111_1111,
        0b0000_1111_1111_1111,
        0b0001_1111_1111_1111,
        0b0011_1111_1111_1111,
        0b0111_1111_1111_1111,
        0b1111_1111_1111_1111,
    };

        private readonly ushort[] data;
        private readonly int bits;
        private readonly int count;

        public PackedIntN16(int bits, int count)
            : this(bits, count, new ushort[(count * bits + LastBitIndexInBaseType) / BitsInBaseType])
        {
        }

        public PackedIntN16(int bits, int count, ushort[] data)
        {
            this.data = data;
            this.bits = bits;
            this.count = count;
        }

        public int BitSize => bits;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            var i = (index * bits) / BitsInBaseType;
            var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            if (b >= bits)
            {
                return (int)((data[i] & mask[b]) >> (b - bits));
            }
            else
            {
                var r1 = ((uint)data[i] & mask[b]) << (bits - b);
                var r2 = ((uint)data[i + 1] >> (BitsInBaseType - bits + b));
                return (int)(r1 | r2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            var i = (index * bits) / BitsInBaseType;
            var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            if (b >= bits)
            {
                data[i] |= (ushort)(value << (b - bits));
            }
            else
            {
                data[i] |= (ushort)(value >> (bits - b));
                data[i + 1] |= (ushort)(value << (BitsInBaseType - bits + b));
            }
        }

        public long GetLong(int index)
        {
            return Get(index);
        }

        public void SetLong(int index, long value)
        {
            checked
            {
                Set(index, (int)value);
            }
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + (7 + count * bits) / 8];
            r[0] = (byte)bits;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length && k < r.Length; ++i)
            {
                var v = data[i];
                //if (i == data.Length - 1)
                //{
                //    v >>= BitsInBaseType - (count * bits) % BitsInBaseType;
                //}

                for (int j = 0; j < BitsInBaseType && k < r.Length; j += 8)
                {
                    r[k++] = (byte)v;
                    v >>= 8;
                }
            }
            return r;
        }
    }

    public class PackedInt16 : IPackedInts
    {
        private readonly ushort[] data;
        private readonly int count;

        public PackedInt16(int count)
            : this(count, new ushort[count])
        {
        }

        public PackedInt16(int count, ushort[] data)
        {
            this.data = data;
            this.count = count;
        }

        public int BitSize => 16;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            return data[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            checked
            {
                data[index] = (ushort)value;
            }
        }

        public long GetLong(int index)
        {
            return Get(index);
        }

        public void SetLong(int index, long value)
        {
            checked
            {
                Set(index, (int)value);
            }
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + 2 * data.Length];
            r[0] = 16;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length; ++i)
            {
                var v = data[i];
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
            }
            return r;
        }
    }

    public class PackedIntN32 : IPackedInts
    {
        private const int BitsInBaseType = 32;
        private const int LastBitIndexInBaseType = 31;

        private static readonly uint[] mask = new uint[]{
        0b0000_0000_0000_0000_0000_0000_0000_0000,
        0b0000_0000_0000_0000_0000_0000_0000_0001,
        0b0000_0000_0000_0000_0000_0000_0000_0011,
        0b0000_0000_0000_0000_0000_0000_0000_0111,
        0b0000_0000_0000_0000_0000_0000_0000_1111,
        0b0000_0000_0000_0000_0000_0000_0001_1111,
        0b0000_0000_0000_0000_0000_0000_0011_1111,
        0b0000_0000_0000_0000_0000_0000_0111_1111,
        0b0000_0000_0000_0000_0000_0000_1111_1111,
        0b0000_0000_0000_0000_0000_0001_1111_1111,
        0b0000_0000_0000_0000_0000_0011_1111_1111,
        0b0000_0000_0000_0000_0000_0111_1111_1111,
        0b0000_0000_0000_0000_0000_1111_1111_1111,
        0b0000_0000_0000_0000_0001_1111_1111_1111,
        0b0000_0000_0000_0000_0011_1111_1111_1111,
        0b0000_0000_0000_0000_0111_1111_1111_1111,
        0b0000_0000_0000_0000_1111_1111_1111_1111,
        0b0000_0000_0000_0001_1111_1111_1111_1111,
        0b0000_0000_0000_0011_1111_1111_1111_1111,
        0b0000_0000_0000_0111_1111_1111_1111_1111,
        0b0000_0000_0000_1111_1111_1111_1111_1111,
        0b0000_0000_0001_1111_1111_1111_1111_1111,
        0b0000_0000_0011_1111_1111_1111_1111_1111,
        0b0000_0000_0111_1111_1111_1111_1111_1111,
        0b0000_0000_1111_1111_1111_1111_1111_1111,
        0b0000_0001_1111_1111_1111_1111_1111_1111,
        0b0000_0011_1111_1111_1111_1111_1111_1111,
        0b0000_0111_1111_1111_1111_1111_1111_1111,
        0b0000_1111_1111_1111_1111_1111_1111_1111,
        0b0001_1111_1111_1111_1111_1111_1111_1111,
        0b0011_1111_1111_1111_1111_1111_1111_1111,
        0b0111_1111_1111_1111_1111_1111_1111_1111,
        0b1111_1111_1111_1111_1111_1111_1111_1111,
    };

        private readonly uint[] data;
        private readonly int bits;
        private readonly int count;

        public PackedIntN32(int bits, int count)
            : this(bits, count, new uint[(count * bits + LastBitIndexInBaseType) / BitsInBaseType])
        {
        }

        public PackedIntN32(int bits, int count, uint[] data)
        {
            this.data = data;
            this.bits = bits;
            this.count = count;
        }

        public int BitSize => bits;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            // var i = (index * bits) / BitsInBaseType;
            // var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            var i = Math.DivRem(index * bits, BitsInBaseType, out int rem);
            var b = BitsInBaseType - rem;
            if (b >= bits)
            {
                return (int)((data[i] & mask[b]) >> (b - bits));
            }
            else
            {
                var r1 = (data[i] & mask[b]) << (bits - b);
                var r2 = (data[i + 1] >> (BitsInBaseType - bits + b));
                return (int)(r1 | r2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            // var i = (index * bits) / BitsInBaseType;
            // var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            var i = Math.DivRem(index * bits, BitsInBaseType, out int rem);
            var b = BitsInBaseType - rem;
            if (b >= bits)
            {
                data[i] |= (uint)(value << (b - bits));
            }
            else
            {
                data[i] |= (uint)(value >> (bits - b));
                data[i + 1] |= (uint)(value << (BitsInBaseType - bits + b));
            }
        }

        public long GetLong(int index)
        {
            return Get(index);
        }

        public void SetLong(int index, long value)
        {
            checked
            {
                Set(index, (int)value);
            }
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + (7 + count * bits) / 8];
            r[0] = (byte)bits;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length && k < r.Length; ++i)
            {
                var v = data[i];
                if (i == data.Length - 1)
                {
                    v >>= BitsInBaseType - (count * bits) % BitsInBaseType;
                }

                for (int j = 0; j < BitsInBaseType && k < r.Length; j += 8)
                {
                    r[k++] = (byte)v;
                    v >>= 8;
                }
            }
            return r;
        }
    }

    public class PackedInt32 : IPackedInts
    {
        private readonly uint[] data;
        private readonly int count;

        public PackedInt32(int count)
            : this(count, new uint[count])
        {
        }

        public PackedInt32(int count, uint[] data)
        {
            this.data = data;
            this.count = count;
        }

        public int BitSize => 32;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            return (int)data[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            data[index] = (uint)value;
        }

        public long GetLong(int index)
        {
            return Get(index);
        }

        public void SetLong(int index, long value)
        {
            checked
            {
                Set(index, (int)value);
            }
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + 4 * data.Length];
            r[0] = (byte)32;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length; ++i)
            {
                var v = data[i];
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
            }
            return r;
        }
    }

    public class PackedIntN64 : IPackedInts, IPackedLongs
    {
        private const int BitsInBaseType = 64;
        private const int LastBitIndexInBaseType = 63;

        private static readonly ulong[] mask = new ulong[]{
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0011,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0011_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0011_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0011_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0011_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0011_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0001_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0011_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0000_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0001_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0011_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_0111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0000_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0001_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0011_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_0111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0000_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0001_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0011_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_0111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0001_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0011_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_0111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0001_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0011_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_0111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0001_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0011_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_0111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0001_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0011_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_0111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0001_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0011_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_0111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0000_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0001_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0011_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b0111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
        0b1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111_1111,
    };

        private readonly ulong[] data;
        private readonly int bits;
        private readonly int count;
        public PackedIntN64(int bits, int count)
            : this(bits, count, new ulong[(count * bits + LastBitIndexInBaseType) / BitsInBaseType])
        {
        }

        public PackedIntN64(int bits, int count, ulong[] data)
        {
            this.data = data;
            this.bits = bits;
            this.count = count;
        }

        public int BitSize => bits;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            checked
            {
                return (int)GetLong(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            SetLong(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetLong(int index)
        {
            var i = (index * bits) / BitsInBaseType;
            var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            if (b >= bits)
            {
                return (long)((data[i] & mask[b]) >> (b - bits));
            }
            else
            {
                var r1 = ((ulong)data[i] & mask[b]) << (bits - b);
                var r2 = ((ulong)data[i + 1] >> (BitsInBaseType - bits + b));
                return (long)(r1 | r2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLong(int index, long value)
        {
            var i = (index * bits) / BitsInBaseType;
            var b = BitsInBaseType - ((index * bits) % BitsInBaseType);
            if (b >= bits)
            {
                data[i] |= (ulong)(value << (b - bits));
            }
            else
            {
                data[i] |= (ulong)(value >> (bits - b));
                data[i + 1] |= (ulong)(value << (BitsInBaseType - bits + b));
            }
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + (7 + count * bits) / 8];
            r[0] = (byte)bits;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length && k < r.Length; ++i)
            {
                var v = data[i];
                if (i == data.Length - 1)
                {
                    v >>= BitsInBaseType - (count * bits) % BitsInBaseType;
                }

                for (int j = 0; j < BitsInBaseType && k < r.Length; j += 8)
                {
                    r[k++] = (byte)v;
                    v >>= 8;
                }
            }
            return r;
        }
    }

    public class PackedInt64 : IPackedInts, IPackedLongs
    {
        private readonly ulong[] data;
        private readonly int count;

        public PackedInt64(int count)
            : this(count, new ulong[count])
        {
        }

        public PackedInt64(int count, ulong[] data)
        {
            this.data = data;
            this.count = count;
        }

        public int BitSize => 64;

        public int Length => count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            checked
            {
                return (int)data[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int value)
        {
            data[index] = (ulong)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLong(int index, long value)
        {
            data[index] = (ulong)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetLong(int index)
        {
            return (long)data[index];
        }

        public byte[] GetBytes()
        {
            var r = new byte[1 + 4 + 8 * data.Length];
            r[0] = (byte)64;
            r[1] = (byte)count;
            r[2] = (byte)(count >> 8);
            r[3] = (byte)(count >> 16);
            r[4] = (byte)(count >> 24);
            var k = 5;
            for (int i = 0; i < data.Length; ++i)
            {
                var v = data[i];
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
                v >>= 8;
                r[k++] = (byte)v;
            }
            return r;
        }
    }
}


