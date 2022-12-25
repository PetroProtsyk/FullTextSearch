using System;

namespace Protsyk.PMS.FullText.Core.Automata;

/// <summary>
/// Calculate Levenshtein distance between two strings using Levenshtein automaton
/// https://en.wikipedia.org/wiki/Levenshtein_automaton
/// </summary>
public static class LevenshteinAutomaton
{
    public static bool Match(string pattern, string text, int d)
    {
        var dfa = CreateAutomaton(pattern, d).Determinize();
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

    public static NFA CreateAutomaton(string a, int k)
    {
        if (a.Contains('*'))
        {
            throw new ArgumentException("Star is a reserved character");
        }

        var result = new NFA();
        var m = a.Length + 1;

        /* Create |a|*k states */
        for (int i = 0; i < m; ++i)
        {
            for (int j = 0; j <= k; ++j)
            {
                result.AddState(i + m * j, i == a.Length);
            }
        }

        /* Create transitions */
        for (int i = 0; i < m; ++i)
        {
            for (int j = 0; j <= k; ++j)
            {
                if (i < m - 1)
                {
                    result.AddTransition(i + m * j, i + 1 + m * j, CharRange.SingleChar(a[i]));
                }

                if (j < k)
                {
                    if (i < m - 1)
                    {
                        result.AddTransition(i + m * j, i + 1 + m * (j + 1), NFA.Any);
                        result.AddTransition(i + m * j, i + 1 + m * (j + 1), NFA.Epsilon);
                    }

                    result.AddTransition(i + m * j, i + m * (j + 1), NFA.Any);
                }
            }
        }

        return result;
    }
}
