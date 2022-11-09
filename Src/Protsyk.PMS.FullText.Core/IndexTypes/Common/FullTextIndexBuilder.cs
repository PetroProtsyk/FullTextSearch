using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Protsyk.PMS.FullText.Core
{
    public abstract class FullTextIndexBuilder : IIndexBuilder
    {
        // Default and the first field id for searchable fields
        private const int DefaultFieldId = 1;
        // Temporary in-memory inverted index
        private SortedDictionary<string, List<Occurrence>> temp;
        private IFullTextIndexHeader header;
        private long nextId;

        public FullTextIndexBuilder()
        {
        }

        public void Start()
        {
            temp = new SortedDictionary<string, List<Occurrence>>(StringComparer.Ordinal);
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
            AddTerms(id, DefaultFieldId, TokenizeReader(id, DefaultFieldId, new StringReader(text)));
            if (metadata != null)
            {
                AddFields(id, metadata);
            }
        }

        public void AddFile(string fileName, string metadata)
        {
            var id = (ulong)Interlocked.Increment(ref nextId);
            AddTerms(id, DefaultFieldId, TokenizeFile(id, fileName));
            AddFields(id, metadata);
        }

        public void AddCompound(IInputDocument document)
        {
           ArgumentNullException.ThrowIfNull(document);

           var id = (ulong)Interlocked.Increment(ref nextId);
           if (document.Metadata != null)
           {
               for (int i=0; i<document.Fields.Count; ++i)
               {
                    var reader = document.Fields[i].GetTextReader();
                    AddTerms(id, DefaultFieldId + (ulong)i, TokenizeReader(id, DefaultFieldId + (ulong)i, reader));
               }
               AddFields(id, document.Metadata);
           }
        }

        private IEnumerable<ScopedToken> TokenizeFile(ulong id, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = new StreamReader(stream))
                {
                    foreach (var token in TokenizeReader(id, DefaultFieldId, reader))
                    {
                        yield return token;
                    }
                }
            }
        }

        private IEnumerable<ScopedToken> TokenizeReader(ulong id, ulong fieldId, TextReader reader)
        {
            using (var wrapper = new TextReaderSink(reader, GetTextWriter(id, fieldId)))
            {
                foreach (var token in TokenizeReader(wrapper))
                {
                    yield return token;
                }
            }
        }

        private IEnumerable<ScopedToken> TokenizeReader(TextReader reader)
        {
            using (var tokenizer = new BasicTokenizer(header.MaxTokenSize))
            {
                foreach (var token in tokenizer.Tokenize(reader))
                {
                    yield return token;
                }
            }
        }

        private void AddTerms(ulong id, ulong fieldId, IEnumerable<ScopedToken> terms)
        {
            var positions = new List<TextPosition>();
            ulong tokenId = 0;
            foreach (var token in terms)
            {
                var term = token.AsString();
                if (!temp.TryGetValue(term, out var postingList))
                {
                    postingList = new List<Occurrence>();
                    temp.Add(term, postingList);
                }

                positions.Add(TextPosition.P(token.CharOffset, token.Length));
                postingList.Add(Occurrence.O(id, fieldId, ++tokenId));
            }

            AddDocVector(id, fieldId, positions);
        }

        public IndexBuilderStatistics StopAndWait()
        {
            long termCount = 0;
            long occCount = 0;

            foreach (var term in temp)
            {
                var postingListAddress = AddOccurrences(term.Key, term.Value.OrderBy(o => o));
                AddTerm(term.Key, postingListAddress);
                ++termCount;
                occCount += term.Value.Count;

                // TODO: Term and occurrences can be released from memory now
            }
            UpdateIndexHeader();

            DoStop();

            return new IndexBuilderStatistics
            {
                Terms = termCount,
                Occurrences = occCount
            };
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

        protected abstract void AddDocVector(ulong id, ulong fieldId, IEnumerable<TextPosition> positions);

        protected abstract void AddFields(ulong id, string jsonData);

        protected abstract TextWriter GetTextWriter(ulong id, ulong fieldId);

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

        #region Types
        // From MSDN (https://docs.microsoft.com/en-us/dotnet/api/system.io.textreader)
        // A derived class must minimally implement the Peek() and Read() methods to make a useful instance of TextReader.
        private class TextReaderSink : TextReader
        {
            private readonly TextReader reader;
            private readonly TextWriter sink;

            public TextReaderSink(TextReader reader, TextWriter sink)
            {
                this.reader = reader;
                this.sink = sink;
            }

            public override int Peek()
            {
                return reader.Peek();
            }

            public override int Read()
            {
                var result = reader.Read();
                if (result >= 0)
                {
                    sink.Write((char)result);
                }
                return result;
            }

            public override int Read(char[] buffer, int index, int count)
            {
                var read = reader.Read(buffer, index, count);
                if (read > 0)
                {
                    sink.Write(buffer, index, read);
                }
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    sink?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
        #endregion
    }
}
