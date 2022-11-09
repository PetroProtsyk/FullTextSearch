using System.IO;
using System.Linq;

using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common.Persistance;

using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class BtreePersistentTests : TestWithFolderBase
    {
        [Theory]
        [InlineData(1000)]
        public void BtreePTests_Base(int size)
        {
            GenericDictionaryTests.TheFirstTest<BtreePersistent<int, string>>();
            GenericDictionaryTests.TheSecondTest<BtreePersistent<int, string>>(size);
            GenericDictionaryTests.TheOrderByTest<BtreePersistent<int, string>>(size);
            GenericDictionaryTests.TheRemoveTest<BtreePersistent<int, string>>();
            GenericDictionaryTests.TheRemoveTestFull<BtreePersistent<int, string>>(size);
            GenericDictionaryTests.TheDictionaryTest<BtreePersistent<int, string>>();
        }

        [Fact]
        public void BtreeP_Append_Test()
        {
            var testFile = Path.Combine(TestFolder, "btree-test.bin");
            using (var tree = new BtreePersistent<int, string>(new FileStorage(testFile), 4))
            {
                tree.Add(1, "Petro");
                tree.Add(2, "Sophie");
                tree.Add(3, "Maria");
                tree.Add(4, "Tolya");
                tree.Add(5, "Pavlo");
                tree.Add(6, "Natasha");
                tree.Add(7, "Antonina");
                tree.Add(8, "Alexander");
                tree.Add(9, "Raisa");

                Assert.True(tree.ContainsKey(1));
                Assert.True(tree.ContainsKey(2));
                Assert.True(tree.ContainsKey(9));
                Assert.False(tree.ContainsKey(10));
            }

            using (var tree = new BtreePersistent<int, string>(new FileStorage(testFile), 4))
            {
                tree.Add(10, "Sylvia");
                tree.Add(11, "Lyisa");
                tree.Add(12, "Olivia");
                tree.Add(13, "Martijn");
                tree.Add(14, "Karlijn");
                tree.Add(15, "Steven");
                tree.Add(16, "Zozylya");

                Assert.True(tree.ContainsKey(1));
                Assert.True(tree.ContainsKey(2));
                Assert.True(tree.ContainsKey(9));
                Assert.True(tree.ContainsKey(10));
                Assert.True(tree.ContainsKey(15));
            }
        }

        [Theory]
        [InlineData(1000)]
        public string TestBtreeP(int size)
        {
            var rnd = Enumerable.Range(1, size).ToArray().Shuffle();
            var testFile = Path.Combine(TestFolder, "btree-test.bin");

            using (var tree = new BtreePersistent<int, string>(new FileStorage(testFile), 101))
            {
                foreach (var r in rnd)
                {
                    tree.Add(r, $"Number = {r}");
                }

                foreach (var r in rnd)
                {
                    Assert.True(tree.ContainsKey(r));
                }

                for (int i = -1000; i < 0; i++)
                {
                    Assert.False(tree.ContainsKey(i));
                }
            }

            using (var tree = new BtreePersistent<int, string>(new FileStorage(testFile), 101))
            {
                foreach (var r in rnd)
                {
                    Assert.True(tree.ContainsKey(r));
                }

                for (int i = -1000; i < 0; i++)
                {
                    Assert.False(tree.ContainsKey(i));
                }
            }

            return testFile;
        }
    }
}
