using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core
{
    /// <summary>
    /// Name and the instance of the in memory index
    /// </summary>
    public class InMemoryIndexName : IIndexName
    {
        internal InMemoryIndex Index { get; } = new InMemoryIndex();
    }
}
