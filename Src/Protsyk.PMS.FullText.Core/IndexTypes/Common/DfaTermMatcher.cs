using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core;

public class DfaTermMatcher : ITermMatcher
{
    private readonly IDfaMatcher<char> matcher;

    public DfaTermMatcher(IDfaMatcher<char> matcher)
    {
        this.matcher = matcher;
    }

    public IDfaMatcher<char> ToDfaMatcher() => matcher;
}