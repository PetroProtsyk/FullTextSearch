using System.Linq;

namespace Protsyk.PMS.FullText.Core;

public static class SearchQueryExtensions
{
    public static IEnumerable<IMatch> AsEnumerable(this ISearchQuery query)
    {
        var match = query.NextMatch();
        while (match != null)
        {
            yield return match;
            match = query.NextMatch();
        }
    }

    public static string ExecuteToString(this ISearchQuery query)
    {
        return string.Join(", ", query.AsEnumerable().Select(m => m.ToString()));
    }
}
