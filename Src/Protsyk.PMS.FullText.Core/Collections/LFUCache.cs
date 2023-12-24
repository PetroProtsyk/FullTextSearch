namespace Protsyk.PMS.FullText.Core.Collections;

public class LFUCache<TKey, TValue>
{
    private readonly GenericHeap<(int, int, TKey)> h = new();
    private readonly Dictionary<TKey, (TValue, GenericHeap<(int, int, TKey)>.IItemReference)> c = new();
    private readonly int capacity;
    private int nextOrder;

    public LFUCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        this.capacity = capacity;
        this.nextOrder = 0;
    }

    private void Ensure(int count)
    {
        while (h.Count + count > capacity)
        {
            var k = h.RemoveTop();
            if (!c.Remove(k.Item3))
            {
                throw new Exception("Unexpected");
            }
        }
    }

    public void Put(TKey key, TValue value)
    {
        if (!c.TryGetValue(key, out var valref))
        {
            Ensure(1);
            var order = Interlocked.Increment(ref nextOrder);
            var newRef = h.Add((1, nextOrder, key));
            valref = (value, newRef);
            c.Add(key, valref);
        }
        else
        {
            var order = Interlocked.Increment(ref nextOrder);
            var newFrequency = valref.Item2.Value.Item1 + 1;
            valref.Item2.Change((newFrequency, order, key));
            c[key] = (value, valref.Item2);
        }
    }

    public TValue Get(TKey key)
    {
        if (!TryGet(key, out var result))
        {
            throw new KeyNotFoundException();
        }
        return result;
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (!c.TryGetValue(key, out var valref))
        {
            value = default;
            return false;
        }
        else
        {
            var order = Interlocked.Increment(ref nextOrder);
            var newFrequency = valref.Item2.Value.Item1 + 1;
            valref.Item2.Change((newFrequency, order, key));
            value = valref.Item1;
            return true;
        }
    }
}
