using System;
using System.IO;
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

        IEnumerable<TextPosition> GetPositions(ulong docId, ulong fieldId);

        TextReader GetText(ulong docId, ulong fieldId);

        ITermMatcher CompilePattern(string pattern);

        ISearchQuery Compile(string query);
    }
}
