using System;

namespace Protsyk.PMS.FullText.Core
{
    public class PersistentIndexFactory : IIndexTypeFactory
    {
        public IIndexBuilder CreateBuilder(IIndexName name)
        {
            return new PersistentBuilder(Convert(name));
        }

        public IFullTextIndex OpenIndex(IIndexName name)
        {
            return new PersistentIndex(Convert(name));
        }

        private static PersistentIndexName Convert(IIndexName name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var result = name as PersistentIndexName;
            if (result == null)
            {
                throw new InvalidOperationException($"Invalid type {name.GetType().Name}");
            }

            return result;
        }
    }
}
