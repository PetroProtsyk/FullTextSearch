namespace Protsyk.PMS.FullText.Core.UnitTests;

public class TextPositionTests: TestWithFolderBase
{
    [Fact]
    public void TextPositionsPersistentIndex()
    {
        using var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder));

        TextIndex(index);
    }

    [Fact]
    public void TextPositionsMemoryIndex()
    {
        using var index = TestHelper.PrepareIndexForSearch(new InMemoryIndexName());

        TextIndex(index);
    }

    private void TextIndex(IFullTextIndex index)
    {
        Assert.Equal(new TextPosition[]
                        {
                            new(0, 5),
                            new(6, 5)
                        },
                        index.GetPositions(1, 1));

        Assert.Equal(new TextPosition[]
                        {
                            new(0, 5),
                            new(6, 9),
                            new(16, 6)
                        },
                        index.GetPositions(2, 1));

        Assert.Equal(new TextPosition[]
                        {
                            new(0, 11),
                            new(12, 2),
                            new(15, 4),
                            new(20, 8),
                            new(30, 8),
                            new(39, 3),
                            new(43, 4),
                            new(49, 4),
                            new(54, 2),
                            new(57, 9)
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

    [Theory]
    [InlineData("[0,0]", 0, 0)]
    [InlineData("[1,5]", 1, 5)]
    public void CanParse(string text, int offset, int length)
    {
        var result = TextPosition.Parse(text);

        Assert.Equal(offset, result.Offset);
        Assert.Equal(length, result.Length);
    }
}
