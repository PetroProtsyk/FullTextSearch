using System.Globalization;

namespace Protsyk.PMS.FullText.Core;

public readonly struct Occurrence : IEquatable<Occurrence>, IComparable<Occurrence>
{
    /// <summary>
    /// Not valid Id
    /// </summary>
    public const ulong NoId = 0;

    /// <summary>
    /// Empty occurrence
    /// </summary>
    public static readonly Occurrence Empty = new(NoId, NoId, NoId);

    public Occurrence(ulong documentId, ulong fieldId, ulong tokenId)
    {
        DocumentId = documentId;
        FieldId = fieldId;
        TokenId = tokenId;
    }

    /// <summary>
    /// Document Id
    /// </summary>
    public ulong DocumentId { get; }

    /// <summary>
    /// Fields Id
    /// </summary>
    public ulong FieldId { get; }

    /// <summary>
    /// Token Id
    /// </summary>
    public ulong TokenId { get; }

    #region Static Methods

    public static Occurrence Parse(ReadOnlySpan<char> text)
    {
        if (text is [ '[', .. var inner, ']' ])
        {
            var splitter = new StringSplitter(inner, ',');

            if (splitter.TryRead(out var t0) && ulong.TryParse(t0, NumberStyles.None, CultureInfo.InvariantCulture, out ulong docId) &&
                splitter.TryRead(out var t1) && ulong.TryParse(t1, NumberStyles.None, CultureInfo.InvariantCulture, out ulong fieldId) &&
                splitter.TryRead(out var t2) && ulong.TryParse(t2, NumberStyles.None, CultureInfo.InvariantCulture, out ulong tokenId))
            {
                return new Occurrence(docId, fieldId, tokenId);
            }           
        }

        throw new InvalidOperationException($"Occurrence text has invalid format. Was {text}");
    }

    #endregion   

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"[{DocumentId},{FieldId},{TokenId}]");
    }
   
    public bool Equals(Occurrence other)
    {
        return DocumentId == other.DocumentId &&
               FieldId == other.FieldId && 
               TokenId == other.TokenId;
    }

    public override bool Equals(object obj)
    {
        return obj is Occurrence other && Equals(other);
    }

    public override int GetHashCode() => HashCode.Combine(DocumentId, FieldId, TokenId);

    #region IComparable
    public int CompareTo(Occurrence other)
    {
        var documentIdComparison = DocumentId.CompareTo(other.DocumentId);
        if (documentIdComparison != 0)
        {
            return documentIdComparison;
        }

        var fieldIdComparison = FieldId.CompareTo(other.FieldId);
        if (fieldIdComparison != 0)
        {
            return fieldIdComparison;
        }

        return TokenId.CompareTo(other.TokenId);
    }
    #endregion

    #region Operators
    public static bool operator <(Occurrence l, Occurrence r)
    {
        return l.CompareTo(r) < 0;
    }

    public static bool operator >(Occurrence l, Occurrence r)
    {
        return l.CompareTo(r) > 0;
    }

    public static bool operator ==(Occurrence l, Occurrence r)
    {
        return l.Equals(r);
    }

    public static bool operator !=(Occurrence l, Occurrence r)
    {
        return !l.Equals(r);
    }
    #endregion
}
