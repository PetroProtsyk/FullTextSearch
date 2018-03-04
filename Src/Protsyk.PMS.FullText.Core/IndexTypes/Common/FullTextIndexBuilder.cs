using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Protsyk.PMS.FullText.Core
{
    public abstract class FullTextIndexBuilder : IIndexBuilder
    {
        private SortedDictionary<string, List<Occurrence>> temp;
        private IFullTextIndexHeader header;
        private long nextId;

        public FullTextIndexBuilder()
        {
        }

        public void Start()
        {
            temp = new SortedDictionary<string, List<Occurrence>>();
            DoStart();

            header = GetIndexHeader();
            nextId = (long)header.NextDocumentId;
        }

        public void AddText(string text)
        {
            AddText(text, null);
        }

        public void AddText(string text, string metadata)
        {
            var id = (ulong)Interlocked.Increment(ref nextId);
            AddTerms(id, TokenizeReader(new StringReader(text)));
            if (metadata != null)
            {
                AddFields(id, metadata);
            }
        }

        public void AddFile(string fileName, string metadata)
        {
            var id = (ulong)Interlocked.Increment(ref nextId);
            AddTerms(id, TokenizeFile(fileName));
            AddFields(id, metadata);
        }


        private IEnumerable<string> TokenizeFile(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = new StreamReader(stream))
                {
                    foreach (var term in TokenizeReader(reader))
                    {
                        yield return term;
                    }
                }
            }
        }


        private IEnumerable<string> TokenizeReader(TextReader reader)
        {
            using (var tokenizer = new BasicTokenizer(header.MaxTokenSize))
            {
                foreach (var term in tokenizer.Tokenize(reader).Select(t => t.AsString()))
                {
                    yield return term;
                }
            }
        }

        private void AddTerms(ulong id, IEnumerable<string> terms)
        {
            ulong tokenId = 0;
            foreach (var term in terms)
            {
                List<Occurrence> postingList;
                if (!temp.TryGetValue(term, out postingList))
                {
                    postingList = new List<Occurrence>();
                    temp.Add(term, postingList);
                }
                postingList.Add(Occurrence.O(id, 1, ++tokenId));
            }
        }

        public void StopAndWait()
        {
            foreach (var term in temp)
            {
                var postingListAddress = AddOccurrences(term.Key, term.Value.OrderBy(o => o));
                AddTerm(term.Key, postingListAddress);
            }
            UpdateIndexHeader();

            DoStop();
        }

        private void UpdateIndexHeader()
        {
            header.NextDocumentId = (ulong)nextId;
            header.ModifiedDate = DateTime.UtcNow;

            UpdateIndexHeader(header);
        }

        protected virtual void DoStart() { }

        protected virtual void DoStop() { }

        protected abstract PostingListAddress AddOccurrences(string term, IEnumerable<Occurrence> occurrences);

        protected abstract void AddTerm(string term, PostingListAddress address);

        protected abstract void AddFields(ulong id, string jsonData);

        protected abstract void UpdateIndexHeader(IFullTextIndexHeader header);

        protected abstract IFullTextIndexHeader GetIndexHeader();

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
