using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common.Compression;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PersistentDictionary : ITermDictionary, IDisposable
    {
        #region Fields
        private readonly TernaryDictionary<byte, long> dictionary;
        private readonly int maxTokenByteLength;
        private readonly ITextEncoding encoding;
        #endregion

        #region Constructor
        public PersistentDictionary(string folder,
                                    string fileNameDictionary,
                                    int maxTokenLength,
                                    ITextEncoding encoding)
            : this(new FileStorage(Path.Combine(folder, fileNameDictionary)),
                   maxTokenLength,
                   encoding)
        {
        }

        public PersistentDictionary(IPersistentStorage storage,
                                    int maxTokenLength,
                                    ITextEncoding encoding)
        {
            this.maxTokenByteLength = encoding.GetMaxEncodedLength(maxTokenLength);
            this.encoding = encoding;
            this.dictionary = new TernaryDictionary<byte, long>(storage);
        }
        #endregion

        #region ITermDictionary
        public IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher)
        {
            var decodingMatcher = new DecodingMatcher(matcher.ToDfaMatcher(), maxTokenByteLength, encoding);

            foreach (var term in dictionary.Match(decodingMatcher)
                                           .Select(p=>encoding.GetString(p.ToArray())))
            {
                yield return GetTerm(term);
            }
        }

        public DictionaryTerm GetTerm(string term)
        {
            long offset = 0;
            if (!dictionary.TryGet(encoding.GetBytes(term), out offset))
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
            if (!dictionary.AddOrGet(encoding.GetBytes(term), address.Offset, out long previosOffset))
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
