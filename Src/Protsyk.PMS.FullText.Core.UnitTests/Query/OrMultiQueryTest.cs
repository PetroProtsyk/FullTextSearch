using System;
using System.Linq;
using Xunit;


namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class OrMultiQueryTest : TestWithFolderBase
    {
        [Fact]
        public void TestOrMultiQueryWithFixedPostingList_1()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
            {
                using (var query = new OrMultiQuery(
                    new TermQuery(index.GetPostingList("this")),
                    new TermQuery(index.GetPostingList("is")),
                    new TermQuery(index.GetPostingList("and"))))
                {
                    var result = query.ExecuteToString();
                    var expected = "{[3,1,1]}, {[3,1,2]}, {[4,1,1]}, {[4,1,4]}, {[5,1,1]}, {[5,1,8]}, {[6,1,2]}, {[6,1,8]}, {[6,1,9]}";
                    Assert.Equal(expected, result);
                }
            }
        }
    }
}
