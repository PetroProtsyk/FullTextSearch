using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core;

public interface ITermMatcher
{
    IDfaMatcher<char> ToDfaMatcher();
}