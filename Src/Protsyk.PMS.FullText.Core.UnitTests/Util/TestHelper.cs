
namespace Protsyk.PMS.FullText.Core.UnitTests
{
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
    }
}
