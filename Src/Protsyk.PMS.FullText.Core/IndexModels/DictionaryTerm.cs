namespace Protsyk.PMS.FullText.Core;

public readonly struct DictionaryTerm : IEquatable<DictionaryTerm>
{
    public readonly PostingListAddress Value;
    public readonly string Key;

    public DictionaryTerm(string key, PostingListAddress value)
    {
        this.Key = key;
        this.Value = value;
    }

    public override bool Equals(object obj)
    {
        return obj is DictionaryTerm other && Equals(other);
    }

    public bool Equals(DictionaryTerm other)
    {
        return string.Equals(Key, other.Key, StringComparison.Ordinal) && Value.Equals(other.Value);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Key);
    }
}