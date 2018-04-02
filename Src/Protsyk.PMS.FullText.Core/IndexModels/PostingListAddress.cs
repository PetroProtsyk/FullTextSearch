using System;

namespace Protsyk.PMS.FullText.Core
{
    public struct PostingListAddress : IEquatable<PostingListAddress>
    {
        public static PostingListAddress Null = new PostingListAddress(-1);

        public readonly long Offset;

        public PostingListAddress(long offset)
        {
            this.Offset = offset;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            return obj is PostingListAddress && Equals((PostingListAddress)obj);
        }

        public bool Equals(PostingListAddress other)
        {
            return Offset == other.Offset;
        }

        public override int GetHashCode()
        {
            return Offset.GetHashCode();
        }
    }
}
