using System;
using System.Linq;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class PhraseQueryTest : TestWithFolderBase
    {
        [Fact]
        public void TestPhraseQueryWithDefaultIndex_1()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
            {
                using (var query = new PhraseQuery(
                    new TermQuery(index.GetPostingList("search")),
                    new TermQuery(index.GetPostingList("only"))))
                {
                    var result = query.ExecuteToString();
                    var expected = "{[5,1,3], [5,1,4]}";
                    Assert.Equal(expected, result);
                }
            }
        }

        [Fact]
        public void TestPhraseQueryWithDefaultIndex_2()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
            {
                using (var query = new PhraseQuery(
                    new TermQuery(index.GetPostingList("this")),
                    new TermQuery(index.GetPostingList("is"))))
                {
                    var result = query.ExecuteToString();
                    var expected = "{[3,1,1], [3,1,2]}, {[6,1,8], [6,1,9]}";
                    Assert.Equal(expected, result);
                }
            }
        }

        [Fact]
        public void TestPhraseQueryWithDefaultIndex_3()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
            {
                using (var query = new PhraseQuery(
                    new TermQuery(index.GetPostingList("search")),
                    new TermQuery(index.GetPostingList("only")),
                    new TermQuery(index.GetPostingList("supports")),
                    new TermQuery(index.GetPostingList("boolean"))))
                {
                    var result = query.ExecuteToString();
                    var expected = "{[5,1,3], [5,1,4], [5,1,5], [5,1,6]}";
                    Assert.Equal(expected, result);
                }
            }
        }

    }
}
