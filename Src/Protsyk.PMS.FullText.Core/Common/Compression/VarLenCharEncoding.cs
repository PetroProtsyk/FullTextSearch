using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Globalization;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
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
        private readonly IEncodingNode root;
        private readonly Dictionary<char, byte[]> codes;
        public const char TerminalChar = '\0';

        internal VarLenCharEncoding(IEncodingNode root)
        {
            this.root = root;
            this.codes = new Dictionary<char, byte[]>();

            Traverse(root, codes, new List<byte>());
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
                current.Add(1);
                Traverse(node.Left, codes, current);
                current.RemoveAt(current.Count - 1);

                current.Add(0);
                Traverse(node.Right, codes, current);
                current.RemoveAt(current.Count - 1);
                return;
            }

            throw new InvalidOperationException();
        }

        private string RenderChar(char c)
        {
            var category = char.GetUnicodeCategory(c);

            if (char.IsWhiteSpace(c) ||
                category == UnicodeCategory.Control ||
                category == UnicodeCategory.OtherNotAssigned ||
                category == UnicodeCategory.Surrogate)
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

            foreach (var code in codes.Keys.OrderBy(x=>x))
            {
                result.AppendLine($"{RenderChar(code)} = { string.Join("", codes[code].Select(b=>$"{b}"))}");
            }

            return result.ToString();
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

            int rem;
            var byteSize = Math.DivRem(size, 8, out rem);
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
                        result[jc] = (byte) current;
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
                result[jc] = (byte) (current << (8 - ic));
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
                    if (bit == 1)
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
    }

    public abstract class VarLenCharEncodingBuilder
    {
        protected readonly List<CharFrequency> symbols = new List<CharFrequency>();
        protected readonly HashSet<char> alphabet = new HashSet<char>();

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

        protected struct CharFrequency
        {
            public char c;
            public int f;

            public CharFrequency(char c, int f)
            {
                this.c = c;
                this.f = f;
            }
        }
    }
}
