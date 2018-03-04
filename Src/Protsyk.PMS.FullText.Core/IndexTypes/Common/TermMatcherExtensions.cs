using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Protsyk.PMS.FullText.Core
{
    public static class TermMatcherExtensions
    {
        public static bool IsMatch(this ITermMatcher<char> matcher, string word)
        {
            matcher.Reset();
            for(int i=0; i<word.Length; ++i)
            {
                if (!matcher.Next(word[i]))
                {
                    return false;
                }
            }
            return matcher.IsFinal();
        }
    }

}