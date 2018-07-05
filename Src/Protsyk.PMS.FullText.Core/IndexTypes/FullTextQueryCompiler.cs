using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Protsyk.PMS.FullText.Core.Automata;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core
{

    internal class FullTextQueryCompiler : IFullTextQueryCompiler
    {
        private readonly IFullTextIndex index;
        private readonly int maxTokenLength;

        public FullTextQueryCompiler(IFullTextIndex index)
        {
            this.index = index;
            this.maxTokenLength = index.Header.MaxTokenSize;
        }

        public ISearchQuery Compile(string query)
        {
            var ast = Parse(query);
            return Compile(ast);
        }

        public ITermMatcher CompilePattern(string pattern)
        {
            var ast = Parse(pattern);
            return BuildMatcher(ast);
        }

        public void Dispose()
        {
        }

        private static AstQuery Parse(string query)
        {
            var parser = new QueryParser();
            var ast = parser.Parse(query);
            return ast;
        }

        private ISearchQuery Compile(AstQuery ast)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if ((ast is WordAstQuery) ||
                (ast is WildcardAstQuery) ||
                (ast is EditAstQuery))
            {
                return CompilePattern(index.GetTerms(BuildMatcher(ast)));
            }

            var func = ast as FunctionAstQuery;
            if (func != null)
            {
                if (func.Name == "OR")
                {
                    return CompileOr(func);
                }
                else if (func.Name == "SEQ")
                {
                    return CompileSeq(func);
                }
                else
                {
                    throw new NotSupportedException($"Function {func.Name} is not supported");
                }
            }

            throw new NotSupportedException($"Query {ast.Name} is not supported");
        }

        private ISearchQuery CompilePattern(IEnumerable<DictionaryTerm> terms)
        {
            var termQueries = terms.Select(t => new TermQuery(index.PostingLists.Get(t.Value))).ToArray();

            if (termQueries.Length == 0)
            {
                return NullQuery.Instance;
            }
            else if (termQueries.Length == 1)
            {
                return termQueries[0];
            }
            else if (termQueries.Length == 2)
            {
                return new OrQuery(termQueries[0], termQueries[1]);
            }
            else
            {
                return new OrMultiQuery(termQueries);
            }
        }

        private ITermMatcher BuildMatcher(AstQuery ast)
        {
            var wordQuery = ast as WordAstQuery;
            if (wordQuery != null)
            {
                return BuildWordMatcher(wordQuery);
            }

            var wildQuery = ast as WildcardAstQuery;
            if (wildQuery != null)
            {
                return BuildWildcardMatcher(wildQuery);
            }

            var editQuery = ast as EditAstQuery;
            if (editQuery != null)
            {
                return BuildEditMatcher(editQuery);
            }

            throw new Exception("Not a terminal query");
        }

        private ISearchQuery CompileOr(FunctionAstQuery func)
        {
            return new OrMultiQuery(func.Args.Select(Compile).ToArray());
        }

        private ISearchQuery CompileSeq(FunctionAstQuery func)
        {
            if (func.Args.Any(q => !(q is WordAstQuery)))
            {
                throw new Exception("Unexpected query take in phrase");
            }

            return new PhraseQuery(func.Args.Select(Compile).ToArray());
        }

        private ITermMatcher BuildWordMatcher(WordAstQuery wordQuery)
        {
            return new DfaTermMatcher(new SequenceMatcher<char>(wordQuery.Value, false));
        }

        private ITermMatcher BuildWildcardMatcher(WildcardAstQuery wildQuery)
        {
            return new DfaTermMatcher(new WildcardMatcher(wildQuery.Value, index.Header.MaxTokenSize));
        }

        private ITermMatcher BuildEditMatcher(EditAstQuery editQuery)
        {
            return new DfaTermMatcher(new LevenshteinMatcher(editQuery.Value, editQuery.Distance));
        }
    }
}
