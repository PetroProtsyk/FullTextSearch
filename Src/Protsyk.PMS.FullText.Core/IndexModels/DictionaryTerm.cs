using System;

namespace Protsyk.PMS.FullText.Core
{
    public struct DictionaryTerm : IEquatable<DictionaryTerm>
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
            if (ReferenceEquals(null, obj))
                return false;

            return obj is DictionaryTerm && Equals((DictionaryTerm)obj);
        }

        public bool Equals(DictionaryTerm other)
        {
            return string.Equals(Key, other.Key) && Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return HashCombine.Combine(Value.GetHashCode(), Key?.GetHashCode() ?? 0);
        }
    }
}
