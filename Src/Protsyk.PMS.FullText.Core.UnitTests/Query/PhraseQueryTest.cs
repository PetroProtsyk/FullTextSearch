namespace Protsyk.PMS.FullText.Core.UnitTests;

public class PhraseQueryTest : TestWithFolderBase
{
    [Fact]
    public void TestPhraseQueryWithDefaultIndex_1()
    {
        using var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder));
        using var query = new PhraseQuery(
            new TermQuery(TestHelper.GetPostingList(index, "search")),
            new TermQuery(TestHelper.GetPostingList(index, "only")));

        var result = query.ExecuteToString();
        var expected = "{[5,1,3], [5,1,4]}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestPhraseQueryWithDefaultIndex_2()
    {
        using var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder));
        using var query = new PhraseQuery(
            new TermQuery(TestHelper.GetPostingList(index, "this")),
            new TermQuery(TestHelper.GetPostingList(index, "is")));

        var result = query.ExecuteToString();
        var expected = "{[3,1,1], [3,1,2]}, {[6,1,8], [6,1,9]}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestPhraseQueryWithDefaultIndex_3()
    {
        using var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder));
        using var query = new PhraseQuery(
            new TermQuery(TestHelper.GetPostingList(index, "search")),
            new TermQuery(TestHelper.GetPostingList(index, "only")),
            new TermQuery(TestHelper.GetPostingList(index, "supports")),
            new TermQuery(TestHelper.GetPostingList(index, "boolean"))
        );

        var result = query.ExecuteToString();
        var expected = "{[5,1,3], [5,1,4], [5,1,5], [5,1,6]}";
        Assert.Equal(expected, result);
    }

}
