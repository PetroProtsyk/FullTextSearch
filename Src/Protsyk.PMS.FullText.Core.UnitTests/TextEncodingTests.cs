using System;
using System.Linq;
using System.Text;
using Protsyk.PMS.FullText.Core.Common.Compression;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class TextEncodingTests
    {
        [Fact]
        public void EncodingDecodingBalancedByWeight()
        {
            Test<BalancedByWeightBuilder>();
        }

        [Fact]
        public void EncodingDecodingHuTuckerSimple()
        {
            Test<HuTuckerSimpleBuilder>();
        }

        [Fact]
        public void EncodingDecodingHuTucker()
        {
            Test<HuTuckerBuilder>();
        }

        [Fact]
        public void EncodingDecodingHuffman()
        {
            Test<HuffmanEncodingBuilder>();
        }

        static void Test<T>()
            where T : VarLenCharEncodingBuilder, new()
        {
            var textBuilder = new StringBuilder();
            for (int j = 0; j < 1000; ++j)
            {
                textBuilder.Append("Hello" + j);
                textBuilder.Append("Здоровенькі" + j);
                textBuilder.Append("Були" + j);
                textBuilder.Append("Окружение" + j);
                textBuilder.Append("שלום" + j);
                textBuilder.Append("עולם" + j);
                textBuilder.Append("ТестыТексты" + j);
                textBuilder.Append("ТестыТексты" + j);
                textBuilder.Append("\u03E8\u0680\u0930\u0B81\u2CAA" + j);
            }

            var input = textBuilder.ToString();
            var encoding = new VarLenEncoding(VarLenCharEncoding.FromText<T>(input));
            var encoded = encoding.GetBytes(input).ToArray();
            var decoded = encoding.GetString(encoded, 0, encoded.Length);

            Assert.Equal(input, decoded);
        }
    }
}
