using System;

namespace Protsyk.PMS.FullText.Core.Automata
{
    /// <summary>
    /// Match all elements in the Trie within given Levenshtein distance
    /// from the target element.
    /// 
    /// Works for Tries built from a set of strings.
    /// </summary>
    public class LevenshteinMatcher : AutomataMatcher
    {
        public LevenshteinMatcher(string pattern, int degree)
            : base(pattern.Length + degree + 1, ()=> CreateDFA(pattern, degree))
        {
        }

        private static DFA CreateDFA(string pattern, int degree)
        {
            return LevenshteinAutomaton.CreateAutomaton(pattern, degree).Determinize();
        }
    }
}