using System;

namespace Protsyk.PMS.FullText.Core
{
    internal static class PostingListIOFactory
    {
        public static IOccurrenceWriter CreateWriter(string readerType, string folder, string fileName)
        {
            if (readerType == PersistentIndexName.DefaultValue || readerType == PostingListWriter.Id)
            {
                return new PostingListWriter(folder, fileName);
            }

            if (readerType == PostingListBinaryWriter.Id)
            {
                return new PostingListBinaryWriter(folder, fileName);
            }

            if (readerType == PostingListBinaryDeltaWriter.Id)
            {
                return new PostingListBinaryDeltaWriter(folder, fileName);
            }

            if (readerType == PostingListVarIntDeltaWriter.Id)
            {
                return new PostingListVarIntDeltaWriter(folder, fileName);
            }

            throw new NotSupportedException($"Not supported Posting Type {readerType}");
        }

        public static IOccurrenceReader CreateReader(string readerType, string folder, string fileName)
        {
            if (readerType == PersistentIndexName.DefaultValue || readerType == PostingListWriter.Id)
            {
                return new PostingListReader(folder, fileName);
            }

            if (readerType == PostingListBinaryWriter.Id)
            {
                return new PostingListBinaryReader(folder, fileName);
            }

            if (readerType == PostingListBinaryDeltaWriter.Id)
            {
                return new PostingListBinaryDeltaReader(folder, fileName);
            }

            if (readerType == PostingListVarIntDeltaWriter.Id)
            {
                return new PostingListVarIntDeltaReader(folder, fileName);
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
}
