using System;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    public interface IFullTextIndex : IDisposable
    {
        IFullTextIndexHeader Header { get; }

        ITermDictionary Dictionary { get; }

        IPostingLists PostingLists { get; }

        IMetadataStorage<string> Fields { get; }

        IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher);

        ITermMatcher CompilePattern(string pattern);

        ISearchQuery Compile(string query);
    }
}
