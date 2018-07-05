using System;
using System.Collections.Generic;

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

            Dictionary = new PersistentDictionary(folder, FileNameDictionary, FileNamePostingLists);
            PostingLists = new PostingListReader(folder, FileNamePostingLists);
            Fields = PersistentMetadataFactory.CreateStorage(Header.Type.Split(' ')[1], folder, FileNameFields);
            this.name = name;
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
