using System;
using System.Linq;
using Protsyk.PMS.FullText.Core.Collections;
using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class HeapTests
    {
        [Theory]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10031)]
        public void TestHeapSort(int n)
        {
            var rnd = Enumerable.Range(1, n).ToArray().Shuffle();

            var expected = rnd.OrderBy(x => x).ToArray();
            var actual = rnd.HeapSort().ToArray();

            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(10031)]
        public void TestTopN(int n)
        {
            var rnd = Enumerable.Range(1, n).ToArray().Shuffle();

            var expected = rnd.OrderByDescending(x => x).Take(n / 2).ToArray();
            var actual = rnd.TopN(n / 2).ToArray();

            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }
}
