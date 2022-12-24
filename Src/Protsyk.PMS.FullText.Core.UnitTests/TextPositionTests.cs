using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests;

public class TextPositionTests: TestWithFolderBase
{
    [Fact]
    public void TextPositionsPersistentIndex()
    {
        using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
        {
            TextIndex(index);
        }
    }

    [Fact]
    public void TextPositionsMemoryIndex()
    {
        using (var index = TestHelper.PrepareIndexForSearch(new InMemoryIndexName()))
        {
            TextIndex(index);
        }
    }

    private void TextIndex(IFullTextIndex index)
    {
        Assert.Equal(new TextPosition[]
                        {
                            TextPosition.P(0, 5),
                            TextPosition.P(6, 5)
                        },
                        index.GetPositions(1, 1));

        Assert.Equal(new TextPosition[]
                        {
                            TextPosition.P(0, 5),
                            TextPosition.P(6, 9),
                            TextPosition.P(16, 6)
                        },
                        index.GetPositions(2, 1));

        Assert.Equal(new TextPosition[]
                        {
                            TextPosition.P(0, 11),
                            TextPosition.P(12, 2),
                            TextPosition.P(15, 4),
                            TextPosition.P(20, 8),
                            TextPosition.P(30, 8),
                            TextPosition.P(39, 3),
                            TextPosition.P(43, 4),
                            TextPosition.P(49, 4),
                            TextPosition.P(54, 2),
                            TextPosition.P(57, 9)
                        },
                    index.GetPositions(6, 1));

        Assert.Equal("Programming is very exciting. Programs can help. This is fantastic!!!", index.GetText(6, 1).ReadToEnd());

        var doc = new TextDocument(index.GetText(6, 1).ReadToEnd(),
                                   index.GetPositions(6, 1));

        Assert.Equal("Programming", doc.Token(0));
        Assert.Equal("is", doc.Token(1));
        Assert.Equal("very", doc.Token(2));
        Assert.Equal("exciting", doc.Token(3));
        Assert.Equal("Programs", doc.Token(4));
        Assert.Equal("can", doc.Token(5));
        Assert.Equal("help", doc.Token(6));
        Assert.Equal("This", doc.Token(7));
        Assert.Equal("is", doc.Token(8));
        Assert.Equal("fantastic", doc.Token(9));

        Assert.Equal("Programming", doc.TokenAt(3));
        Assert.Equal("is", doc.TokenAt(13));
        Assert.Equal("very", doc.TokenAt(15));
        Assert.Equal("fantastic", doc.TokenAt(60));
    }
}
