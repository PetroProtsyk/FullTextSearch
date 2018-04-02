using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    /// <summary>
    /// Match of the query. One or more occurrences
    /// </summary>
    public interface IMatch
    {
        IEnumerable<Occurrence> GetOccurrences();

        Occurrence Left { get; }

        Occurrence Right { get; }

        Occurrence Max { get; }

        Occurrence Min { get; }

        ulong DocumentId { get; }
    }
}
