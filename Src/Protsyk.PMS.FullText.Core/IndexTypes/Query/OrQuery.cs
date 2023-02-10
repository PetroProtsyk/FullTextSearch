namespace Protsyk.PMS.FullText.Core;

public class OrQuery : ISearchQuery
{
    #region Fields
    private readonly ISearchQuery leftQuery;
    private readonly ISearchQuery rightQuery;

    private State state;
    private IMatch leftMatch;
    private IMatch rightMatch;

    private enum State
    {
        Initial,
        Merge,
        AdvanceLeft,
        AdvanceRight,
        Tail,
        Null
    };
    #endregion

    #region Methods
    public OrQuery(ISearchQuery leftQuery, ISearchQuery rightQuery)
    {
        this.leftQuery = leftQuery;
        this.rightQuery = rightQuery;
    }
    #endregion

    #region ISearchQuery
    public IMatch NextMatch()
    {
        while (true)
        {
            switch (state)
            {
                case State.Null:
                    return null;
                case State.Initial:
                    leftMatch = leftQuery.NextMatch();
                    rightMatch = rightQuery.NextMatch();
                    if (leftMatch is null && rightMatch is null)
                    {
                        state = State.Null;
                        return null;
                    }
                    else if (leftMatch != null && rightMatch != null)
                    {
                        state = State.Merge;
                    }
                    else
                    {
                        state = State.Tail;
                        return leftMatch ?? rightMatch;
                    }
                    break;
                case State.AdvanceLeft:
                    leftMatch = leftQuery.NextMatch();
                    if (leftMatch is null)
                    {
                        state = State.Tail;
                        return rightMatch;
                    }
                    state = State.Merge;
                    break;
                case State.AdvanceRight:
                    rightMatch = rightQuery.NextMatch();
                    if (rightMatch is null)
                    {
                        state = State.Tail;
                        return leftMatch;
                    }
                    state = State.Merge;
                    break;
                case State.Tail:
                    if (leftMatch is null)
                    {
                        rightMatch = rightQuery.NextMatch();
                        if (rightMatch is null)
                        {
                            state = State.Null;
                            return null;
                        }
                        return rightMatch;
                    }

                    if (rightMatch is null)
                    {
                        leftMatch = leftQuery.NextMatch();
                        if (leftMatch is null)
                        {
                            state = State.Null;
                            return null;
                        }
                        return leftMatch;
                    }

                    throw new InvalidOperationException();
                case State.Merge:
                    if (leftMatch.Left < rightMatch.Left)
                    {
                        state = State.AdvanceLeft;
                        return leftMatch;
                    }
                    else
                    {
                        state = State.AdvanceRight;
                        return rightMatch;
                    }
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public void Dispose()
    {
        leftQuery?.Dispose();
        rightQuery?.Dispose();
    }
    #endregion
}
