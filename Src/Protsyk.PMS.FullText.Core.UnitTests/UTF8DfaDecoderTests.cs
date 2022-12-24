using System.Text;

using Protsyk.PMS.FullText.Core.Common;

namespace Protsyk.PMS.FullText.Core.UnitTests;

public class UTF8DfaDecoderTest
{
    [Fact]
    public void Decoding()
    {
        var text = "Hello Здоровенькі ᆵሄ⅙⅙Ⅸ ТестыТексты Були שלום עולם";
        var input = Encoding.UTF8.GetBytes(text);
        var decoded = UTF8DfaDecoder.Decode(input);
        Assert.Equal(text, decoded);
    }
}
