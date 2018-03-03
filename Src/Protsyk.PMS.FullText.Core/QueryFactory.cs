using System;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    public static class QueryFactory
    {
        public static ISearchQuery OrMultiQuery(IEnumerable<ISearchQuery> subqueries)
        {
            throw new NotImplementedException();
        }

        public static ISearchQuery TermQuery(IPostingList postingList)
        {
            throw new NotImplementedException();
        }
    }
}
