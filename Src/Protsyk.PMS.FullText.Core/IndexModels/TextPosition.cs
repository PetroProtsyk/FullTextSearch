using System.Globalization;

namespace Protsyk.PMS.FullText.Core;

public readonly struct TextPosition : IEquatable<TextPosition>, IComparable<TextPosition>
{
    public static readonly TextPosition Empty = new(0, 0);
   
    public TextPosition(int offset, int length)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Offset = offset;
        Length = length;
    }

    public int Offset { get; }

    public int Length { get; }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"[{Offset},{Length}]");
    }

    public static TextPosition Parse(ReadOnlySpan<char> text)
    {
        int commaIndex;

        if (text is ['[', .. var inner, ']' ] 
            && (commaIndex = inner.IndexOf(',')) > -1
            && int.TryParse(inner.Slice(0, commaIndex), NumberStyles.None, CultureInfo.InvariantCulture, out int offset)
            && int.TryParse(inner.Slice(commaIndex + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int length))
        {
            return new TextPosition(offset, length);
        }
        else
        {
            throw new InvalidOperationException($"Occurrence text has invalid format. Was {text}");
        }
    }

    #region IEquatable<TextPosition>

    public bool Equals(TextPosition other)
    {
        return Offset == other.Offset &&
               Length == other.Length;
    }

    public override bool Equals(object obj)
    {           
        return obj is TextPosition other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Offset, Length);
    }

    #endregion

    #region IComparable
    public int CompareTo(TextPosition other)
    {
        var offsetComparison = Offset.CompareTo(other.Offset);
        if (offsetComparison != 0)
        {
            return offsetComparison;
        }

        return Length.CompareTo(other.Length);
    }
    #endregion

    #region Operators
    public static bool operator <(TextPosition l, TextPosition r)
    {
        return l.CompareTo(r) < 0;
    }

    public static bool operator >(TextPosition l, TextPosition r)
    {
        return l.CompareTo(r) > 0;
    }

    public static bool operator ==(TextPosition l, TextPosition r)
    {
        return l.Equals(r);
    }

    public static bool operator !=(TextPosition l, TextPosition r)
    {
        return !l.Equals(r);
    }
    #endregion
}
