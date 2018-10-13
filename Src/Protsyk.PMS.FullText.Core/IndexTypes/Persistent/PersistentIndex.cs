﻿using System;
using System.Collections.Generic;
using Protsyk.PMS.FullText.Core.Common.Compression;

namespace Protsyk.PMS.FullText.Core
{
    public class PersistentIndex : IFullTextIndex
    {
        public static readonly string FileNameFields = "index-fields.pms";
        public static readonly string FileNameDictionary = "index-dictionary.pms";
        public static readonly string FileNamePostingLists = "index-postinglists.pms";
        public static readonly string FileNameInfo = "index-info.pms";
        private readonly PersistentIndexName name;

        public PersistentIndex(PersistentIndexName name)
        {
            var folder = name.Folder;

            if (!PersistentIndexInfo.Exists(folder, FileNameInfo))
            {
                throw new InvalidOperationException("No index");
            }

            HeaderReader = new PersistentIndexInfo(folder, FileNameInfo);
            Header = HeaderReader.Read();
            if (Header == null)
            {
                throw new InvalidOperationException("No index");
            }

            VerifyHeader(name);

            var indexType = Header.Type.Split(' ');
            Dictionary = PersistentDictionaryFactory.Create(indexType[1], folder, FileNameDictionary, Header.MaxTokenSize, indexType[4]);
            PostingLists = PostingListIOFactory.CreateReader(indexType[3], folder, FileNamePostingLists);
            Fields = PersistentMetadataFactory.CreateStorage(indexType[2], folder, FileNameFields);
            this.name = name;
        }

        private void VerifyHeader(PersistentIndexName name)
        {
            var types = Header.Type.Split(' ');
            if (types[0] != nameof(PersistentIndex))
            {
                throw new InvalidOperationException("Index type and name mismatch");
            }

            if (name.DictionaryType != PersistentIndexName.DefaultValue && types[1] != name.DictionaryType)
            {
                throw new InvalidOperationException("Index type and name mismatch");
            }

            if (name.FieldsType != PersistentIndexName.DefaultValue && types[2] != name.FieldsType)
            {
                throw new InvalidOperationException("Index type and name mismatch");
            }

            if (name.PostingType != PersistentIndexName.DefaultValue && types[3] != name.PostingType)
            {
                throw new InvalidOperationException("Index type and name mismatch");
            }

            if (name.TextEncoding != PersistentIndexName.DefaultValue && types[4] != name.TextEncoding)
            {
                throw new InvalidOperationException("Index type and name mismatch");
            }
        }

        private PersistentIndexInfo HeaderReader { get; }

        public IFullTextIndexHeader Header { get; }

        public ITermDictionary Dictionary { get; }

        public IPostingLists PostingLists { get; }

        public IMetadataStorage<string> Fields { get; }

        public IEnumerable<DictionaryTerm> GetTerms(ITermMatcher matcher)
        {
            return Dictionary.GetTerms(matcher);
        }

        public ITermMatcher CompilePattern(string pattern)
        {
            using (var compiler = new FullTextQueryCompiler(this))
            {
                return compiler.CompilePattern(pattern);
            }
        }

        public ISearchQuery Compile(string query)
        {
            using (var compiler = new FullTextQueryCompiler(this))
            {
                return compiler.Compile(query);
            }
        }

        public void Dispose()
        {
            PostingLists?.Dispose();
            Dictionary?.Dispose();
            Fields?.Dispose();
            HeaderReader?.Dispose();
        }
    }
}
