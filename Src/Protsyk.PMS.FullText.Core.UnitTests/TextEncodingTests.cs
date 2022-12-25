using System.Linq;
using System.Text;

using Protsyk.PMS.FullText.Core.Common.Compression;

namespace Protsyk.PMS.FullText.Core.UnitTests;

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

    [Fact]
    public void EncodingReconstruction()
    {
        var codes = new VarLenCharEncoding.CodeSymbol[] {
            new VarLenCharEncoding.CodeSymbol{Symbol ='_', Code = new byte[] {1, 1, 1 }},
            new VarLenCharEncoding.CodeSymbol{Symbol ='a', Code = new byte[] {0, 1, 0 }},
            new VarLenCharEncoding.CodeSymbol{Symbol ='e', Code = new byte[] {0, 0, 0}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='f', Code = new byte[] {1, 1, 0, 1}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='h', Code = new byte[] {1, 0, 1, 0}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='i', Code = new byte[] {1, 0, 0, 0}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='m', Code = new byte[] {0, 1, 1, 1}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='n', Code = new byte[] {0, 0, 1, 0}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='s', Code = new byte[] {1, 0, 1, 1}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='t', Code = new byte[] {0, 1, 1, 0}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='l', Code = new byte[] {1, 1, 0, 0, 1}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='o', Code = new byte[] {0, 0, 1, 1, 0}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='p', Code = new byte[] {1, 0, 0, 1, 1}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='r', Code = new byte[] {1, 1, 0, 0, 0}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='u', Code = new byte[] {0, 0, 1, 1, 1}},
            new VarLenCharEncoding.CodeSymbol{Symbol ='x', Code = new byte[] {1, 0, 0, 1, 0}},
        };

        var encoding = VarLenCharEncoding.FromCodes(codes);

        Assert.Equal("hello", encoding.Decode(new byte[] { 1, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1, 1, 0 }));
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
