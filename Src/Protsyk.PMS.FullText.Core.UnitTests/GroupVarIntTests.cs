using System;
using System.Linq;
using Xunit;

namespace Protsyk.PMS.FullText.Core
{
    public class GroupVarIntTests
    {
        [Fact]
        public void Encoding()
        {
            // Test from article
            Assert.Equal("00010000 01010000 00000001 01000000 00011111 11111111", GroupVarint.EncodeToBits(new int[] { 80, 320, 31, 255 }));
        }

        [Fact]
        public void EncodingDecoding()
        {
            Test(new int[] { 80, 320, 31, 255, int.MaxValue, 1000000, -1, 8 });
            Test(new int[] { 80 });
            Test(new int[] { 80, -2 });
            Test(new int[] { 80, -2, int.MaxValue });
            Test(new int[] { -6677741, 1, int.MaxValue, int.MinValue });
            Test(Enumerable.Range(1, 1000000).ToArray());
        }

        static void Test(int[] input)
        {
            var result = GroupVarint.Decode(GroupVarint.Encode(input));
            Assert.Equal(input, result);
        }
    }
}
