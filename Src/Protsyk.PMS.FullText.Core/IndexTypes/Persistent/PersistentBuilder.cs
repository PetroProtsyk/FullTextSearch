using System;
using System.Collections.Generic;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core
{
    public class PersistentBuilder : FullTextIndexBuilder
    {
        private const int AutoCommitThreshold = 1000;
        private const int MaxTokenSize = 64;

        private readonly PersistentIndexName name;
        private IMetadataStorage<string> fields;
        private IOccurrenceWriter occurrenceWriter;
        private PersistentDictionary dictionaryWriter;
        private PersistentIndexInfo indexInfo;
        private IUpdate dictionaryUpdate;
        private long updates;

        private string Folder => name.Folder;

        public PersistentBuilder(PersistentIndexName name)
        {
            this.name = name;
        }

        protected override void DoStart()
        {
            base.DoStart();
            indexInfo = new PersistentIndexInfo(Folder, PersistentIndex.FileNameInfo);
            fields = PersistentMetadataFactory.CreateStorage(name.FieldsType, Folder, PersistentIndex.FileNameFields);
            occurrenceWriter = PostingListReaderFactory.CreateWriter(name.PostingType, Folder, PersistentIndex.FileNamePostingLists);
            dictionaryWriter = new PersistentDictionary(Folder, PersistentIndex.FileNameDictionary);
            dictionaryUpdate = dictionaryWriter.BeginUpdate();
            updates = 0;
        }

        protected override void DoStop()
        {
            dictionaryUpdate?.Commit();
            DisposeObjects();
        }

        protected override void AddFields(ulong id, string jsonData)
        {
            fields.SaveMetadata(id, jsonData);
        }

        protected override void AddTerm(string term, PostingListAddress address)
        {
            dictionaryWriter.AddTerm(term, address, existingList => occurrenceWriter.UpdateNextList(existingList, address));

            ++updates;
            if (updates > AutoCommitThreshold)
            {
                dictionaryUpdate.Commit();
                dictionaryUpdate = dictionaryWriter.BeginUpdate();
                updates = 0;
            }
        }

        protected override PostingListAddress AddOccurrences(string term, IEnumerable<Occurrence> occurrences)
        {
            occurrenceWriter.StartList(term);
            foreach (var occurrence in occurrences)
            {
                occurrenceWriter.AddOccurrence(occurrence);
            }
            return occurrenceWriter.EndList();
        }

        protected override IFullTextIndexHeader GetIndexHeader()
        {
            var header = indexInfo.Read();
            if (header == null)
            {
                var fieldsType = PersistentMetadataFactory.GetName(name.FieldsType);
                var postingType = PostingListReaderFactory.GetName(name.PostingType);

                header = new IndexHeaderData
                {
                    Type = $"{nameof(PersistentIndex)} {fieldsType} {postingType}",
                    MaxTokenSize = MaxTokenSize,
                    NextDocumentId = 0,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow,
                };
            }
            else
            {
                var types = header.Type.Split(' ');
                if (types[0] != nameof(PersistentIndex))
                {
                    throw new InvalidOperationException("Index type and name mismatch");
                }

                if (types[1] != name.FieldsType)
                {
                    throw new InvalidOperationException("Index type and name mismatch");
                }

                if (types[2] != name.PostingType)
                {
                    throw new InvalidOperationException("Index type and name mismatch");
                }
            }

            return header;
        }

        protected override void UpdateIndexHeader(IFullTextIndexHeader header)
        {
            indexInfo.Write(header);
        }

        private void DisposeObjects()
        {
            indexInfo?.Dispose();
            dictionaryUpdate?.Dispose();
            dictionaryWriter?.Dispose();
            occurrenceWriter?.Dispose();
            fields?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeObjects();
            }

            base.Dispose(disposing);
        }
    }
}
