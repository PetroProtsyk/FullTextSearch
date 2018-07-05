using System;
using System.Linq;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class TermQueryTest : TestWithFolderBase
    {
        [Fact]
        public void TestTermQueryWithFixedPostingList()
        {
            using (var q = new TermQuery(
                PostingListArray.Parse("[3,1,1], [4,1,1], [5,1,1], [50,10,81], [143787543,79815,2124]")))
            {
                var m = 0;
                while (q.NextMatch() != null)
                {
                    ++m;
                }

                Assert.Equal(5, m);
                Assert.Null(q.NextMatch());
            }
        }

        [Fact]
        public void TestTermQueryWithDefaultIndex()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
            {
                using (var query = new TermQuery(TestHelper.GetPostingList(index, "this")))
                {
                    var result = query.ExecuteToString();
                    var expected = "{[3,1,1]}, {[4,1,1]}, {[5,1,1]}, {[6,1,8]}";

                    Assert.Equal(expected, result);
                }
            }
        }

    }
}
