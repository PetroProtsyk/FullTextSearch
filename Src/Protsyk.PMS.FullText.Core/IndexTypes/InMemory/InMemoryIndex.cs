using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core
{
    internal class InMemoryIndex : ITermDictionary, IPostingLists, IIndexName, IFullTextIndex, IMetadataStorage<string>
    {
        #region Fields
        private const int MaxTokenSize = 64;

        private readonly ConcurrentDictionary<string, PostingListAddress> data = new ConcurrentDictionary<string, PostingListAddress>();
        private readonly ConcurrentDictionary<PostingListAddress, Occurrence[]> postingLists = new ConcurrentDictionary<PostingListAddress, Occurrence[]>();
        private readonly ConcurrentDictionary<ulong, string> fields = new ConcurrentDictionary<ulong, string>();
        #endregion

        #region Constructor
        public InMemoryIndex()
        {
            Header = new IndexHeaderData
            {
                Type = nameof(InMemoryIndex),
                MaxTokenSize = MaxTokenSize,
                NextDocumentId = 0,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
            };
        }
        #endregion

        #region IFullTextIndex
        public ITermDictionary Dictionary => this;

        public IPostingLists PostingLists => this;

        public IMetadataStorage<string> Fields => null;

        public IFullTextIndexHeader Header { get; internal set; }

        IEnumerable<DictionaryTerm> IFullTextIndex.GetTerms(ITermMatcher matcher)
        {
            return Dictionary.GetTerms(matcher);
        }

        public ITermMatcher CompilePattern(string pattern)
        {
            using (var compiler = new FullTextQueryCompiler(this))
            {
                return compiler.CompilePattern(pattern);
            }
        }

        public ISearchQuery Compile(string query)
        {
            using (var compiler = new FullTextQueryCompiler(this))
            {
                return compiler.Compile(query);
            }
        }
        #endregion

        #region IMetadataStorage
        public string GetMetadata(ulong id)
        {
            return fields[id];
        }

        public void SaveMetadata(ulong id, string data)
        {
            fields.AddOrUpdate(id, data, (a, b) => data);
        }
        #endregion

        #region ITermDictionary
        public IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher)
        {
            var dfaMatcher = matcher.ToDfaMatcher();
            foreach (var term in data)
            {
                if (dfaMatcher.IsMatch(term.Key))
                {
                    yield return new DictionaryTerm(term.Key, term.Value);
                }
            }
        }
        #endregion

        #region IPostingLists
        public IPostingList Get(PostingListAddress address)
        {
            return new PostingListArray(postingLists[address]);
        }
        #endregion

        #region IndexBuilder
        public PostingListAddress AddOccurrences(string term, IEnumerable<Occurrence> occurrences)
        {
            var address = new PostingListAddress(postingLists.Count);
            if (!postingLists.TryAdd(address, occurrences.ToArray()))
            {
                throw new InvalidOperationException();
            }
            return address;
        }

        public void AddTerm(string term, PostingListAddress address)
        {
            if (!data.TryAdd(term, address))
            {
                throw new DuplicateTermException(term);
            }
        }

        public void AddFields(ulong id, string jsonData)
        {
            if (!fields.TryAdd(id, jsonData))
            {
                throw new InvalidOperationException();
            }
        }
        #endregion

        #region IDisposable
        public void Dispose() { }
        #endregion
    }
}
