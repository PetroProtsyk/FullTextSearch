using System;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    public interface ITermDictionary : IDisposable
    {
        /// <summary>
        /// Get all posting lists that match pattern
        /// </summary>
        IEnumerable<DictionaryTerm> GetTerms(ITermMatcher<char> matcher);
    }
}
