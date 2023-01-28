namespace Protsyk.PMS.FullText.Core.Common.Compression;

//
// http://www.ietf.org/rfc/rfc1978.txt
//
public class PredictorCompressionProtocol
{
    private const int guessSize = 2048;

    public static byte[] Compress(byte[] input)
    {
        var guessTable = new byte[guessSize];
        uint flags = 0;
        uint bitmask = 1;
        uint hash = 0;
        var result = new List<byte>();
        int flagsIndex = result.Count;
        result.Add(0);
        for (int i = 0; i < input.Length; ++i)
        {
            var c = (byte)(input[i] ^ 0xEA);
            if (guessTable[hash] == c)
            {
                flags |= bitmask;
            }
            else
            {
                guessTable[hash] = c;
                result.Add(c);
            }
            hash = ((hash << 7) ^ c) % guessSize;
            bitmask <<= 1;

            if (bitmask == 256)
            {
                result[flagsIndex] = (byte)flags;
                flags = 0;
                bitmask = 1;

                flagsIndex = result.Count;
                result.Add(0);
            }
        }
        result[flagsIndex] = (byte)flags;
        return result.ToArray();
    }

    public static byte[] Uncompress(byte[] input)
    {
        var guessTable = new byte[guessSize];
        uint hash = 0;

        var result = new List<byte>();
        uint flags = input[0];
        var i = 1;
        var j = 0;
        while ((flags != 0) || (i < input.Length))
        {
            byte c;
            if ((flags & 1) > 0)
            {
                c = guessTable[hash];
            }
            else
            {
                c = input[i];
                guessTable[hash] = c;
                ++i;
            }

            result.Add((byte)(c ^ 0xEA));
            hash = ((hash << 7) ^ c) % guessSize;
            flags >>= 1;
            ++j;

            if (j == 8)
            {
                j = 0;
                flags = input[i];
                ++i;
            }
        }
        return result.ToArray();
    }
}