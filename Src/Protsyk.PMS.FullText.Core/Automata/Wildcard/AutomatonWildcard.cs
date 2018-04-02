using System;
using System.Linq;

namespace Protsyk.PMS.FullText.Core.Automata
{
    /// <summary>
    /// Automaton for matching agains wildcard pattern (* and ?)
    /// </summary>
    public static class AutomatonWildcard
    {
        public static bool Match(string pattern, string text)
        {
            var dfa = CreateAutomaton(pattern).Determinize();
            var s = 0;
            for (int i=0; i<text.Length; ++i)
            {
                s = dfa.Next(s, text[i]);
                if (s == DFA.NoState)
                {
                    return false;
                }
            }
            return dfa.IsFinal(s);
        }

        public static NFA CreateAutomaton(string a)
        {
            var result = new NFA();
            var start = 0;
            var next = 1;

            result.AddState(start, false);

            for (int i = 0; i < a.Length; ++i)
            {
                if (a[i] == '*')
                {
                    result.AddTransition(next - 1, next - 1, NFA.Any);
                }
                else
                {
                    result.AddState(next, false);
                    result.AddTransition(next - 1, next, (a[i] != '?' ? CharRange.SingleChar(a[i]) : NFA.Any));
                    ++next;
                }
            }

            result.AddState(next, true);
            result.AddTransition(next - 1, next, NFA.Epsilon);

            return result;
        }
    }
}
