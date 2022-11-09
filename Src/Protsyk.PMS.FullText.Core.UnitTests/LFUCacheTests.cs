using Protsyk.PMS.FullText.Core.Collections;

using Xunit;

namespace Protsyk.PMS.FullText.Core.UnitTests
{
    public class LFUCacheTests
    {
        [Fact]
        public void Acceptance()
        {
            var lfu = new LFUCache<int, int>(2);
            lfu.Put(3, 1);
            lfu.Put(2, 1);
            lfu.Put(2, 2);    // replace key 2
            lfu.Put(4, 4);    // evicts key 3

            Assert.False(lfu.TryGet(3, out var _));
            Assert.Equal(4, lfu.Get(4));
            Assert.Equal(2, lfu.Get(2));
        }
    }
}
