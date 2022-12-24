namespace Protsyk.PMS.FullText.Core.UnitTests;

public class OrMultiQueryTest : TestWithFolderBase
{
    [Fact]
    public void TestOrMultiQueryWithFixedPostingList_1()
    {
        using var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder));
        using var query = new OrMultiQuery(
            new TermQuery(TestHelper.GetPostingList(index, "this")),
            new TermQuery(TestHelper.GetPostingList(index, "is")),
            new TermQuery(TestHelper.GetPostingList(index, "and"))
        );

        var result = query.ExecuteToString();
        var expected = "{[3,1,1]}, {[3,1,2]}, {[4,1,1]}, {[4,1,4]}, {[5,1,1]}, {[5,1,8]}, {[6,1,2]}, {[6,1,8]}, {[6,1,9]}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestOrMultiQueryWithFixedPostingList_2()
    {
        using var query = new OrMultiQuery(
            new TermQuery(new PostingListArray(new Occurrence[] { Occurrence.O(1, 1, 1) })),
            new TermQuery(new PostingListArray(new Occurrence[] { Occurrence.O(1, 1, 2), })),
            new TermQuery(new PostingListArray(new Occurrence[] { Occurrence.O(1, 1, 2), Occurrence.O(1, 1, 3) }))
        );

        var result = query.ExecuteToString();
        var expected = "{[1,1,1]}, {[1,1,2]}, {[1,1,2]}, {[1,1,3]}";
        Assert.Equal(expected, result);
    }
}
