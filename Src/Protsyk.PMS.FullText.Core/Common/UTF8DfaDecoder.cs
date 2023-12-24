using System.Text;

namespace Protsyk.PMS.FullText.Core.Common;

public class UTF8DfaDecoder
{
    // Type 1 U+0000  U+007F    0xxxxxxx
    // Type 3 U+0080  U+07FF    110xxxxx  10xxxxxx
    // Type 4 U+0800  U+FFFF    1110xxxx  10xxxxxx    10xxxxxx
    // Type 5 U+10000 U+10FFFF  11110xxx  10xxxxxx    10xxxxxx    10xxxxxx
    //
    // Type 0                   Invalid UTF-8 byte
    // Type 2                   10xxxxxx
    private static ReadOnlySpan<int> ByteClass =>
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        5, 5, 5, 5, 5, 5, 5, 5, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    // State ID = k * 6, where k = 0, 1, 2, 3
    // 6 - number of transitions corresponding to 6 byte types
    private static ReadOnlySpan<int> states => [
        -1,  0, -1,  6, 12, 18, // state 0
        -1, -1,  0, -1, -1, -1, // state 1 * 6
        -1, -1,  6, -1, -1, -1, // state 2 * 6
        -1, -1, 12, -1, -1, -1, // state 3 * 6
    ];

    static UTF8DfaDecoder()
    {
        VerifyByteClass();
    }

    private static void VerifyByteClass()
    {
        for (int i = 0; i < 256; ++i)
        {
            if (((i & 0b1_0000000) ^ 0b0_0000000) == 0)
            {
                if (ByteClass[i] != 1)
                {
                    throw new Exception("Something wrong");
                }
            }

            if (((i & 0b11_000000) ^ 0b10_000000) == 0)
            {
                if (ByteClass[i] != 2)
                {
                    throw new Exception("Something wrong");
                }
            }

            if (((i & 0b111_00000) ^ 0b110_00000) == 0)
            {
                if (ByteClass[i] != 3)
                {
                    throw new Exception("Something wrong");
                }
            }

            if (((i & 0b1111_0000) ^ 0b1110_0000) == 0)
            {
                if (ByteClass[i] != 4)
                {
                    throw new Exception("Something wrong");
                }
            }

            if (((i & 0b11111_000) ^ 0b11110_000) == 0)
            {
                if (ByteClass[i] != 5)
                {
                    throw new Exception("Something wrong");
                }
            }
        }
    }

    private static int Decode(int c, int state, ref int code)
    {
        var type = ByteClass[c];
        if (state == 0)
        {
            code = (0xff >> type) & c;
        }
        else
        {
            code = (code << 6) | (c & 0x3f);
        }
        return states[state + type];
    }

    public static string Decode(byte[] input)
    {
        var result = new StringBuilder();
        var state = 0;
        var code = 0;
        for (int i = 0; i < input.Length; ++i)
        {
            state = Decode(input[i], state, ref code);
            if (state == 0)
            {
                result.Append((char)code);
            }
            else if (state < 0)
            {
                throw new Exception("Not UTF-8");
            }
        }

        if (state != 0)
        {
            throw new Exception("Not UTF-8");
        }

        return result.ToString();
    }
}
