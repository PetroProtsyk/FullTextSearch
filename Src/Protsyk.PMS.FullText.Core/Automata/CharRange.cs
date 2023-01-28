using System.Diagnostics;

namespace Protsyk.PMS.FullText.Core.Automata;

[DebuggerDisplay("[{start}, {end}]")]
public readonly struct CharRange : IEquatable<CharRange>
{
    public static CharRange Empty = new CharRange(int.MinValue, int.MinValue);

    public readonly int start;
    public readonly int end;

    public CharRange(int start, int end)
    {
        if (end < start)
            throw new ArgumentException(nameof(end));

        this.start = start;
        this.end = end;
    }

    public bool Equals(CharRange other)
    {
        return (start == other.start) && (end == other.end);
    }

    public override bool Equals(object obj)
    {
        return obj is CharRange other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(start, end);
    }

    public override string ToString()
    {
        if (start == end)
        {
            return FormatChar(start);
        }
        else
        {
            return $"[{FormatChar(start)}-{FormatChar(end)}]";
        }
    }

    private static string FormatChar(int code)
    {
        if (code > 32 && code < 127)
        {
            return $"{(char)code}";
        }

        if (code == char.MaxValue)
        {
            // Infinity sign
            return "\u221E";
        }

        return $"U+{code,4:X4}";
    }

    public bool Intersects(CharRange other)
    {
        return (start >= other.start && start <= other.end)
            || (end >= other.start && end <= other.end);
    }

    public bool Contains(char c)
    {
        return ((start <= (int)c) && ((int)c <= end));
    }

    public static CharRange SingleChar(char v)
    {
        return new CharRange(v, v);
    }

    public CharRange Intersect(CharRange other)
    {
        int s = Math.Max(start, other.start);
        int e = Math.Min(end, other.end);
        if (s > e)
        {
            return Empty;
        }
        return new CharRange(s, e);
    }
}

public static class CharRangeExtensions
{
    public static IEnumerable<CharRange> Disjoin(this IEnumerable<CharRange> ranges)
    {
        var pqueue = new SortedSet<CharRange>(ranges, Comparer<CharRange>.Create((x, y) =>
        {
            if (x.start == y.start)
            {
                return y.end - x.end;
            }
            return y.start - x.start;
        }));


        while (pqueue.Count > 1)
        {
            var current = pqueue.Max;
            pqueue.Remove(current);

            var next = pqueue.Max;
            if (current.end == next.start)
            {
                pqueue.Remove(next);

                if (current.start <= current.end - 1)
                {
                    pqueue.Add(new CharRange(current.start, current.end - 1));
                }

                pqueue.Add(new CharRange(next.start, next.start));

                if (next.start + 1 <= next.end)
                {
                    pqueue.Add(new CharRange(next.start + 1, next.end));
                }
            }
            else if (current.end > next.start)
            {
                pqueue.Remove(next);

                if (current.start < next.start)
                {
                    var first = new CharRange(current.start, next.start - 1);
                    pqueue.Add(first);
                }

                var firstEnd = Math.Min(current.end, next.end);
                var lastEnd = Math.Max(current.end, next.end);

                if (next.start <= firstEnd)
                {
                    var second = new CharRange(next.start, firstEnd);
                    pqueue.Add(second);
                }

                if (firstEnd + 1 < lastEnd)
                {
                    var third = new CharRange(firstEnd + 1, lastEnd);
                    pqueue.Add(third);
                }
            }
            else
            {
                yield return current;
            }
        }

        if (pqueue.Count > 0)
        {
            yield return pqueue.Max;
        }
    }
}
