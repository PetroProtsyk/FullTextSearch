using System;
using System.Buffers.Binary;

namespace Protsyk.PMS.FullText.Core.Common;

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

    public static int ReadInt(ReadOnlySpan<byte> buffer, out int value)
    {
        value = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        return sizeof(int);
    }

    public static int ReadUInt(ReadOnlySpan<byte> buffer, out uint value)
    {
        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        return sizeof(uint);
    }

    public static int WriteInt(int value, Span<byte> buffer)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);

        return 4;
    }

    public static int WriteUInt(uint value, Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);

        return 4;
    }
}