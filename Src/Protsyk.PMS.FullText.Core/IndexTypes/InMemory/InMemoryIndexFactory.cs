namespace Protsyk.PMS.FullText.Core;

public class InMemoryIndexFactory : IIndexTypeFactory
{
    public IIndexBuilder CreateBuilder(IIndexName name)
    {
        return new InMemoryBuilder(InMemoryName(name));
    }

    public IFullTextIndex OpenIndex(IIndexName name)
    {
        return InMemoryName(name).Index;
    }

    private static InMemoryIndexName InMemoryName(IIndexName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var result = name as InMemoryIndexName;
        if (result == null)
        {
            throw new InvalidOperationException($"Invalid type {name.GetType().Name}");
        }

        return result;
    }
}
