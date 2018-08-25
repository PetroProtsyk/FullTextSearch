using System;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core
{
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
            //return GetNumOfBytesSlow((uint)value);
        }

        private static int GetNumOfBytesSlow(uint value)
        {
            var r = 8;
            var v = (value >> 8);
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

        public static IList<byte> Encode(IList<int> input)
        {
            var result = new List<byte>();
            EncodeTo(input, result);
            return result;
        }

        public static void EncodeTo(IList<int> input, IList<byte> result)
        {
            var offset = 0;

            for (int i = 0; i < input.Count / 4; ++i)
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
            if (offset < input.Count)
            {
                int n1 = GetNumOfBytes(input[offset + 0]);
                int n2 = (offset + 1 < input.Count) ? GetNumOfBytes(input[offset + 1]) : 1;
                int n3 = (offset + 2 < input.Count) ? GetNumOfBytes(input[offset + 2]) : 1;
                int n4 = (offset + 3 < input.Count) ? GetNumOfBytes(input[offset + 3]) : 1;

                int selector = ((n1 - 1) << 6) | ((n2 - 1) << 4) | ((n3 - 1) << 2) | (n4 - 1);

                result.Add((byte)selector);

                WriteInt(input[offset + 0], n1, result);
                if (offset + 1 < input.Count) WriteInt(input[offset + 1], n2, result);
                if (offset + 2 < input.Count) WriteInt(input[offset + 2], n3, result);
                if (offset + 3 < input.Count) WriteInt(input[offset + 3], n4, result);
            }
        }

        public static IList<int> Decode(IList<byte> input)
        {
            var result = new List<int>();

            int index = 0;
            while (index < input.Count)
            {
                int selector = (int)input[index++];

                var selector4 = (selector & 0b11) + 1;
                var selector3 = ((selector >> 2) & 0b11) + 1;
                var selector2 = ((selector >> 4) & 0b11) + 1;
                var selector1 = ((selector >> 6) & 0b11) + 1;

                result.Add(ReadInt(input, index, selector1));
                index += selector1;
                if (index >= input.Count) break;

                result.Add(ReadInt(input, index, selector2));
                index += selector2;
                if (index >= input.Count) break;

                result.Add(ReadInt(input, index, selector3));
                index += selector3;
                if (index >= input.Count) break;

                result.Add(ReadInt(input, index, selector4));
                index += selector4;
            }

            return result.ToArray();
        }

        private static int ReadInt(IList<byte> input, int index, int selector)
        {
            if (selector > 2)
            {
                // 4 or 3

                if (selector == 4)
                {
                    return ((int)input[index] << 24) | ((int)input[index + 1] << 16) | ((int)input[index + 2] << 8) | (int)input[index + 3];
                }

                return ((int)input[index] << 16) | ((int)input[index + 1] << 8) | (int)input[index + 2];
            }
            else
            {
                // 2 or 1

                if (selector == 2)
                {
                    return ((int)input[index] << 8) | (int)input[index + 1];
                }

                return (int)input[index];
            }
        }

        private static void WriteInt(int value, int selector, IList<byte> result)
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

        public static string EncodeToBits(int[] input)
        {
            var code = Encode(input);
            var result = new StringBuilder();
            for (int i = 0; i < code.Count; ++i)
            {
                result.Append(Convert.ToString(code[i], 2).PadLeft(8, '0'));
                if (i != code.Count - 1)
                {
                    result.Append(" ");
                }
            }
            return result.ToString();
        }
    }
}
