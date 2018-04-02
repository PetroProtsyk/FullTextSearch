
namespace Protsyk.PMS.FullText.Core.UnitTests
{
    class TestHelper
    {
        public static IFullTextIndex PrepareIndexForSearch(IIndexName indexName)
        {
            using (var builder = IndexFactory.CreateBuilder(indexName))
            {
                builder.Start();
                builder.AddText("Hello World!", string.Empty);
                builder.AddText("Petro Petrolium Petrol", string.Empty);
                builder.AddText("This is test document for search unit tests", string.Empty);
                builder.AddText("This test document is used for search operators", string.Empty);
                builder.AddText("This full-text search only supports boolean operators: and, or", string.Empty);
                builder.AddText("Programming is very exciting. Programs can help. This is fantastic!!!", string.Empty);
                builder.StopAndWait();
            }

            return IndexFactory.OpenIndex(indexName);
        }
    }
}
