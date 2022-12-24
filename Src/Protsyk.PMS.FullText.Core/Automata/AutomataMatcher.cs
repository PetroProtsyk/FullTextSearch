using System;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.Automata;

/// <summary>
/// Match all elements in the Trie using DFA.
/// </summary>
public abstract class AutomataMatcher : IDfaMatcher<char>
{
    private readonly DFA dfa;
    private readonly int[] states;

    private int current;

    public AutomataMatcher(int maxLength, Func<DFA> createDFA)
    {
        this.states = new int[maxLength];
        this.dfa = createDFA();
    }

    public void Reset()
    {
        current = 0;
        states[current] = 0;
    }

    public bool IsFinal()
    {
        if (current < 1)
        {
            return dfa.IsFinal(0);
        }
        return dfa.IsFinal(states[current - 1]);
    }

    public bool Next(char p)
    {
        var next = dfa.Next(current == 0 ? 0 : states[current - 1], p);
        if (next == DFA.NoState)
        {
            return false;
        }

        states[current++] = next;
        return true;
    }

    public void Pop()
    {
        if (current == 0)
        {
            throw new InvalidOperationException();
        }
        --current;
    }
}