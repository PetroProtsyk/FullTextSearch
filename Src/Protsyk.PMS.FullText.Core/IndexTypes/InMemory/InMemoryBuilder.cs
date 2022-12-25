using System.Collections.Generic;
using System.IO;

namespace Protsyk.PMS.FullText.Core;

internal sealed class InMemoryBuilder : FullTextIndexBuilder
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

    protected override void AddDocVector(ulong id, ulong fieldId, IEnumerable<TextPosition> positions)
    {
        Index.AddDocVector(id, fieldId, positions);
    }

    protected override TextWriter GetTextWriter(ulong id, ulong fieldId)
    {
        return Index.GetTextWriter(id, fieldId);
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
