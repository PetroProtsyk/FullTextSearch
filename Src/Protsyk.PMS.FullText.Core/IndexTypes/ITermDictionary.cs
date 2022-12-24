using System;
using System.Collections.Generic;

using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core;

public interface ITermDictionary : IDisposable
{
    /// <summary>
    /// Get all posting lists that match pattern
    /// </summary>
    IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher);
}

public interface IUpdateTermDictionary : IDisposable
{
    IUpdate BeginUpdate();

    void AddTerm(string term, PostingListAddress address, Action<PostingListAddress> onDuplicate);
}
