using System;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core;

public static class IndexFactory
{
    private static readonly Dictionary<Type, IIndexTypeFactory> indexTypes = new Dictionary<Type, IIndexTypeFactory>
    {
        { typeof(InMemoryIndexName), new InMemoryIndexFactory() },
        { typeof(PersistentIndexName), new PersistentIndexFactory() },
    };

    public static IIndexBuilder CreateBuilder(IIndexName name)
    {
        IIndexTypeFactory factory = GetFactoryForName(name);
        return factory.CreateBuilder(name);
    }

    public static IFullTextIndex OpenIndex(IIndexName name)
    {
        IIndexTypeFactory factory = GetFactoryForName(name);
        return factory.OpenIndex(name);
    }

    private static IIndexTypeFactory GetFactoryForName(IIndexName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!indexTypes.TryGetValue(name.GetType(), out var factory))
        {
            throw new InvalidOperationException($"Invalid type {name.GetType().Name}");
        }

        return factory;
    }
}

public interface IIndexTypeFactory
{
    IIndexBuilder CreateBuilder(IIndexName name);

    IFullTextIndex OpenIndex(IIndexName name);
}
