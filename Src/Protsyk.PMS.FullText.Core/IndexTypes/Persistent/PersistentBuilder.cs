using System;
using System.Collections.Generic;
using System.IO;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common.Compression;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class PersistentBuilder : FullTextIndexBuilder
    {
        private const int AutoCommitThreshold = 1000;
        private const int MaxTokenSize = 64;

        private readonly PersistentIndexName name;
        private IMetadataStorage<string> fields;
        private IOccurrenceWriter occurrenceWriter;
        private DeltaVarIntListWriter positionsWriter;
        private IUpdateTermDictionary dictionaryWriter;
        private IUpdateTermDictionary posIndexWriter;
        private PersistentIndexInfo indexInfo;
        private IUpdate dictionaryUpdate;
        private IUpdate posIndexUpdate;
        private long dictUpdates;
        private long posUpdates;

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
            occurrenceWriter = PostingListIOFactory.CreateWriter(name.PostingType, Folder, PersistentIndex.FileNamePostingLists);
            dictionaryWriter = PersistentDictionaryFactory.CreateWriter(name.DictionaryType, Folder, PersistentIndex.FileNameDictionary, MaxTokenSize, name.TextEncoding);
            dictionaryUpdate = dictionaryWriter.BeginUpdate();
            posIndexWriter = PersistentDictionaryFactory.CreateWriter(name.DictionaryType, Folder, PersistentIndex.FileNamePosIndex, PersistentIndex.PosIndexKeySize, name.TextEncoding);
            posIndexUpdate = posIndexWriter.BeginUpdate();
            positionsWriter = new DeltaVarIntListWriter(Folder, PersistentIndex.FileNamePositions);
            dictUpdates = 0;
            posUpdates = 0;
        }

        protected override void DoStop()
        {
            dictionaryUpdate?.Commit();
            posIndexUpdate?.Commit();
            DisposeObjects();
        }

        protected override void AddFields(ulong id, string jsonData)
        {
            fields.SaveMetadata(id, jsonData);
        }

        protected override void AddTerm(string term, PostingListAddress address)
        {
            dictionaryWriter.AddTerm(term, address, existingList => occurrenceWriter.UpdateNextList(existingList, address));

            ++dictUpdates;
            if (dictUpdates > AutoCommitThreshold)
            {
                dictionaryUpdate.Commit();
                dictionaryUpdate = dictionaryWriter.BeginUpdate();
                dictUpdates = 0;
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

        protected override void AddDocVector(ulong id, ulong fieldId, IEnumerable<TextPosition> positions)
        {
            var listStart = positionsWriter.StartList();
            foreach (var pos in positions)
            {
                positionsWriter.AddValue((ulong)pos.Offset + 1);
                positionsWriter.AddValue((ulong)pos.Offset + 1 +(ulong)pos.Length);
            }
            var listEnd = positionsWriter.EndList();

            var key = PersistentIndex.GetKeyForPositions('P', id, fieldId);
            posIndexWriter.AddTerm(key,
                                   new PostingListAddress(listStart),
                                   _ => throw new Exception("Not expected"));
            ++posUpdates;
            if (posUpdates > AutoCommitThreshold)
            {
                posIndexUpdate.Commit();
                posIndexUpdate = posIndexWriter.BeginUpdate();
                posUpdates = 0;
            }
        }

        protected override TextWriter GetTextWriter(ulong id, ulong fieldId)
        {
            var payload = positionsWriter.StartPayload();
            var key = PersistentIndex.GetKeyForPositions('T', id, fieldId);
            posIndexWriter.AddTerm(key,
                                   new PostingListAddress(payload),
                                   _ => throw new Exception("Not expected"));
            return positionsWriter.StartTextPayload();
        }

        protected override IFullTextIndexHeader GetIndexHeader()
        {
            var header = indexInfo.Read();

            var dictionaryType = PersistentDictionaryFactory.GetName(name.DictionaryType);
            var fieldsType = PersistentMetadataFactory.GetName(name.FieldsType);
            var postingType = PostingListIOFactory.GetName(name.PostingType);
            var textEncoding = TextEncodingFactory.GetName(name.TextEncoding);

            if (header == null)
            {
                header = new IndexHeaderData
                {
                    Type = $"{nameof(PersistentIndex)} {dictionaryType} {fieldsType} {postingType} {textEncoding}",
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

                if (types[1] != dictionaryType)
                {
                    throw new InvalidOperationException("Field type and name mismatch");
                }

                if (types[2] != fieldsType)
                {
                    throw new InvalidOperationException("Field type and name mismatch");
                }

                if (types[3] != postingType)
                {
                    throw new InvalidOperationException("Posting type and name mismatch");
                }

                if (types[4] != textEncoding)
                {
                    throw new InvalidOperationException("Text encoding type and name mismatch");
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
            posIndexUpdate?.Dispose();
            posIndexWriter?.Dispose();
            positionsWriter?.Dispose();
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
