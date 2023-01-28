using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Compression;

public interface IEncodingNode
{
}

public interface IEncodingLeafNode : IEncodingNode
{
    char V { get; }
}

public interface IEncodingTreeNode : IEncodingNode
{
    IEncodingNode Left { get; }
    IEncodingNode Right { get; }
}

public class VarLenCharEncoding
{
    private const byte LeftValue = 0;
    private const byte RightValue = 1;

    private readonly IEncodingNode root;
    private readonly Dictionary<char, byte[]> codes;
    private readonly List<DecodeSymbol> decodingTable;
    public const char TerminalChar = '\0';

    internal VarLenCharEncoding(IEncodingNode root)
    {
        this.root = root;
        this.codes = new Dictionary<char, byte[]>();
        Traverse(root, codes, new List<byte>());
        this.decodingTable = BuildDecodingTable();
    }

    private void Traverse(IEncodingNode root, Dictionary<char, byte[]> codes, List<byte> current)
    {
        var leaf = root as IEncodingLeafNode;
        if (leaf != null)
        {
            codes[leaf.V] = current.ToArray();
            return;
        }

        var node = root as IEncodingTreeNode;
        if (node != null)
        {
            current.Add(LeftValue);
            Traverse(node.Left, codes, current);
            current.RemoveAt(current.Count - 1);

            current.Add(RightValue);
            Traverse(node.Right, codes, current);
            current.RemoveAt(current.Count - 1);
            return;
        }

        throw new InvalidOperationException();
    }

    private static string RenderChar(char c)
    {
        var category = char.GetUnicodeCategory(c);

        if (char.IsWhiteSpace(c) ||
            category is UnicodeCategory.Control or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Surrogate)
        {
            return $"U+{(int)c:X4}";
        }

        return $"{c}";
    }

    public override string ToString()
    {
        var result = new StringBuilder();
        var codes = new Dictionary<char, byte[]>();
        Traverse(root, codes, new List<byte>());

        foreach (var code in codes.Keys.OrderBy(x => x))
        {
            result.AppendLine($"{RenderChar(code)} = { string.Join("", codes[code].Select(b => $"{b}"))}");
        }

        return result.ToString();
    }

    // Build decoding table for prefix codes (Huffman, HuTucker, etc)
    // No code can be prefix of other code, i.e. a code ends in a leaf node of the encoding tree.
    //
    // Each code will occupy higher bits of integral type. I.e. the code 0110010 will 
    // be represented by the following pair of uint in a binary representation:
    // code  : 0110_0100_0000_0000_0000_0000_0000_0000
    // mask  : 1111_1110_0000_0000_0000_0000_0000_0000
    //
    // The codes can be sorted, to use binary search over decoding table during decoding
    private List<DecodeSymbol> BuildDecodingTable()
    {
        var result = new List<DecodeSymbol>();
        foreach (var symbol in codes)
        {
            if (symbol.Value.Length > 8 * sizeof(uint))
            {
                throw new Exception($"Code is longer then {nameof(DecodeSymbol.Code)} can fit");
            }

            uint code = 0;
            uint mask = 0;

            for (int i = 0; i < 8 * sizeof(uint); ++i)
            {
                if (i < symbol.Value.Length)
                {
                    if (symbol.Value[i] != 0)
                    {
                        code = (code << 1) | 1;
                    }
                    else
                    {
                        code = (code << 1) | 0;
                    }

                    mask = (mask << 1) | 1;
                }
                else
                {
                    code <<= 1;
                    mask <<= 1;
                }
            }

            result.Add(new DecodeSymbol
            {
                Code = code,
                Mask = mask,
                Length = symbol.Value.Length,
                Symbol = symbol.Key
            });
        }

        result.Sort((x, y) =>
        {
            if (x.Code > y.Code)
            {
                return 1;
            }
            else if (x.Code == y.Code)
            {
                return 0;
            }
            else
            {
                return -1;
            }
        });

        return result;
    }

    public byte[] Encode(string value)
    {
        int size = 0;

        for (int i = 0; i < value.Length; ++i)
        {
            size += codes[value[i]].Length;
        }

        var result = new byte[size];
        var ic = 0;

        for (int i = 0; i < value.Length; ++i)
        {
            var code = codes[value[i]];

            Array.Copy(code, 0, result, ic, code.Length);
            ic += code.Length;
        }

        return result;
    }

    public byte[] EncodeBits(string value)
    {
        int size = 0;

        // Size in bits
        for (int i = 0; i < value.Length; ++i)
        {
            size += codes[value[i]].Length;
        }
        size += codes[TerminalChar].Length;

        var byteSize = Math.DivRem(size, 8, out int rem);
        var result = new byte[byteSize + (rem > 0 ? 1 : 0)];

        int current = 0;
        int ic = 0;
        int jc = 0;

        for (int i = 0; i < value.Length + 1; ++i)
        {
            var code = codes[i < value.Length ? value[i] : TerminalChar];
            for (int j = 0; j < code.Length; ++j)
            {
                current = (current << 1) | code[j];

                if (ic == 7)
                {
                    result[jc] = (byte)current;
                    ++jc;

                    current = 0;
                    ic = 0;
                }
                else
                {
                    ++ic;
                }
            }
        }

        if (ic > 0)
        {
            result[jc] = (byte)(current << (8 - ic));
            ++jc;
        }

        if (jc != result.Length)
        {
            throw new InvalidOperationException();
        }

        return result;
    }

    public string DecodeBits(IEnumerable<byte> data)
    {
        return Decode(new ByteToBit(data));
    }

    public string DecodeUsingTable(IEnumerable<byte> data)
    {
        var result = new StringBuilder();
        var encoded = default(uint);
        var encodedLength = default(int);
        using (var bytes = data.GetEnumerator())
        {
            var left = 0;
            var hasBytes = true;
            var current = default(byte);
            while (true)
            {
                while (hasBytes && encodedLength < 32)
                {
                    if (left > 0)
                    {
                        var take = Math.Min(32 - encodedLength, left);
                        var toCopy = (uint)current;

                        if (take != 8)
                        {
                            toCopy >>= (8 - take);
                        }

                        if (encodedLength + take < 32)
                        {
                            toCopy <<= (32 - (encodedLength + take));
                        }

                        encoded |= toCopy;
                        encodedLength += take;

                        left -= take;
                        current <<= take;
                    }
                    else if (bytes.MoveNext())
                    {
                        current = bytes.Current;
                        left = 8;
                    }
                    else
                    {
                        hasBytes = false;
                    }
                }

                if (encodedLength > 0)
                {
                    var i = -1;
                    var a = 0;
                    var b = decodingTable.Count;
                    while (a != b)
                    {
                        var mid = (a + b) / 2;
                        var d = decodingTable[mid];
                        var masked = encoded & d.Mask;
                        if (masked > d.Code)
                        {
                            a = mid + 1;
                        }
                        else if (masked < d.Code)
                        {
                            b = mid;
                        }
                        else
                        {
                            i = mid;
                            break;
                        }
                    }

                    if (i < 0)
                    {
                        throw new Exception("Symbol not found. Different encoding or bad input");
                    }

                    var sym = decodingTable[i];
                    encodedLength -= sym.Length;
                    encoded <<= sym.Length;

                    if (sym.Symbol != TerminalChar)
                    {
                        result.Append(sym.Symbol);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        return result.ToString();
    }

    public static VarLenCharEncoding FromCodes(IEnumerable<CodeSymbol> codes)
    {
        var root = new BaseNode();
        foreach (var code in codes)
        {
            Reconstruct(code, 0, root);
        }
        return new VarLenCharEncoding(root);
    }

    private static void Reconstruct(CodeSymbol c, int index, BaseNode parent)
    {
        if (index == c.Code.Length - 1)
        {
            if (c.Code[index] == LeftValue)
            {
                if (parent.Left != null)
                {
                    throw new Exception("Bad code");
                }
                parent.Left = new BaseLeafNode { V = c.Symbol };
            }
            else
            {
                if (parent.Right != null)
                {
                    throw new Exception("Bad code");
                }
                parent.Right = new BaseLeafNode { V = c.Symbol };
            }
        }
        else
        {
            if (c.Code[index] == LeftValue)
            {
                parent.Left = parent.Left != null ? parent.Left : new BaseNode();
                if (parent.Left is IEncodingLeafNode)
                {
                    throw new Exception("Bad code");
                }
                Reconstruct(c, index + 1, (BaseNode)parent.Left);
            }
            else
            {
                parent.Right = parent.Right != null ? parent.Right : new BaseNode();
                if (parent.Right is IEncodingLeafNode)
                {
                    throw new Exception("Bad code");
                }
                Reconstruct(c, index + 1, (BaseNode)parent.Right);
            }
        }
    }

    private class BaseLeafNode : IEncodingLeafNode
    {
        public char V { get; set; }
    }

    private class BaseNode : IEncodingTreeNode
    {
        public IEncodingNode Left { get; set; }
        public IEncodingNode Right { get; set; }
    }

    public readonly struct DecodeSymbol
    {
        public uint Code { get; init; }

        public uint Mask { get; init; }

        public int Length { get; init; }

        public char Symbol { get; init; }
    }

    public class CodeSymbol
    {
        public byte[] Code { get; set; }

        public char Symbol { get; set; }
    }

    public class ByteToBit : IEnumerable<byte>
    {
        private readonly IEnumerable<byte> data;

        public ByteToBit(IEnumerable<byte> data)
        {
            this.data = data;
        }


        public IEnumerator<byte> GetEnumerator()
        {
            return new ByteToBitEn(data.GetEnumerator());
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class ByteToBitEn : IEnumerator<byte>
        {
            private readonly IEnumerator<byte> data;
            private int c;


            public ByteToBitEn(IEnumerator<byte> data)
            {
                this.data = data;
                this.c = 7;
            }

            public void Dispose()
            {
                data.Dispose();
            }

            public bool MoveNext()
            {
                if (c < 7)
                {
                    c++;
                }
                else
                {
                    c = 0;

                    if (!data.MoveNext())
                    {
                        return false;
                    }
                }

                return true;
            }


            public void Reset()
            {
                c = 0;
                data.Reset();
            }


            public byte Current
            {
                get
                {
                    if ((data.Current & (1 << (7 - c))) > 0)
                    {
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }
        }
    }

    public string Decode(IEnumerable<byte> data)
    {
        var current = root;
        var result = new StringBuilder();

        foreach (var bit in data)
        {
            var node = current as IEncodingTreeNode;
            if (node != null)
            {
                if (bit == LeftValue)
                {
                    current = node.Left;
                }
                else
                {
                    current = node.Right;
                }
            }

            var leaf = current as IEncodingLeafNode;
            if (leaf != null)
            {
                current = root;

                if (leaf.V == TerminalChar)
                {
                    break;
                }
                else
                {
                    result.Append(leaf.V);
                }
            }
        }

        if (current != root)
        {
            throw new ArgumentException("Incomplete or incorrect input");
        }

        return result.ToString();
    }

    public IBitDecoder GetDecoder()
    {
        return new BitDecoder(root);
    }

    public static VarLenCharEncoding FromFrequency<T>(IDictionary<char, int> charFrequencies, bool extend = false)
                     where T : VarLenCharEncodingBuilder, new()
    {
        if (extend)
        {
            for (char symbol = char.MinValue; symbol < char.MaxValue; ++symbol)
            {
                if (symbol != TerminalChar && !charFrequencies.ContainsKey(symbol))
                {
                    charFrequencies.Add(symbol, 1);
                }
            }
        }

        var builder = new T();

        foreach (var symbol in charFrequencies.Keys.OrderBy(x => x))
        {
            builder.AddChar(symbol, charFrequencies[symbol]);
        }

        return builder.Build();
    }

    public static VarLenCharEncoding FromText<T>(string text, bool extend = false)
                     where T : VarLenCharEncodingBuilder, new()
    {
        var charFrequencies = new Dictionary<char, int>();

        foreach (var c in text)
        {
            if (!charFrequencies.TryGetValue(c, out var frequency))
            {
                charFrequencies.Add(c, 1);
            }
            else
            {
                charFrequencies[c] = frequency + 1;
            }
        }

        return FromFrequency<T>(charFrequencies, extend);
    }

    private sealed class BitDecoder : IBitDecoder
    {
        private readonly IEncodingNode root;
        private IEncodingNode current;
        private readonly Stack<IEncodingNode> states = new();

        public BitDecoder(IEncodingNode root)
        {
            this.root = root;
            this.current = root;
        }

        public char Next(bool p)
        {
            states.Push(current);

            var node = current as IEncodingTreeNode;
            if (node != null)
            {
                if (p == (LeftValue == 1))
                {
                    current = node.Left;
                }
                else
                {
                    current = node.Right;
                }
            }

            var leaf = current as IEncodingLeafNode;
            if (leaf != null)
            {
                current = root;

                if (leaf.V == TerminalChar)
                {
                    return TerminalChar;
                }
                else
                {
                    return leaf.V;
                }
            }

            return TerminalChar;
        }

        public void Pop()
        {
            current = states.Pop();
        }
    }
}

public interface IBitDecoder
{
    char Next(bool bit);

    void Pop();
}

public abstract class VarLenCharEncodingBuilder
{
    protected readonly List<CharFrequency> symbols = new();
    protected readonly HashSet<char> alphabet = new();

    private bool consumed = false;

    public void AddChar(char c, int frequency)
    {
        EnsureConsumed(false);

        if (c == VarLenCharEncoding.TerminalChar)
        {
            throw new ArgumentOutOfRangeException(nameof(c));
        }

        if (!alphabet.Add(c))
        {
            throw new ArgumentException("Duplicate character");
        }

        symbols.Add(new CharFrequency(c, frequency));
    }

    private void AddTerminationSymbol()
    {
        EnsureConsumed(false);
        consumed = true;

        // Add termination character, i.e. 0
        int maxWeight = 0;
        foreach (var item in symbols)
        {
            maxWeight = Math.Max(maxWeight, item.f);
        }

        symbols.Add(new CharFrequency(VarLenCharEncoding.TerminalChar, maxWeight));
    }

    private void EnsureConsumed(bool value)
    {
        if (consumed != value)
        {
            throw new InvalidOperationException();
        }
    }

    public VarLenCharEncoding Build()
    {
        EnsureConsumed(false);
        AddTerminationSymbol();
        return DoBuild();
    }

    protected abstract VarLenCharEncoding DoBuild();

    protected readonly struct CharFrequency
    {
        public readonly char c;
        public readonly int f;

        public CharFrequency(char c, int f)
        {
            this.c = c;
            this.f = f;
        }
    }
}
