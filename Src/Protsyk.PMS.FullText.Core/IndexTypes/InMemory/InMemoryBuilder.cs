using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core
{
    internal class InMemoryBuilder : FullTextIndexBuilder
    {
        private readonly InMemoryIndexName name;

        private InMemoryIndex Index => name.Index;

        public InMemoryBuilder(InMemoryIndexName name)
        {
            this.name = name;
        }

        protected override PostingListAddress AddOccurrences(string term, IEnumerable<Occurrence> occurrences)
        {
            return Index.AddOccurrences(term, occurrences);
        }

        protected override void AddTerm(string term, PostingListAddress address)
        {
            Index.AddTerm(term, address);
        }

        protected override void AddFields(ulong id, string jsonData)
        {
            Index.AddFields(id, jsonData);
        }

        protected override IFullTextIndexHeader GetIndexHeader()
        {
            return Index.Header;
        }

        protected override void UpdateIndexHeader(IFullTextIndexHeader header)
        {
            Index.Header = header;
        }
    }
}
