using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class SearchTest
    {
        [Theory]
        [InlineData("TST", "BTree", "Text", "UTF-8")]
        [InlineData("TST", "BTree", "Binary", "UTF-8")]
        [InlineData("TST", "BTree", "BinaryCompressed", "UTF-8")]
        [InlineData("TST", "HashTable", "BinaryCompressed", "UTF-8")]
        [InlineData("TST", "List", "Text", "UTF-8")]
        [InlineData("TST", "List", "Binary", "UTF-8")]
        [InlineData("TST", "List", "BinaryCompressed", "UTF-8")]
        [InlineData("TST", "List", "BinaryCompressed", "LatinHuffman")]
        [InlineData("TST", "List", "BinaryCompressed", "LatinHuTucker")]
        [InlineData("TST", "List", "BinaryCompressed", "LatinBalanced")]
        public void TestTermSearchPersistentIndex(string dictionaryType, string fieldsType, string postingType, string textEncoding)
        {
            var testFolder = Path.Combine(Path.GetTempPath(), "PMS_FullText_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testFolder);

            try
            {
                var indexName = new PersistentIndexName(testFolder, dictionaryType, fieldsType, postingType, textEncoding);

                using (var index = TestHelper.PrepareIndexForSearch(indexName))
                {
                    TestTermSearch(index, "WORD(this)", "{[3,1,1]}, {[4,1,1]}, {[5,1,1]}, {[6,1,8]}");
                }

                using (var index = TestHelper.AddToIndex(indexName, "this is not a joke"))
                {
                    TestTermSearch(index, "WORD(this)", "{[3,1,1]}, {[4,1,1]}, {[5,1,1]}, {[6,1,8]}, {[7,1,1]}");
                }

                using (var index = TestHelper.AddToIndex(indexName, "Really, this is not a joke"))
                {
                    TestTermSearch(index, "WORD(this)", "{[3,1,1]}, {[4,1,1]}, {[5,1,1]}, {[6,1,8]}, {[7,1,1]}, {[8,1,2]}");
                    TestTermSearch(index, "WILD(th?s)", "{[3,1,1]}, {[4,1,1]}, {[5,1,1]}, {[6,1,8]}, {[7,1,1]}, {[8,1,2]}");
                    TestTermSearch(index, "EDIT(these,2)", "{[3,1,1]}, {[3,1,3]}, {[4,1,1]}, {[4,1,2]}, {[5,1,1]}, {[6,1,8]}, {[7,1,1]}, {[8,1,2]}");
                }
            }
            finally
            {
                Directory.Delete(testFolder, true);
            }
        }

        [Fact]
        public void TestTermSearchMemoryIndex()
        {
            using (var index = TestHelper.PrepareIndexForSearch(new InMemoryIndexName()))
            {
                TestTermSearch(index, "WORD(this)", "{[3,1,1]}, {[4,1,1]}, {[5,1,1]}, {[6,1,8]}");
            }
        }

        private void TestTermSearch(IFullTextIndex index, string query, string expected)
        {
            var postings = index.Compile(query).ExecuteToString();
            Assert.Equal(expected, postings);
        }
    }
}
