using System;
using System.Linq;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class SearchTest : TestWithFolderBase
    {
        [Fact]
        public void TestTermSearchPersistentIndex()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new PersistentIndexName(TestFolder)))
            {
                TestTermSearch(index);
            }
        }

        [Fact]
        public void TestTermSearchMemoryIndex()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new InMemoryIndexName()))
            {
                TestTermSearch(index);
            }
        }

        private void TestTermSearch(IFullTextIndex index)
        {
            var postings = TestHelper.GetPostingList(index, "this").ToString();
            Assert.Equal("[3,1,1], [4,1,1], [5,1,1], [6,1,8]", postings);
        }
    }
}
