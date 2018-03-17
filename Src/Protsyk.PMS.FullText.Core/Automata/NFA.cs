using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Automata
{
    /// <summary>
    /// Nondeterministic finite automaton
    /// https://en.wikipedia.org/wiki/Nondeterministic_finite_automaton
    /// </summary>
    public class NFA
    {
        public static char EpsilonChar = 'ε';
        public static char AnyChar = '*';

        public static CharRange Epsilon = new CharRange(0, 0);
        public static CharRange Any = new CharRange(1, 65535);

        private int initial = 0;
        private readonly List<int> states = new List<int>();
        private readonly List<ValueTuple<int, int, CharRange>> transitions = new List<ValueTuple<int, int, CharRange>>();
        private readonly HashSet<int> final = new HashSet<int>();

        public void AddState(int state, bool isFinal)
        {
            states.Add(state);
            if (isFinal)
            {
                final.Add(state);
            }
        }

        public void AddTransition(int from, int to, CharRange c)
        {
            if (!transitions.Any(t => t.Item1 == from && t.Item2 == to && t.Item3.Equals(c)))
            {
                transitions.Add(new ValueTuple<int, int, CharRange>(from, to, c));
            }
        }

        public static NFA AnyOf(NFA left, NFA right)
        {
            var result = new NFA();
            result.AddState(0, false);
            result.AddTransition(0, result.Add(left), Epsilon);
            result.AddTransition(0, result.Add(right), Epsilon);
            return result;
        }

        public static NFA Star(NFA input)
        {
            var result = new NFA();

            var newFinal = result.NewState();
            result.AddState(newFinal, true);

            var start = result.Add(input);
            var finals = result.final.Where(h => h != newFinal).ToArray();
            foreach (var final in finals)
            {
                result.AddTransition(final, start, Epsilon);
            }
            result.AddTransition(newFinal, start, Epsilon);

            return result;
        }

        public static NFA Concat(NFA left, NFA right)
        {
            var result = new NFA();
            result.Add(left);

            var leftFinals = result.final.ToArray();
            result.final.Clear();

            int rightInitial = result.Add(right);
            foreach (var final in leftFinals)
            {
                result.AddTransition(final, rightInitial, Epsilon);
            }
            return result;
        }

        private int Add(NFA left)
        {
            int stateOffset = NewState();
            foreach (var s in left.states)
            {
                AddState(s + stateOffset, left.final.Contains(s));
            }
            foreach (var t in left.transitions)
            {
                AddTransition(t.Item1 + stateOffset, t.Item2 + stateOffset, t.Item3);
            }
            return stateOffset;
        }

        private int NewState()
        {
            if (states.Count == 0)
            {
                return 0;
            }
            return states.Max() + 1;
        }

        private void EpsilonClosure(HashSet<int> setState)
        {
            var frontier = new Stack<int>(setState);

            while (frontier.Count > 0)
            {
                var fromState = frontier.Pop();

                foreach (var ept in FindTransitions(fromState).Where(t => t.Item3.Equals(Epsilon)))
                {
                    if (setState.Add(ept.Item2))
                    {
                        frontier.Push(ept.Item2);
                    }
                }
            }
        }

        private IEnumerable<ValueTuple<int, int, CharRange>> FindTransitions(int from_state)
        {
            return transitions.Where(t => t.Item1 == from_state);
        }

        class SetStateComparer : IEqualityComparer<HashSet<int>>
        {
            public bool Equals(HashSet<int> x, HashSet<int> y)
            {
                if (x.Count != y.Count)
                {
                    return false;
                }

                return x.All(t => y.Contains(t));
            }

            public int GetHashCode(HashSet<int> x)
            {
                uint hash = int.MaxValue;
                foreach (var value in x.OrderBy(t => t))
                {
                    hash ^= (uint)value + 0x9e3779b9 + (hash << 6) + (hash >> 2);
                }
                return (int)hash;
            }
        }

        bool ContainsFinalState(HashSet<int> x)
        {
            return x.Intersect(final).Any();
        }

        public string ToDotNotation()
        {
            var result = new StringBuilder();
            result.AppendLine("digraph NFA {");
            result.AppendLine("rankdir = LR;");
            result.AppendLine("orientation = Portrait;");

            for (int i = 0; i < states.Count; ++i)
            {
                if (states[i] == initial)
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = circle, style = bold, fontsize = 14]", states[i]);
                    result.AppendLine();
                }
                else if (final.Contains(states[i]))
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = doublecircle, style = bold, fontsize = 14]", states[i]);
                    result.AppendLine();
                }
                else
                {
                    result.AppendFormat("{0}[label = \"{0}\", shape = circle, style = solid, fontsize = 14]", states[i]);
                    result.AppendLine();
                }

                foreach (var t in transitions)
                {
                    if (t.Item1 != states[i]) continue;
                    if (t.Item3.Equals(Any))
                    {
                        result.AppendFormat("{0}->{1} [label = \"*\", fontsize = 14];", t.Item1, t.Item2);
                        result.AppendLine();
                    }
                    else if (t.Item3.Equals(Epsilon))
                    {
                        result.AppendFormat("{0}->{1} [label = \"&epsilon;\", fontsize = 14];", t.Item1, t.Item2);
                        result.AppendLine();
                    }
                    else
                    {
                        result.AppendFormat("{0}->{1} [label = \"{2}\", fontsize = 14];", t.Item1, t.Item2, t.Item3);
                        result.AppendLine();
                    }
                }
            }

            result.AppendLine("}");
            return result.ToString();
        }

        public DFA Determinize()
        {
            var target = new DFA();

            var frontier = new Stack<ValueTuple<int, HashSet<int>>>();
            var seen = new Dictionary<HashSet<int>, int>(new SetStateComparer());
            int setKey = 0;

            var setInitial = new HashSet<int>();
            setInitial.Add(0);
            EpsilonClosure(setInitial);

            target.AddState(setKey, ContainsFinalState(setInitial));

            frontier.Push(new ValueTuple<int, HashSet<int>>(setKey, setInitial));
            seen[setInitial] = setKey;

            while (frontier.Count > 0)
            {
                var current = frontier.Pop();
                var currentKey = current.Item1;
                var currentSet = current.Item2;

                var inputs = new HashSet<CharRange>();
                foreach (var st in currentSet)
                {
                    foreach (var t in FindTransitions(st))
                    {
                        if (t.Item3.Equals(Epsilon))
                        {
                            continue;
                        }
                        inputs.Add(t.Item3);
                    }
                }

                var disjoinInputs = inputs.Disjoin();
                foreach (var i in disjoinInputs)
                {
                    var newState = new HashSet<int>();
                    var newTransition = new HashSet<CharRange>();

                    foreach (var st in currentSet)
                    {
                        foreach (var t in FindTransitions(st))
                        {
                            var commonRange = t.Item3.Intersect(i);
                            if (!commonRange.Equals(CharRange.Empty))
                            {
                                newState.Add(t.Item2);
                                newTransition.Add(commonRange);
                            }
                        }
                    }

                    var newTransitions = newTransition.Disjoin();

                    EpsilonClosure(newState);

                    int seenStateKey;
                    if (!seen.TryGetValue(newState, out seenStateKey))
                    {
                        setKey++;

                        target.AddState(setKey, ContainsFinalState(newState));

                        foreach (var range in newTransitions)
                        {
                            target.AddTransition(currentKey, setKey, range);
                        }

                        frontier.Push(new ValueTuple<int, HashSet<int>>(setKey, newState));
                        seen[newState] = setKey;
                    }
                    else
                    {
                        foreach (var range in newTransitions)
                        {
                            target.AddTransition(currentKey, seenStateKey, range);
                        }
                    }
                }
            }

            return target;
        }
    }
}
