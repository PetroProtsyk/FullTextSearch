using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
    public class VarLenEncoding : ITextEncoding
    {
        public static ITextEncoding Default = new TextEncoding(Encoding.UTF8);

        private readonly VarLenCharEncoding encoding;

        public VarLenEncoding(VarLenCharEncoding encoding)
        {
            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public string GetName() => encoding.ToString();

        public IEnumerable<byte> GetBytes(string text, int index, int count)
            => encoding.EncodeBits(text);

        public string GetString(byte[] bytes, int index, int count)
            => encoding.DecodeUsingTable(bytes.Skip(index).Take(count));

        public int GetMaxEncodedLength(int maxTokenLength)
            => 4 * maxTokenLength;

        public IDfaMatcher<byte> CreateMatcher(IDfaMatcher<char> charMatcher, int maxLength)
            => (IDfaMatcher<byte>)new DecodingMatcherForVarLenCharEncoding(charMatcher, encoding);
    }
}
