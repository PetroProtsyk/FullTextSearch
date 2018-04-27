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
        private PostingListWriter occurrenceWriter;
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
            occurrenceWriter = new PostingListWriter(Folder, PersistentIndex.FileNamePostingLists);
            dictionaryWriter = new PersistentDictionary(Folder, PersistentIndex.FileNameDictionary, PersistentIndex.FileNamePostingLists);
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
            var offsetStart = occurrenceWriter.StartList(term);
            foreach (var occurrence in occurrences)
            {
                occurrenceWriter.AddOccurrence(occurrence);
            }
            var offsetEnd = occurrenceWriter.EndList();

            return new PostingListAddress(offsetStart);
        }

        protected override IFullTextIndexHeader GetIndexHeader()
        {
            return indexInfo.Read() ?? new IndexHeaderData
            {
                Type = $"{nameof(PersistentIndex)} {name.FieldsType}",
                MaxTokenSize = MaxTokenSize,
                NextDocumentId = 0,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };
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
