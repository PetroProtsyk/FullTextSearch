using System.IO;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core;

public static class PostingListIOFactory
{
    public static IEnumerable<string> GetNames()
    {
        yield return PostingListWriter.Id;
        yield return PostingListBinaryWriter.Id;
        yield return PostingListBinaryDeltaWriter.Id;
        yield return PostingListVarIntDeltaWriter.Id;
        yield return PostingListPackedIntDeltaWriter.Id;
    }

    public static IOccurrenceWriter CreateWriter(string readerType, string folder, string fileName)
    {
        return CreateWriter(readerType, new FileStorage(Path.Combine(folder, fileName)));
    }

    public static IOccurrenceWriter CreateWriter(string readerType, IPersistentStorage storage)
    {
        if (readerType == PersistentIndexName.DefaultValue || readerType == PostingListWriter.Id)
        {
            return new PostingListWriter(storage);
        }

        if (readerType == PostingListBinaryWriter.Id)
        {
            return new PostingListBinaryWriter(storage);
        }

        if (readerType == PostingListBinaryDeltaWriter.Id)
        {
            return new PostingListBinaryDeltaWriter(storage);
        }

        if (readerType == PostingListVarIntDeltaWriter.Id)
        {
            return new PostingListVarIntDeltaWriter(storage);
        }

        if (readerType == PostingListPackedIntDeltaWriter.Id)
        {
            return new PostingListPackedIntDeltaWriter(storage);
        }

        throw new NotSupportedException($"Not supported Posting Type {readerType}");
    }

    public static IOccurrenceReader CreateReader(string readerType, string folder, string fileName)
    {
        return CreateReader(readerType, new FileStorage(Path.Combine(folder, fileName)));
    }

    public static IOccurrenceReader CreateReader(string readerType, IPersistentStorage storage)
    {
        if (readerType == PersistentIndexName.DefaultValue || readerType == PostingListWriter.Id)
        {
            return new PostingListReader(storage);
        }

        if (readerType == PostingListBinaryWriter.Id)
        {
            return new PostingListBinaryReader(storage);
        }

        if (readerType == PostingListBinaryDeltaWriter.Id)
        {
            return new PostingListBinaryDeltaReader(storage);
        }

        if (readerType == PostingListVarIntDeltaWriter.Id)
        {
            return new PostingListVarIntDeltaReader(storage);
        }

        if (readerType == PostingListPackedIntDeltaWriter.Id)
        {
            return new PostingListPackedIntDeltaReader(storage);
        }

        throw new NotSupportedException($"Not supported Posting Type {readerType}");
    }

    public static string GetName(string fieldsType)
    {
        if (fieldsType == PersistentIndexName.DefaultValue)
        {
            return PostingListWriter.Id;
        }
        return fieldsType;
    }
}
