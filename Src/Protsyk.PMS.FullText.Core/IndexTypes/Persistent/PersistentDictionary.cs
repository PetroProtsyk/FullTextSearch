using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PersistentDictionary : ITermDictionary, IDisposable
    {
        #region Fields
        private readonly TernaryDictionary<char, long> dictionary;
        #endregion

        #region Constructor

        public PersistentDictionary(string folder, string fileNameDictionary)
        {
            dictionary = new TernaryDictionary<char, long>(new FileStorage(Path.Combine(folder, fileNameDictionary)));
        }
        #endregion

        #region ITermDictionary
        public IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher)
        {
            foreach (var term in dictionary.Match(matcher.ToDfaMatcher())
                                           .Select(p=>new string(p.ToArray())))
            {
                yield return GetTerm(term);
            }
        }

        public DictionaryTerm GetTerm(string term)
        {
            long offset = 0;
            if (!dictionary.TryGet(term, out offset))
            {
                throw new InvalidOperationException();
            }

            return new DictionaryTerm(term, new PostingListAddress(offset));
        }
        #endregion

        #region Methods

        public IUpdate BeginUpdate()
        {
            return dictionary.StartUpdate();
        }

        public void AddTerm(string term, PostingListAddress address, Action<PostingListAddress> onDuplicate)
        {
            if (!dictionary.AddOrGet(term, address.Offset, out long previosOffset))
            {
                onDuplicate?.Invoke(new PostingListAddress(previosOffset));
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            dictionary?.Dispose();
        }
        #endregion
    }
}
