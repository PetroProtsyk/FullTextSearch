using System;
using System.Collections.Generic;
using System.Linq;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core
{
    public static class FullTextIndexExtensions
    {
        public static void Visit(this IFullTextIndex index, IIndexVisitor visitor)
        {
            foreach (var term in index.GetTerms(new DfaTermMatcher(new AnyMatcher<char>())))
            {
                if (!visitor.VisitTerm(term))
                {
                    break;
                }
            }
        }
    }
}
