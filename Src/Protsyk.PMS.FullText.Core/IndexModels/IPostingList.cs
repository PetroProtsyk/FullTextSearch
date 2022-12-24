using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core;

// Ordered list of occurrences.
// Occurrences should be ordered from smallest to greatest.
public interface IPostingList : IEnumerable<Occurrence>
{
}
