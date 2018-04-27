using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Protsyk.PMS.FullText.Core;

namespace Protsyk.PMS.FullText.ConsoleUtil
{
    class Program
    {
        static void Main(string[] args)
        {
            PrintConsole(ConsoleColor.Green, "PMS Full-Text Search (c) Petro Protsyk 2017-2018");
            if (args.Length < 1)
            {
                PrintHelp();
                return;
            }

            if (args[0] == "index")
            {
                var fieldsType = (args.Length > 3 && args[2] == "--fieldsType") ? args[3] : "List";
                using (var builder = IndexFactory.CreateBuilder(new PersistentIndexName(".", fieldsType)))
                {
                    builder.Start();

                    var timer = Stopwatch.StartNew();
                    var documents = 0;
                    foreach (var file in Directory.EnumerateFiles(args[1], "*.txt", SearchOption.AllDirectories).Select(f => new FileInfo(f)))
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
            }
            else if (args[0] == "search")
            {
                var timer = Stopwatch.StartNew();
                var documentsCount = 0;
                var matchesCount = 0;
                using (var index = IndexFactory.OpenIndex(new PersistentIndexName(".")))
                {
                    using (var compiler = new FullTextQueryCompiler(index))
                    {
                        var searchQuery = compiler.Compile(args[1]);
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
                }

                PrintConsole(ConsoleColor.White, $"Documents found: {documentsCount}, matches: {matchesCount}, time: {timer.Elapsed}");
            }
            else if (args[0] == "print")
            {
                var timer = Stopwatch.StartNew();
                var terms = 0;
                using (var index = IndexFactory.OpenIndex(new PersistentIndexName(".")))
                {
                    index.Visit(new PrintVisitor(index));
                    ++terms;
                }

                PrintConsole(ConsoleColor.White, $"Terms: {terms}, time: {timer.Elapsed}");
            }
            else if (args[0] == "lookup")
            {
                var timer = Stopwatch.StartNew();
                var termsFound = 0;
                using (var index = IndexFactory.OpenIndex(new PersistentIndexName(".")))
                {
                    int tilda = args[1].IndexOf("~");
                    IEnumerable<DictionaryTerm> terms = null;
                    if (tilda == -1)
                    {
                        terms = index.GetTerms(args[1]);
                    }
                    else
                    {
                        terms = index.GetTerms(args[1].Substring(0, tilda), int.Parse(args[1].Substring(tilda + 1)));
                    }

                    foreach (var term in terms)
                    {
                        ++termsFound;
                        PrintConsole(ConsoleColor.Gray, term.Key);
                    }
                }
                PrintConsole(ConsoleColor.White, $"Terms found: {termsFound}, time: {timer.Elapsed}");
            }
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
                PrintConsole(ConsoleColor.Gray, $"{term.Key} -> {index.GetPostingList(term.Key).ToString()}");
                return true;
            }
        }
    }
}
