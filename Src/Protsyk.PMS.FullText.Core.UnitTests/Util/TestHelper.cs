using System.Linq;

using Protsyk.PMS.FullText.Core.Automata;

namespace Protsyk.PMS.FullText.Core.UnitTests;

class TestHelper
{
    public static IFullTextIndex PrepareIndexForSearch(IIndexName indexName)
    {
        using (var builder = IndexFactory.CreateBuilder(indexName))
        {
            builder.Start();
            builder.AddText("Hello World!", null);
            builder.AddText("Petro Petrolium Petrol", null);
            builder.AddText("This is test document for search unit tests", null);
            builder.AddText("This test document is used for search operators", null);
            builder.AddText("This full-text search only supports boolean operators: and, or", null);
            builder.AddText("Programming is very exciting. Programs can help. This is fantastic!!!", null);
            builder.StopAndWait();
        }

        return IndexFactory.OpenIndex(indexName);
    }

    public static IFullTextIndex AddToIndex(IIndexName indexName, string text)
    {
        using (var builder = IndexFactory.CreateBuilder(indexName))
        {
            builder.Start();
            builder.AddText(text, null);
            builder.StopAndWait();
        }

        return IndexFactory.OpenIndex(indexName);
    }

    public static IPostingList GetPostingList(IFullTextIndex index, string term)
    {
        var dictionaryTerm = index.Dictionary.GetTerms(new DfaTermMatcher(new WildcardMatcher(term, index.Header.MaxTokenSize))).Single();
        return index.PostingLists.Get(dictionaryTerm.Value);
    }
}
