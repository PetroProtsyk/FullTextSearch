using System.Collections.Concurrent;
using System.IO;
using System.Linq;

using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core;

internal sealed class InMemoryIndex : ITermDictionary, IPostingLists, IIndexName, IFullTextIndex, IMetadataStorage<string>
{
    #region Fields
    private const int MaxTokenSize = 64;

    private readonly ConcurrentDictionary<string, PostingListAddress> data = new();
    private readonly ConcurrentDictionary<PostingListAddress, Occurrence[]> postingLists = new();
    private readonly ConcurrentDictionary<ulong, string> fields = new();
    private readonly ConcurrentDictionary<(ulong, ulong), PostingListAddress> posIndex = new();
    private readonly ConcurrentDictionary<PostingListAddress, TextPosition[]> positions = new();
    private readonly ConcurrentDictionary<(ulong, ulong), string> docTexts = new();
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

    IEnumerable<TextPosition> IFullTextIndex.GetPositions(ulong docId, ulong fieldId)
    {
        var vectorId = (docId, fieldId);
        if (!posIndex.TryGetValue(vectorId, out var address))
        {
            throw new Exception($"Not found document field id:{docId}, field:{fieldId}");
        }

        if (!positions.TryGetValue(address, out var result))
        {
            throw new Exception($"Not found positions {address.Offset}");
        }

        return result;
    }

    TextReader IFullTextIndex.GetText(ulong docId, ulong fieldId)
    {
        var textId = (docId, fieldId);
        if (!docTexts.TryGetValue(textId, out var text))
        {
            throw new Exception($"Not found document field id:{docId}, field:{fieldId}");
        }
        return new StringReader(text);
    }

    public ITermMatcher CompilePattern(string pattern)
    {
        using var compiler = new FullTextQueryCompiler(this);

        return compiler.CompilePattern(pattern);
    }

    public ISearchQuery Compile(string query)
    {
        using var compiler = new FullTextQueryCompiler(this);

        return compiler.Compile(query);
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

    public void AddDocVector(ulong id, ulong fieldId, IEnumerable<TextPosition> pos)
    {
        var address = new PostingListAddress(posIndex.Count);
        var vectorId = (id, fieldId);
        if (!posIndex.TryAdd(vectorId, address))
        {
            throw new Exception($"Duplicate document field id:{id}, field:{fieldId}");
        }

        if (!positions.TryAdd(address, pos.ToArray()))
        {
            throw new Exception($"Duplicate address {address.Offset}");
        }
    }

    public TextWriter GetTextWriter(ulong id, ulong fieldId)
    {
        return new TextWriterWrapper((text)=>{
            var textId = (id, fieldId);
            docTexts.TryAdd(textId, text);
        });
    }

    private sealed class TextWriterWrapper : StringWriter
    {
        private readonly Action<string> whenDisposed;

        public TextWriterWrapper(Action<string> whenDisposed)
        {
            this.whenDisposed = whenDisposed;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                whenDisposed?.Invoke(ToString());
            }
            base.Dispose(disposing);
        }
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
    }
    #endregion
}
