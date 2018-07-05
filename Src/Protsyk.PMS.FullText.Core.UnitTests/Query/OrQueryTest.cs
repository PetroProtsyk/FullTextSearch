using System;
using System.Linq;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class OrQueryTest : TestWithFolderBase
    {
        [Fact]
        public void TestOrQueryWithFixedPostingList_1()
        {
            var e = "{[1,1,1]}, {[1,1,2]}, {[1,1,3]}, {[1,1,4]}, {[1,1,5]}, {[1,1,10]}, {[1,2,1]}, {[1,3,1]}, {[1,3,2]}, {[1,3,3]}, {[1,3,4]}, {[1,3,5]}, {[2,1,1]}, {[2,1,2]}";

            var left = new TermQuery(PostingListArray
                                        .Parse("[1,1,1], [1,1,5], [1,1,10], [1,2,1], [1,3,5], [2,1,2]"));
            var right = new TermQuery(PostingListArray
                                         .Parse("[1,1,2], [1,1,3], [1,1,4], [1,3,1], [1,3,2], [1,3,3], [1,3,4], [2,1,1]"));

            using (var q = new OrQuery(left, right))
            {
                var r = q.ExecuteToString();

                Assert.Equal(e, r);
                Assert.Null(q.NextMatch());
            }
        }


        [Fact]
        public void TestOrQueryWithFixedPostingList_2()
        {
            var e = "{[1,1,1]}, {[1,1,2]}, {[1,1,3]}";

            var left = new TermQuery(PostingListArray.Parse("[1,1,1]"));
            var right = new TermQuery(PostingListArray.Parse("[1,1,2], [1,1,3]"));

            using (var q = new OrQuery(left, right))
            {
                var r = q.ExecuteToString();
                Assert.Equal(e, r);
                Assert.Null(q.NextMatch());
            }
        }


        [Fact]
        public void TestOrQueryWithFixedPostingList_3()
        {
            var e = "{[1,1,1]}, {[1,1,2]}, {[1,1,3]}";

            var left = new TermQuery(PostingListArray.Parse("[1,1,2], [1,1,3]"));
            var right = new TermQuery(PostingListArray.Parse("[1,1,1]"));

            using (var q = new OrQuery(left, right))
            {
                var r = q.ExecuteToString();
                Assert.Equal(e, r);
                Assert.Null(q.NextMatch());
            }
        }

        [Fact]
        public void TestOrQueryWithFixedPostingList_4()
        {
            var e = "{[1,1,1]}, {[1,1,3]}";

            var left = new TermQuery(PostingListArray.Parse("[1,1,1], [1,1,3]"));
            var right = NullQuery.Instance;

            using (var q = new OrQuery(left, right))
            {
                var r = q.ExecuteToString();
                Assert.Equal(e, r);
                Assert.Null(q.NextMatch());
            }
        }

        [Fact]
        public void TestOrQueryWithFixedPostingList_5()
        {
            var e = "{[1,1,1]}, {[1,1,3]}";

            var left = NullQuery.Instance;
            var right = new TermQuery(PostingListArray.Parse("[1,1,1], [1,1,3]"));

            using (var q = new OrQuery(left, right))
            {
                var r = q.ExecuteToString();
                Assert.Equal(e, r);
                Assert.Null(q.NextMatch());
            }
        }

        [Fact]
        public void TestOrQueryWithFixedPostingList_6()
        {
            var left = NullQuery.Instance;
            var right = NullQuery.Instance;

            using (var q = new OrQuery(left, right))
            {
                var r = q.ExecuteToString();
                Assert.Equal("", r);
                Assert.Null(q.NextMatch());
            }
        }

        [Fact]
        public void TestOrQueryWithDefaultIndex()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
            {
                using (var query = new OrQuery(
                    new TermQuery(TestHelper.GetPostingList(index, "this")),
                    new TermQuery(TestHelper.GetPostingList(index, "is"))))
                {
                    var result = query.ExecuteToString();
                    var expected = "{[3,1,1]}, {[3,1,2]}, {[4,1,1]}, {[4,1,4]}, {[5,1,1]}, {[6,1,2]}, {[6,1,8]}, {[6,1,9]}";

                    Assert.Equal(expected, result);
                }
            }
        }
    }
}
