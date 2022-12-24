using System;
using System.Collections.Generic;

using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core;

public class OrMultiQuery : ISearchQuery
{
    #region Fields
    private readonly ISearchQuery[] queries;
    private readonly Heap<ValueTuple<IMatch, ISearchQuery>> heap;
    private States state;

    private enum States
    {
        Initial,
        MatchReturn,
        MatchAdvance,
        Consumed
    }
    #endregion

    #region Methods
    public OrMultiQuery(params ISearchQuery[] queries)
    {
        this.queries = queries;
        this.state = States.Initial;
        this.heap = new Heap<ValueTuple<IMatch, ISearchQuery>>(
                Comparer<ValueTuple<IMatch, ISearchQuery>>.Create(
                    (x, y) => MatchComparer.Instance.Compare(x.Item1, y.Item1)));
    }
    #endregion

    #region ISearchQuery

    public IMatch NextMatch()
    {
        while (true)
        {
            switch (state)
            {
                case States.Consumed:
                    return null;
                case States.Initial:
                    {
                        state = States.MatchReturn;
                        foreach (var searchQuery in queries)
                        {
                            var match = searchQuery.NextMatch();
                            if (match != null)
                            {
                                heap.Add(new ValueTuple<IMatch, ISearchQuery>(match, searchQuery));
                            }
                        }
                    }
                    break;
                case States.MatchReturn:
                    if (heap.Count == 0)
                    {
                        state = States.Consumed;
                        return null;
                    }
                    state = States.MatchAdvance;
                    return heap.Top.Item1;
                case States.MatchAdvance:
                    {
                        var top = heap.RemoveTop();
                        var searchQuery = top.Item2;
                        var match = searchQuery.NextMatch();
                        if (match != null)
                        {
                            heap.Add(new ValueTuple<IMatch, ISearchQuery>(match, searchQuery));
                        }
                        state = States.MatchReturn;
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }


    public void Dispose()
    {
        foreach (var searchQuery in queries)
        {
            searchQuery.Dispose();
        }
    }
    #endregion
}
