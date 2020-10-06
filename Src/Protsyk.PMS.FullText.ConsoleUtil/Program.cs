using CommandLine;
using Protsyk.PMS.FullText.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace Protsyk.PMS.FullText.ConsoleUtil
{
    [Verb("index", HelpText = "Add file contents to the index.")]
    class IndexOptions
    {
        [Option('i', "input", Required = true, HelpText = "Folder with files that should be indexed")]
        public string InputPath { get; set; }

        [Option('d', "dictionaryType", Required = false, Default = "Default", HelpText = "Type of dictionary storage. TST or FST")]
        public string DictionaryType { get; set; }

        [Option('f', "fieldsType", Required = false, Default = "Default", HelpText = "Type of metadata storage. List, BTree or HashTable")]
        public string FieldsType { get; set; }

        [Option('p', "postingType", Required = false, Default = "Default", HelpText = "Type of posting list. Text, Binary, PackedInt, VarIntCompressed or BinaryCompressed")]
        public string PostingType { get; set; }

        [Option('e', "textEncoding", Required = false, Default = "Default", HelpText = "Type of dictionary encoding")]
        public string TextEncoding { get; set; }

        [Option('m', "mask", Required = false, Default = "*.txt", HelpText = "File name filter")]
        public string Filter { get; set; }

        [Option('t', "type", Required = false, Default = "text", HelpText = "Index text, Wikipedia metadata xml or file names: text, enwiki, name")]
        public string InputType { get; set; }

        [Option('y', "indexFolder", Required = false, Default = ".", HelpText = "Folder with index")]
        public string IndexPath { get; set; }

    }

    [Verb("search", HelpText = "Search index.")]
    class SearchOptions
    {
        [Option('q', "query", Required = true, HelpText = "Search Query")]
        public string Query { get; set; }

        [Option('y', "indexFolder", Required = false, Default = ".", HelpText = "Folder with index")]
        public string IndexPath { get; set; }
    }

    [Verb("print", HelpText = "Print index.")]
    class PrintOptions
    {
        [Option('y', "indexFolder", Required = false, Default = ".", HelpText = "Folder with index")]
        public string IndexPath { get; set; }
    }

    [Verb("lookup", HelpText = "Match dictionary terms using pattern.")]
    class LookupOptions
    {
        [Option('p', "pattern", Required = true, HelpText = "Search Pattern")]
        public string Pattern { get; internal set; }

        [Option('y', "indexFolder", Required = false, Default = ".", HelpText = "Folder with index")]
        public string IndexPath { get; set; }
    }

    [Verb("benchmark", HelpText = "Benchmark search engine.")]
    class BenchmarkOptions
    {
        [Option('n', "size", Required = false, Default = 10000UL, HelpText = "Size of sample")]
        public ulong Count { get; internal set; }
    }

    [Verb("index-wiki", HelpText = "Download enwiki and create index")]
    class IndexWikikOptions
    {
        [Option('d', "dictionaryType", Required = false, Default = "Default", HelpText = "Type of dictionary storage. TST or FST")]
        public string DictionaryType { get; set; }

        [Option('f', "fieldsType", Required = false, Default = "Default", HelpText = "Type of metadata storage. List, BTree or HashTable")]
        public string FieldsType { get; set; }

        [Option('p', "postingType", Required = false, Default = "Default", HelpText = "Type of posting list. Text, Binary, PackedInt, VarIntCompressed or BinaryCompressed")]
        public string PostingType { get; set; }

        [Option('e', "textEncoding", Required = false, Default = "Default", HelpText = "Type of dictionary encoding")]
        public string TextEncoding { get; set; }

        [Option('y', "indexFolder", Required = false, Default = ".", HelpText = "Folder with index")]
        public string IndexPath { get; set; }
    }

    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintConsole(ConsoleColor.Green, "PMS Full-Text Search (c) Petro Protsyk 2017-2021");

            return Parser.Default.ParseArguments<IndexOptions,
                                                 IndexWikikOptions,
                                                 SearchOptions,
                                                 PrintOptions,
                                                 LookupOptions,
                                                 BenchmarkOptions>(args)
              .MapResult(
                (IndexOptions opts) => DoIndex(opts),
                (IndexWikikOptions opts) => DoWikiIndex(opts),
                (SearchOptions opts) => DoSearch(opts),
                (PrintOptions opts) => DoPrint(opts),
                (LookupOptions opts) => DoLookup(opts),
                (BenchmarkOptions opts) => DoBenchmark(opts),
                errors => 255);
        }

        private static int DoBenchmark(BenchmarkOptions opts)
        {
            var N = opts.Count;
            var skip = 1000UL;

            foreach (var name in PostingListIOFactory.GetNames())
            {
                DoBenchmarkPostingList(name, N, skip);
            }

            return 1;
        }

        private static void DoBenchmarkPostingList(string name, ulong count, ulong skip)
        {
            using (var ms = new Core.Common.Persistance.MemoryStorage())
            {
                var address = default(PostingListAddress);

                var timer = Stopwatch.StartNew();
                using (var writer = PostingListIOFactory.CreateWriter(name, ms.GetReference()))
                {
                    writer.StartList(string.Empty);
                    for (ulong i = 0; i < count; ++i)
                    {
                        writer.AddOccurrence(Occurrence.O(1, 1, skip + i));
                    }
                    address = writer.EndList();
                }
                PrintConsole(ConsoleColor.Magenta, $"Encoder {name}");
                PrintConsole(ConsoleColor.Magenta, $"\tCount : {count}");
                PrintConsole(ConsoleColor.Magenta, $"\tSize  : {ms.Length}");
                PrintConsole(ConsoleColor.Magenta, $"\tWrite : {timer.Elapsed}");

                timer = Stopwatch.StartNew();
                using (var reader = PostingListIOFactory.CreateReader(name, ms.GetReference()))
                {
                    // Linear Scan
                    {
                        ulong i = 0;
                        foreach (var actual in reader.Get(address))
                        {
                            var target = Occurrence.O(1, 1, skip + i);
                            if (target != actual)
                            {
                                throw new Exception();
                            }
                            ++i;
                        }
                    }

                    var list = reader.Get(address).AsSkipList();
                    for (ulong i = 0; i < count; ++i)
                    {
                        var target = Occurrence.O(1, 1, skip + i);
                        var actual = list.LowerBound(target).FirstOrDefault();
                        if (target != actual)
                        {
                            throw new Exception();
                        }
                    }

                    for (ulong i = 1; i < skip; ++i)
                    {
                        var target = Occurrence.O(1, 1, i);
                        var actual = list.LowerBound(target).FirstOrDefault();
                        if (actual != Occurrence.O(1, 1, skip))
                        {
                            throw new Exception();
                        }
                    }

                    for (ulong i = count + 1; i < count + skip; ++i)
                    {
                        var target = Occurrence.O(1, 1, skip + i);
                        var actual = list.LowerBound(target).FirstOrDefault();
                        if (actual != Occurrence.Empty)
                        {
                            throw new Exception();
                        }
                    }
                }
                PrintConsole(ConsoleColor.Magenta, $"\tSeek  : {timer.Elapsed}");
            }
        }

        private static int DoLookup(LookupOptions opts)
        {
            var pattern = opts.Pattern;
            var timer = Stopwatch.StartNew();
            var termsFound = 0;
            using (var index = IndexFactory.OpenIndex(new PersistentIndexName(opts.IndexPath)))
            {
                var matcher = index.CompilePattern(pattern);
                var terms = index.GetTerms(matcher);
                foreach (var term in terms)
                {
                    ++termsFound;
                    PrintConsole(ConsoleColor.Gray, term.Key);
                }
            }

            PrintConsole(ConsoleColor.White, $"Terms found: {termsFound}, time: {timer.Elapsed}");
            return 0;
        }

        private static int DoPrint(PrintOptions opts)
        {
            var timer = Stopwatch.StartNew();
            using (var index = IndexFactory.OpenIndex(new PersistentIndexName(opts.IndexPath)))
            {
                var visitor = new PrintVisitor(index);
                index.Visit(visitor);
                PrintConsole(ConsoleColor.White, $"Terms: {visitor.Terms}, time: {timer.Elapsed}");
            }
            return 0;
        }

        private static int DoSearch(SearchOptions opts)
        {
            var timer = Stopwatch.StartNew();
            var documentsCount = 0;
            var matchesCount = 0;
            using (var index = IndexFactory.OpenIndex(new PersistentIndexName(opts.IndexPath)))
            {
                var searchQuery = index.Compile(opts.Query);
                var prevDoc = Occurrence.NoId;
                var doc = default(TextDocument);
                var hits = new SortedSet<int>();
                foreach (var match in searchQuery.AsEnumerable())
                {
                    if (match.DocumentId != prevDoc)
                    {
                        if (prevDoc != Occurrence.NoId)
                        {
                            PrintConsole(ConsoleColor.Gray, String.Empty);

                            PrintConsole(ConsoleColor.Gray, "====================");
                            doc = new TextDocument(index.GetText(prevDoc, 1UL).ReadToEnd(),
                                                index.GetPositions(prevDoc, 1UL));
                            PrintConsole(ConsoleColor.Green, doc.Annotate(hits));
                            PrintConsole(ConsoleColor.Gray, "====================");
                            PrintConsole(ConsoleColor.Gray, String.Empty);
                            hits.Clear();
                        }

                        PrintConsole(ConsoleColor.Gray, index.Fields.GetMetadata(match.DocumentId));
                        prevDoc = match.DocumentId;
                        documentsCount++;
                    }
                    ++matchesCount;
                    foreach (var o in match.GetOccurrences())
                    {
                        hits.Add((int)o.TokenId);
                    }
                    PrintConsole(ConsoleColor.Gray, $"{match} ");
                }
                if (prevDoc != Occurrence.NoId)
                {
                    PrintConsole(ConsoleColor.Gray, String.Empty);

                    PrintConsole(ConsoleColor.Gray, "====================");
                    doc = new TextDocument(index.GetText(prevDoc, 1UL).ReadToEnd(),
                                        index.GetPositions(prevDoc, 1UL));
                    PrintConsole(ConsoleColor.Green, doc.Annotate(hits));
                    PrintConsole(ConsoleColor.Gray, "====================");
                    PrintConsole(ConsoleColor.Gray, String.Empty);
                    hits.Clear();
                }
            }

            PrintConsole(ConsoleColor.White, $"Documents found: {documentsCount}, matches: {matchesCount}, time: {timer.Elapsed}");
            return 0;
        }

        static IEnumerable<String> ParseEnWikiXml(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            {
                using (var reader = XmlReader.Create(stream))
                {
                    reader.MoveToContent();
                    while (!reader.EOF)
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "title")
                        {
                            var title = reader.ReadElementContentAsString();
                            if (!string.IsNullOrEmpty(title))
                            {
                                yield return title;
                            }
                        }
                        else
                        {
                            reader.Read();
                        }
                    }
                }
            }
        }

        private static int DoIndex(IndexOptions opts)
        {
            using (var builder = IndexFactory.CreateBuilder(new PersistentIndexName(opts.IndexPath, opts.DictionaryType, opts.FieldsType, opts.PostingType, opts.TextEncoding)))
            {
                builder.Start();

                var timer = Stopwatch.StartNew();
                var documents = 0;
                foreach (var file in Directory.EnumerateFiles(opts.InputPath, opts.Filter, SearchOption.AllDirectories).Select(f => new FileInfo(f)))
                {
                    PrintConsole(ConsoleColor.Gray, $"{file.FullName}");
                    if (opts.InputType == "text")
                    {
                        builder.AddFile(
                            file.FullName,
                            "{filename:\"" + file.FullName + "\", size:\"" + file.Length + "\", created:\"" + file.CreationTime.ToString("o") + "\"}");
                    }
                    else if (opts.InputType == "name")
                    {
                        builder.AddText(
                            file.FullName,
                            "{filename:\"" + file.FullName + "\", size:\"" + file.Length + "\", created:\"" + file.CreationTime.ToString("o") + "\"}");
                    }
                    else if (opts.InputType == "enwiki")
                    {
                        int t = 0;
                        foreach (var title in ParseEnWikiXml(file.FullName))
                        {
                            var text = title;
                            if (text.StartsWith("Wikipedia: "))
                            {
                                text = text.Substring(11);
                            }

                            PrintConsole(ConsoleColor.Gray, $"\t {++t} -> {text}");
                            builder.AddText(text, "{id:\"" + t + "\"}");
                        }
                    }
                    else
                    {
                        throw new Exception("Unsupported input type");
                    }
                    ++documents;
                }
                var stat = builder.StopAndWait();
                PrintConsole(ConsoleColor.White, $"Indexed documents: {documents}, terms: {stat.Terms}, occurrences: {stat.Occurrences}, time: {timer.Elapsed}");
            }

            return 0;
        }

        private static int DoWikiIndex(IndexWikikOptions opts)
        {
            var gzFilename = DownloadAbstracts(opts.IndexPath).Result;
            var filename = Decompress(opts.IndexPath, gzFilename).Result;

            return DoIndex(new IndexOptions
            {
                DictionaryType = opts.DictionaryType,
                IndexPath = opts.IndexPath,
                FieldsType = opts.FieldsType,
                PostingType = opts.PostingType,
                TextEncoding = opts.TextEncoding,
                Filter = "*.xml",
                InputType = "enwiki",
                InputPath = Path.GetDirectoryName(filename)
            });
        }

        private static async Task<string> DownloadAbstracts(string outputFolder)
        {
            var url = "https://dumps.wikimedia.org/enwiki/latest/enwiki-latest-abstract.xml.gz";
            var outputFileName = Path.Combine(outputFolder, "enwiki-latest-abstract.xml.gz");
            if (File.Exists(outputFileName))
            {
                PrintConsole(ConsoleColor.White, $"File exists: {outputFileName}");
                return outputFileName;
            }
            PrintConsole(ConsoleColor.White, $"Downloading abstracts to: {outputFileName}");
            using (var wc = new System.Net.WebClient())
            {
                await wc.DownloadFileTaskAsync(new Uri(url), outputFileName);
            }
            PrintConsole(ConsoleColor.White, "Downloading complete");
            return outputFileName;
        }

        private static async Task<string> Decompress(string outputFolder, string fileToDecompress)
        {
            var outputFile = Path.Combine(outputFolder, "enwiki-latest-abstract.xml");
            if (File.Exists(outputFile))
            {
                PrintConsole(ConsoleColor.White, $"File exists: {outputFile}");
                return outputFile;
            }

            PrintConsole(ConsoleColor.White, $"Decompressing to: {outputFile}");
            using (var originalFileStream = File.OpenRead(fileToDecompress))
            {
                using (var decompressedFileStream = File.Create(outputFile))
                {
                    using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    {
                        await decompressionStream.CopyToAsync(decompressedFileStream);
                    }
                }
            }
            PrintConsole(ConsoleColor.White, "Decompressing complete");
            return outputFile;
        }

        private static void PrintConsole(ConsoleColor color, string text)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = old;
        }

        private class PrintVisitor : IIndexVisitor
        {
            private readonly IFullTextIndex index;

            public long Terms {get; private set;}

            public PrintVisitor(IFullTextIndex index)
            {
                this.index = index;
                this.Terms = 0;
            }

            public bool VisitTerm(DictionaryTerm term)
            {
                PrintConsole(ConsoleColor.Gray, $"{term.Key} -> {string.Join(", ", index.PostingLists.Get(term.Value))}");
                ++Terms;
                return true;
            }
        }
    }
}
