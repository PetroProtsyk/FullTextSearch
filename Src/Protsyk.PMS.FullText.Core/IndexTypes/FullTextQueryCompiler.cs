using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core
{

    public class FullTextQueryCompiler : IFullTextQueryCompiler
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
            var parser = new QueryParser();
            var ast = parser.Parse(query);
            return Compile(ast);
        }

        public void Dispose()
        {
        }

        private ISearchQuery Compile(AstQuery ast)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var wordQuery = ast as WordAstQuery;
            if (wordQuery != null)
            {
                return CompileWord(wordQuery);
            }

            var wildQuery = ast as WildcardAstQuery;
            if (wildQuery != null)
            {
                return CompileWildcard(wildQuery);
            }

            var editQuery = ast as EditAstQuery;
            if (editQuery != null)
            {
                return CompileEdit(editQuery);
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

        private ISearchQuery CompileWord(WordAstQuery wordQuery)
        {
            var matcher = new DfaTermMatcher(new SequenceMatcher<char>(wordQuery.Value, false));
            var postingList = index.GetTerms(matcher).Select(p => index.PostingLists.Get(p.Value)).SingleOrDefault();
            if (postingList == null)
            {
                return NullQuery.Instance;
            }

            return new TermQuery(postingList);
        }

        private ISearchQuery CompileWildcard(WildcardAstQuery wildQuery)
        {
            return new OrMultiQuery(index.GetPostingLists(wildQuery.Value).Select(t => new TermQuery(t)).ToArray());
        }

        private ISearchQuery CompileEdit(EditAstQuery editQuery)
        {
            return new OrMultiQuery(index.GetPostingLists(editQuery.Value, editQuery.Distance).Select(t => new TermQuery(t)).ToArray());
        }
    }
}
