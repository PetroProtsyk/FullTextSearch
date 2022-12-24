using System;

namespace Protsyk.PMS.FullText.Core;

public interface IFullTextIndexHeader
{
    string Type { get; }

    int MaxTokenSize { get; }

    ulong NextDocumentId { get; set; }

    DateTime CreatedDate { get; set; }

    DateTime ModifiedDate { get; set; }
}

public class IndexHeaderData : IFullTextIndexHeader
{
    public string Type { get; set; }
    public int MaxTokenSize { get; set; }
    public ulong NextDocumentId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }

    public IndexHeaderData()
    {
    }

    public IndexHeaderData Clone()
    {
        return CopyFrom(this);
    }

    public static IndexHeaderData CopyFrom(IFullTextIndexHeader header)
    {
        return new IndexHeaderData
        {
            Type = header.Type,
            MaxTokenSize = header.MaxTokenSize,
            NextDocumentId = header.NextDocumentId,
            CreatedDate = header.CreatedDate,
            ModifiedDate = header.ModifiedDate,
        };
    }
}
