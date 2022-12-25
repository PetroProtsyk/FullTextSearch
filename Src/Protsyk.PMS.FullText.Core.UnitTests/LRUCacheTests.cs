using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.UnitTests;

public class LRUCacheTests
{
    [Fact]
    public void Acceptance()
    {
        var lru = new LRUCache<int, int>(2);
        lru.Put(3, 1);
        lru.Put(2, 1);
        lru.Put(2, 2);    // evicts key 2
        lru.Put(4, 4);    // evicts key 1.

        Assert.False(lru.TryGet(3, out var _));
        Assert.Equal(4, lru.Get(4));
        Assert.Equal(2, lru.Get(2));
    }
}
