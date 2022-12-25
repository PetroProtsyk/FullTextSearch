using System;

namespace Protsyk.PMS.FullText.Core;

public readonly struct PostingListAddress : IEquatable<PostingListAddress>
{
    public static PostingListAddress Null = new PostingListAddress(-1);

    public readonly long Offset;

    public PostingListAddress(long offset)
    {
        this.Offset = offset;
    }

    public override bool Equals(object obj)
    {
        return obj is PostingListAddress other && Equals(other);
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
