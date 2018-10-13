using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommandLine;
using Protsyk.PMS.FullText.Core;

namespace Protsyk.PMS.FullText.ConsoleUtil
{
    [Verb("index", HelpText = "Add file contents to the index.")]
    class IndexOptions
    {
        [Option('i', "input", Required = true, HelpText = "Folder with files that should be indexed")]
        public string InputPath { get; set; }

        [Option('d', "dictionaryType", Required = false, Default = "Default", HelpText = "Type of dictionary storage. TTD or FST")]
        public string DictionaryType { get; set; }

        [Option('f', "fieldsType", Required = false, Default = "Default", HelpText = "Type of metadata storage. List, BTree or HashTable")]
        public string FieldsType { get; set; }

        [Option('p', "postingType", Required = false, Default = "Default", HelpText = "Type of posting list. Text, Binary or BinaryCompressed")]
        public string PostingType { get; set; }

        [Option('e', "textEncoding", Required = false, Default = "Default", HelpText = "Type of dictionary encoding")]
        public string TextEncoding { get; set; }

        [Option('m', "mask", Required = false, Default = "*.txt", HelpText = "File name filter")]
        public string Filter { get; set; }
    }

    [Verb("search", HelpText = "Search index.")]
    class SearchOptions
    {
        [Option('q', "query", Required = true, HelpText = "Search Query")]
        public string Query { get; set; }
    }

    [Verb("print", HelpText = "Print index.")]
    class PrintOptions
    {
    }

    [Verb("lookup", HelpText = "Match dictionary terms using pattern.")]
    class LookupOptions
    {
        [Option('p', "pattern", Required = true, HelpText = "Search Pattern")]
        public string Pattern { get; internal set; }
    }

    class Program
    {
        static int Main(string[] args)
        {
            PrintConsole(ConsoleColor.Green, "PMS Full-Text Search (c) Petro Protsyk 2017-2018");

            return Parser.Default.ParseArguments<IndexOptions, SearchOptions, PrintOptions, LookupOptions>(args)
              .MapResult(
                (IndexOptions opts) => DoIndex(opts),
                (SearchOptions opts) => DoSearch(opts),
                (PrintOptions opts) => DoPrint(opts),
                (LookupOptions opts) => DoLookup(opts),
                errors => 255);
        }

        private static int DoLookup(LookupOptions opts)
        {
            var pattern = opts.Pattern;
            var timer = Stopwatch.StartNew();
            var termsFound = 0;
            using (var index = IndexFactory.OpenIndex(new PersistentIndexName(".")))
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
            var terms = 0;
            using (var index = IndexFactory.OpenIndex(new PersistentIndexName(".")))
            {
                index.Visit(new PrintVisitor(index));
                ++terms;
            }

            PrintConsole(ConsoleColor.White, $"Terms: {terms}, time: {timer.Elapsed}");
            return 0;
        }

        private static int DoSearch(SearchOptions opts)
        {
            var timer = Stopwatch.StartNew();
            var documentsCount = 0;
            var matchesCount = 0;
            using (var index = IndexFactory.OpenIndex(new PersistentIndexName(".")))
            {
                var searchQuery = index.Compile(opts.Query);
                var prevDoc = Occurrence.NoId;
                foreach (var match in searchQuery.AsEnumerable())
                {
                    if (match.DocumentId != prevDoc)
                    {
                        if (prevDoc != Occurrence.NoId)
                        {
                            PrintConsole(ConsoleColor.Gray, String.Empty);
                        }

                        PrintConsole(ConsoleColor.Gray, index.Fields.GetMetadata(match.DocumentId));
                        prevDoc = match.DocumentId;
                        documentsCount++;
                    }
                    ++matchesCount;
                    PrintConsole(ConsoleColor.Gray, $"{match} ");
                }
                if (prevDoc != Occurrence.NoId)
                {
                    PrintConsole(ConsoleColor.Gray, String.Empty);
                }
            }

            PrintConsole(ConsoleColor.White, $"Documents found: {documentsCount}, matches: {matchesCount}, time: {timer.Elapsed}");
            return 0;
        }

        private static int DoIndex(IndexOptions opts)
        {
            using (var builder = IndexFactory.CreateBuilder(new PersistentIndexName(".", opts.DictionaryType, opts.FieldsType, opts.PostingType, opts.TextEncoding)))
            {
                builder.Start();

                var timer = Stopwatch.StartNew();
                var documents = 0;
                foreach (var file in Directory.EnumerateFiles(opts.InputPath, opts.Filter, SearchOption.AllDirectories).Select(f => new FileInfo(f)))
                {
                    PrintConsole(ConsoleColor.Gray, $"{file.FullName}");
                    builder.AddFile(
                        file.FullName,
                        "{filename:\"" + file.FullName + "\", size:\"" + file.Length + "\", created:\"" + file.CreationTime.ToString("o") + "\"}");
                    ++documents;
                }
                builder.StopAndWait();
                PrintConsole(ConsoleColor.White, $"Indexed documents: {documents}, time: {timer.Elapsed}");
            }

            return 0;
        }

        private static void PrintHelp()
        {
            PrintConsole(ConsoleColor.Gray, "Commands:");

            PrintConsole(ConsoleColor.Gray, "\tIndex folder:");
            PrintConsole(ConsoleColor.Gray, "\tindex FOLDERNAME");
            PrintConsole(ConsoleColor.Gray, "\t--fieldsType List|BTree");
            PrintConsole(ConsoleColor.Gray, "");

            PrintConsole(ConsoleColor.Gray, "\tSearch:");
            PrintConsole(ConsoleColor.Gray, "\tsearch QUERY");
            PrintConsole(ConsoleColor.Gray, "");

            PrintConsole(ConsoleColor.Gray, "\tPrint index:");
            PrintConsole(ConsoleColor.Gray, "\tprint");
            PrintConsole(ConsoleColor.Gray, "");

            PrintConsole(ConsoleColor.Gray, "\tMatch terms in the dictionary:");
            PrintConsole(ConsoleColor.Gray, "\tlookup PATTERN");
            PrintConsole(ConsoleColor.Gray, "");
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

            public PrintVisitor(IFullTextIndex index)
            {
                this.index = index;
            }

            public bool VisitTerm(DictionaryTerm term)
            {
                PrintConsole(ConsoleColor.Gray, $"{term.Key} -> {string.Join(", ", index.PostingLists.Get(term.Value))}");
                return true;
            }
        }
    }
}
