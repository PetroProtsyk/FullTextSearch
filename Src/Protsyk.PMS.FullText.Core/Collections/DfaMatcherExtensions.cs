using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core.Collections
{
    public static class DfaMatcherExtensions
    {
        public static bool IsMatch<T>(this IDfaMatcher<T> matcher, IEnumerable<T> word)
        {
            matcher.Reset();
            foreach(var c in word)
            {
                if (!matcher.Next(c))
                {
                    return false;
                }
            }
            return matcher.IsFinal();
        }
    }

}