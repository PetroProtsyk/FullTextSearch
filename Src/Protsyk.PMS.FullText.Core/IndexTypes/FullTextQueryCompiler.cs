using System.Linq;

using Protsyk.PMS.FullText.Core.Automata;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core;

internal sealed class FullTextQueryCompiler : IFullTextQueryCompiler
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
        ArgumentNullException.ThrowIfNull(ast);

        if (ast is WordAstQuery or WildcardAstQuery or EditAstQuery)
        {
            return CompilePattern(index.GetTerms(BuildMatcher(ast)));
        }
        else if (ast is FunctionAstQuery func)
        {
            return func.Name switch
            {
                "OR"  => CompileOr(func),
                "SEQ" => CompileSeq(func),
                _     => throw new NotSupportedException($"Function {func.Name} is not supported")
            };
        }
        else
        {
            throw new NotSupportedException($"Query {ast.Name} is not supported");
        }
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
        return ast switch
        {
            WordAstQuery wordQuery     => BuildWordMatcher(wordQuery),
            WildcardAstQuery wildQuery => BuildWildcardMatcher(wildQuery),
            EditAstQuery editQuery     => BuildEditMatcher(editQuery),
            _                          => throw new Exception("Not a terminal query")
        };
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
