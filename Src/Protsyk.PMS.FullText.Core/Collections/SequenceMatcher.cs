using System.Linq;

namespace Protsyk.PMS.FullText.Core.Collections;

/// <summary>
/// Match element in that starts from a given prefix
/// </summary>
public class SequenceMatcher<T> : IDfaMatcher<T>
{
    private readonly T[] items;
    private int index;
    private bool acceptPrefixes;

    public SequenceMatcher(IEnumerable<T> items)
        : this(items, false)
    {
    }

    public SequenceMatcher(IEnumerable<T> items, bool acceptPrefixes)
    {
        this.items = items.ToArray();
        this.index = -1;
        this.acceptPrefixes = acceptPrefixes;
    }

    public void Reset()
    {
        index = -1;
    }

    public bool IsFinal()
    {
        if (acceptPrefixes)
        {
            return index + 1 >= items.Length;
        }
        else
        {
            return index + 1 == items.Length;
        }
    }

    public bool Next(T p)
    {
        var next = index + 1;
        if (next >= items.Length)
        {
            if (acceptPrefixes)
            {
                index = next;
                return true;
            }
            else
            {
                return false;
            }
        }

        if (Equals(items[next], p))
        {
            index = next;
            return true;
        }

        return false;
    }

    public void Pop()
    {
        if (index == -1)
        {
            throw new InvalidOperationException();
        }
        --index;
    }
}