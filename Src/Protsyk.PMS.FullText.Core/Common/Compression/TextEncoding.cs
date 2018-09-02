using System;
using System.Collections.Generic;
using System.Text;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
    public class TextEncoding : ITextEncoding
    {
        public static ITextEncoding Default = new TextEncoding(Encoding.UTF8);

        private readonly Encoding encoding;

        public TextEncoding(Encoding encoding)
        {
            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public string GetName() => encoding.WebName;

        public IEnumerable<byte> GetBytes(string text, int index, int count)
            => encoding.GetBytes(text, 0, text.Length);

        public string GetString(byte[] bytes, int index, int count)
            => encoding.GetString(bytes, index, count);

        public int GetMaxEncodedLength(int maxTokenLength)
            => encoding.GetMaxByteCount(maxTokenLength);

        public IDfaMatcher<byte> CreateMatcher(IDfaMatcher<char> charMatcher, int maxLength)
            => encoding == Encoding.UTF8 ?
                (IDfaMatcher<byte>)new DecodingMatcherForUTF8(charMatcher, GetMaxEncodedLength(maxLength)) :
                (IDfaMatcher<byte>)new DecodingMatcher(charMatcher, GetMaxEncodedLength(maxLength), this);
    }
}
