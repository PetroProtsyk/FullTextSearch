using System.Runtime.InteropServices;
using System.Text;

namespace Protsyk.PMS.FullText.Core;

/// <summary>
/// An implementation of Group VarInt encoding as described here:
///  - http://www.ir.uwaterloo.ca/book/addenda-06-index-compression.html
///  - J.Dean in "Challenges in Building Large-Scale Information Retrieval Systems" at WSDM'09
/// </summary>
public class GroupVarint
{
    static GroupVarint()
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new NotSupportedException("This code only works on Little Endian architectures");
        }
    }

    private static int GetNumOfBytes(int value)
    {
        return GetNumOfBytesFast(value);
    }

    private static int GetNumOfBytesSlow(int value)
    {
        var r = 8;
        var v = (uint)(value >> 8);
        while (v > 0)
        {
            v >>= 8;
            r += 8;
        }

        return (r + 7) / 8;
    }

    private static int GetNumOfBytesFast(int value)
    {
        return (value > 0xFFFFFF || value < 0) ? 4 : (value < 0x10000) ? (value < 0x100) ? 1 : 2 : 3;
    }

    public static int GetMaxEncodedSize(int count)
    {
        return 17 * (count + 3) / 4;
    }

    public static int Encode(ReadOnlySpan<int> source, Span<byte> buffer)
    {
        if (GetMaxEncodedSize(source.Length) > buffer.Length)
        {
            throw new Exception($"Make sure {nameof(buffer)} is big enough");
        }

        int count = source.Length;
        var encodedSize = 0;
        var encodedCount = 0;
        for (int i = 0; i < count / 4; ++i)
        {
            int chunkSize = Encode(source[0],
                                   source[1],
                                   source[2],
                                   source[3],
                                   buffer);

            buffer = buffer[chunkSize..];
            source = source[4..];

            encodedSize += chunkSize;
            encodedCount += 4;
        }

        // Encode remainder
        if (encodedCount < count)
        {
            int n1 = source[0];
            int n2 = (encodedCount + 1 < count) ? source[1] : 0;
            int n3 = (encodedCount + 2 < count) ? source[2] : 0;
            int n4 = (encodedCount + 3 < count) ? source[3] : 0;

            int chunkSize = Encode(n1, n2, n3, n4, buffer);

            encodedSize += chunkSize - (4 - (count - encodedCount));
        }

        return encodedSize;
    }

    public static int Encode(int n1, int n2, int n3, int n4, Span<byte> buffer)
    {
        int i = 0;
        int s1 = GetNumOfBytes(n1);
        int s2 = GetNumOfBytes(n2);
        int s3 = GetNumOfBytes(n3);
        int s4 = GetNumOfBytes(n4);

        int selector = ((s1 - 1) << 6) | ((s2 - 1) << 4) | ((s3 - 1) << 2) | (s4 - 1);

        buffer[i++] = (byte)selector;

        WriteInt(n1, s1, buffer[i..]);
        i += s1;
        WriteInt(n2, s2, buffer[i..]);
        i += s2;
        WriteInt(n3, s3, buffer[i..]);
        i += s3;
        WriteInt(n4, s4, buffer[i..]);
        i += s4;

        return i;
    }

    public static List<byte> Encode(List<int> input)
    {
        return Encode(CollectionsMarshal.AsSpan(input));
    }

    public static List<byte> Encode(ReadOnlySpan<int> input)
    {
        var result = new List<byte>();
        EncodeTo(input, result);
        return result;
    }

    public static void EncodeTo(ReadOnlySpan<int> input, List<byte> result)
    {
        var offset = 0;

        for (int i = 0; i < input.Length / 4; ++i)
        {
            int n1 = GetNumOfBytes(input[offset + 0]);
            int n2 = GetNumOfBytes(input[offset + 1]);
            int n3 = GetNumOfBytes(input[offset + 2]);
            int n4 = GetNumOfBytes(input[offset + 3]);

            int selector = ((n1 - 1) << 6) | ((n2 - 1) << 4) | ((n3 - 1) << 2) | (n4 - 1);

            result.Add((byte)selector);

            WriteInt(input[offset + 0], n1, result);
            WriteInt(input[offset + 1], n2, result);
            WriteInt(input[offset + 2], n3, result);
            WriteInt(input[offset + 3], n4, result);
            offset += 4;
        }

        // Encode remainder
        if (offset < input.Length)
        {
            int n1 = GetNumOfBytes(input[offset + 0]);
            int n2 = (offset + 1 < input.Length) ? GetNumOfBytes(input[offset + 1]) : 1;
            int n3 = (offset + 2 < input.Length) ? GetNumOfBytes(input[offset + 2]) : 1;
            int n4 = (offset + 3 < input.Length) ? GetNumOfBytes(input[offset + 3]) : 1;

            int selector = ((n1 - 1) << 6) | ((n2 - 1) << 4) | ((n3 - 1) << 2) | (n4 - 1);

            result.Add((byte)selector);

            WriteInt(input[offset + 0], n1, result);
            if (offset + 1 < input.Length) WriteInt(input[offset + 1], n2, result);
            if (offset + 2 < input.Length) WriteInt(input[offset + 2], n3, result);
            if (offset + 3 < input.Length) WriteInt(input[offset + 3], n4, result);
        }
    }

    public static List<int> Decode(List<byte> input)
    {
        return Decode(CollectionsMarshal.AsSpan(input));
    }

    public static List<int> Decode(ReadOnlySpan<byte> input)
    {
        var result = new List<int>();

        int index = 0;
        while (index < input.Length)
        {
            int selector = (int)input[index++];

            var selector4 = (selector & 0b11) + 1;
            var selector3 = ((selector >> 2) & 0b11) + 1;
            var selector2 = ((selector >> 4) & 0b11) + 1;
            var selector1 = ((selector >> 6) & 0b11) + 1;

            result.Add(ReadInt(input.Slice(index), selector1));
            index += selector1;
            if (index >= input.Length) break;

            result.Add(ReadInt(input.Slice(index), selector2));
            index += selector2;
            if (index >= input.Length) break;

            result.Add(ReadInt(input.Slice(index), selector3));
            index += selector3;
            if (index >= input.Length) break;

            result.Add(ReadInt(input.Slice(index), selector4));
            index += selector4;
        }

        return result;
    }

    public static int ReadInt(ReadOnlySpan<byte> input, int selector)
    {
        if (selector > 2)
        {
            // 4 or 3

            if (selector == 4)
            {
                return ((int)input[0] << 24) | ((int)input[1] << 16) | ((int)input[2] << 8) | (int)input[3];
            }

            return ((int)input[0] << 16) | ((int)input[1] << 8) | (int)input[2];
        }
        else
        {
            // 2 or 1

            if (selector == 2)
            {
                return ((int)input[0] << 8) | (int)input[1];
            }

            return (int)input[0];
        }
    }

    private static void WriteInt(int value, int selector, Span<byte> result)
    {
        int i = 0;

        if (selector > 2)
        {
            // 4 or 3

            if (selector == 4)
            {
                result[i++] = (byte)(value >> 24);
            }

            result[i++] = (byte)(value >> 16);
            result[i++] = (byte)(value >> 8);
            result[i++] = (byte)(value);
        }
        else
        {
            // 2 or 1

            if (selector == 2)
            {
                result[i++] = (byte)(value >> 8);
            }

            result[i++] = (byte)(value);
        }
    }


    private static void WriteInt(int value, int selector, List<byte> result)
    {
        if (selector > 2)
        {
            // 4 or 3

            if (selector == 4)
            {
                result.Add((byte)(value >> 24));
            }

            result.Add((byte)(value >> 16));
            result.Add((byte)(value >> 8));
            result.Add((byte)(value));
        }
        else
        {
            // 2 or 1

            if (selector == 2)
            {
                result.Add((byte)(value >> 8));
            }

            result.Add((byte)(value));
        }
    }

    public static string EncodeToBits(ReadOnlySpan<int> input)
    {
        var code = Encode(input);
        var result = new StringBuilder();
        for (int i = 0; i < code.Count; ++i)
        {
            result.Append(Convert.ToString(code[i], 2).PadLeft(8, '0'));
            if (i != code.Count - 1)
            {
                result.Append(' ');
            }
        }
        return result.ToString();
    }
}
