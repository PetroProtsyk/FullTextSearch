using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core
{
    public static class QueryFactory
    {
        public static ISearchQuery OrMultiQuery(IEnumerable<ISearchQuery> subqueries)
        {
            return new OrMultiQuery(subqueries.ToArray());
        }

        public static ISearchQuery TermQuery(IPostingList postings)
        {
            return new TermQuery(postings);
        }
    }
}
