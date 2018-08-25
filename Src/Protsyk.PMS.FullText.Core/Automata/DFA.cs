using System;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Automata
{
    /// <summary>
    /// Deterministic finite automaton
    /// </summary>
    public class DFA
    {
        public static readonly int NoState = -1;

        private readonly List<List<ValueTuple<CharRange, int>>> transitions = new List<List<ValueTuple<CharRange, int>>>();
        private readonly HashSet<int> final = new HashSet<int>();

        public void AddState(int state, bool isFinal)
        {
            if (state != transitions.Count)
            {
                throw new ArgumentException();
            }

            transitions.Add(new List<ValueTuple<CharRange, int>>());
            if (isFinal)
            {
                final.Add(state);
            }
        }

        public void AddTransition(int from, int to, CharRange c)
        {
            transitions[from].Add(new ValueTuple<CharRange, int>(c, to));
        }

        public int Next(int s, char c)
        {
            if (s == NoState)
            {
                return NoState;
            }
            foreach (var t in transitions[s])
            {
                if (t.Item1.Contains(c))
                {
                    return t.Item2;
                }
            }
            return NoState;
        }

        public bool IsFinal(int s)
        {
            return final.Contains(s);
        }

        public string ToDotNotation()
        {
            var result = new StringBuilder();
            result.AppendLine("digraph DFA {");
            result.AppendLine("rankdir = LR;");
            result.AppendLine("orientation = Portrait;");

            for (int i = 0; i < transitions.Count; ++i)
            {
                if (i == 0)
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = circle, style = bold, fontsize = 14]", i);
                    result.AppendLine();
                }
                else if (final.Contains(i))
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = doublecircle, style = bold, fontsize = 14]", i);
                    result.AppendLine();
                }
                else
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = circle, style = solid, fontsize = 14]", i);
                    result.AppendLine();
                }

                foreach (var t in transitions[i])
                {
                    result.AppendFormat("{0}->{1} [label = \"{2}\", fontsize = 14];", i, t.Item2, t.Item1);
                    result.AppendLine();
                }
            }

            result.AppendLine("}");
            return result.ToString();
        }
    }

}
