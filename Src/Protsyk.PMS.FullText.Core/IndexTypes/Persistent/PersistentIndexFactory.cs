namespace Protsyk.PMS.FullText.Core;

public class PersistentIndexFactory : IIndexTypeFactory
{
    public IIndexBuilder CreateBuilder(IIndexName name)
    {
        //NOTE: Choice of index storage from:
        //      - PersistentMetadataList
        //      - PersistentMetadataBtree

        return new PersistentBuilder(Convert(name));
    }

    public IFullTextIndex OpenIndex(IIndexName name)
    {
        return new PersistentIndex(Convert(name));
    }

    private static PersistentIndexName Convert(IIndexName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var result = name as PersistentIndexName;
        if (result == null)
        {
            throw new InvalidOperationException($"Invalid type {name.GetType().Name}");
        }

        return result;
    }
}
