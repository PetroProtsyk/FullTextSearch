using System;
using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core
{
    public static class IFullTextIndexExtensions
    {
        public static void Visit(this IFullTextIndex index, IIndexVisitor visitor)
        {
            foreach (var term in index.GetTerms("*"))
            {
                if (!visitor.VisitTerm(term))
                {
                    break;
                }
            }
        }

        public static IEnumerable<DictionaryTerm> GetTerms(this IFullTextIndex index, string wildcardPattern)
        {
            var matcher = default(ITermMatcher<char>); //TODO: new WildcardMatcher(wildcardPattern, index.Header.MaxTokenSize);
            return index.GetTerms(matcher);
        }

        public static IEnumerable<DictionaryTerm> GetTerms(this IFullTextIndex index, string word, int distance)
        {
            var matcher = default(ITermMatcher<char>); //TODO: new LevenshteinMatcher(word, distance);
            return index.GetTerms(matcher);
        }

        public static IEnumerable<IPostingList> GetPostingLists(this IFullTextIndex index, string wildcardPattern)
        {
            var matcher = default(ITermMatcher<char>); //TODO: new WildcardMatcher(wildcardPattern, index.Header.MaxTokenSize);
            return index.GetTerms(matcher).Select(p => index.PostingLists.Get(p.Value));
        }

        public static IPostingList GetPostingList(this IFullTextIndex index, string term)
        {
            return GetPostingLists(index, term).Single();
        }
    }
}
