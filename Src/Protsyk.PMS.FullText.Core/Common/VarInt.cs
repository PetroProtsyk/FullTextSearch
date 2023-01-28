namespace Protsyk.PMS.FullText.Core.Common;

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

    public static int WriteVUInt64(ulong value, Span<byte> buffer)
    {
        var index = 0;
        while (value > 0x7F)
        {
            buffer[index++] = (byte)(value | 0x80);
            value >>= 7;
        }
        buffer[index++] = (byte)value;
        return index;
    }

    public static int WriteVInt64(long value, Span<byte> buffer)
    {
        return WriteVUInt64((ulong)value, buffer);
    }

    public static int WriteVUInt32(uint value, Span<byte> buffer)
    {
        var index = 0;
        while (value > 0x7F)
        {
            buffer[index++] = (byte)(value | 0x80);
            value >>= 7;
        }
        buffer[index++] = (byte)value;
        return index;
    }

    public static int WriteVInt32(int value, Span<byte> buffer)
    {
        return WriteVUInt32((uint)value, buffer);
    }

    public static int ReadVUInt64(ReadOnlySpan<byte> buffer, out ulong result)
    {
        int index = 0;
        int shift = 0;
        result = 0;
        while ((buffer[index] & 0x80) != 0)
        {
            result |= (((ulong)buffer[index++]) & 0x7FL) << shift;
            shift += 7;
        }
        result |= ((ulong)buffer[index++]) << shift;
        return index;
    }

    public static int ReadVInt64(ReadOnlySpan<byte> buffer, out long result)
    {
        var count = ReadVUInt64(buffer, out var value);
        result = (long)value;
        return count;
    }

    public static int ReadVUInt32(ReadOnlySpan<byte> buffer, out uint result)
    {
        var index = 0;
        var shift = 0;
        result = 0;
        while ((buffer[index] & 0x80) > 0)
        {
            result |= (uint)((buffer[index++] & 0x7F) << shift);
            shift += 7;
        }
        result |= (uint)(buffer[index++] << shift);
        return index;
    }

    public static int ReadVInt32(ReadOnlySpan<byte> buffer, out int result)
    {
        var count = ReadVUInt32(buffer, out var value);
        result = (int)value;
        return count;
    }
}
