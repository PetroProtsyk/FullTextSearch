using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core;

public interface ISkipList : IEnumerable<Occurrence>
{
    /// Returns all occurrences that are equal or greater than c
    IEnumerable<Occurrence> LowerBound(Occurrence c);
}
