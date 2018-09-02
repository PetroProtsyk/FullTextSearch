using System;
using System.Text;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
    internal class DecodingMatcher : IDfaMatcher<byte>
    {
        private readonly IDfaMatcher<char> matcher;
        private readonly int maxLength;
        private readonly ITextEncoding encoding;
        private readonly byte[] data;

        private int dataIndex;

        public DecodingMatcher(IDfaMatcher<char> matcher, int maxLength, ITextEncoding encoding)
        {
            this.matcher = matcher;
            this.maxLength = maxLength;
            this.encoding = encoding;
            this.data = new byte[maxLength];
            this.dataIndex = 0;
        }

        public bool IsFinal()
        {
            var token = encoding.GetString(data, 0, dataIndex);
            matcher.Reset();
            for (int i = 0; i < token.Length; ++i)
            {
                if (!matcher.Next(token[i]))
                {
                    return false;
                }
            }
            return matcher.IsFinal();
        }

        public bool Next(byte p)
        {
            if (dataIndex >= maxLength)
            {
                throw new Exception("Data exceeds maximum length");
            }

            data[dataIndex++] = p;
            return true;
        }

        public void Pop()
        {
            if (dataIndex == 0)
            {
                throw new InvalidOperationException();
            }
            --dataIndex;
        }

        public void Reset()
        {
            dataIndex = 0;
            matcher.Reset();
        }
    }

}
