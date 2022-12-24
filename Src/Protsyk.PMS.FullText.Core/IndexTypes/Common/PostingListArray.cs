using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core;

public class PostingListArray : IPostingList
{
    private readonly Occurrence[] values;

    public PostingListArray(Occurrence[] values)
    {
        this.values = values;
    }

    public override string ToString()
    {
        return string.Join(", ", values);
    }

    public IEnumerator<Occurrence> GetEnumerator()
    {
        return values.AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static IPostingList Parse(string text)
    {
        return new PostingListArray(text
                                        .Split(new string[] {", "}, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(Occurrence.Parse)
                                        .ToArray());
    }
}
