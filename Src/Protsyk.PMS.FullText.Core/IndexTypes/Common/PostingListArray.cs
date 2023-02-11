using System.Collections;
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
        return values.GetEnumerator();
    }

    public static IPostingList Parse(string text)
    {
        var list = text.Split(", ", StringSplitOptions.RemoveEmptyEntries);
        var values = new Occurrence[list.Length];

        for (int i = 0; i < list.Length; i++)
        {
            values[i] = Occurrence.Parse(list[i]);
        }

        return new PostingListArray(values);
    }
}
