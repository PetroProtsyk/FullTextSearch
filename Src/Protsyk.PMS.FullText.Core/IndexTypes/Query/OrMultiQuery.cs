using System;
using System.Collections.Generic;
using System.Linq;

namespace Protsyk.PMS.FullText.Core
{
    public class OrMultiQuery : ISearchQuery
    {
        #region Fields
        private readonly ISearchQuery[] queries;
        private readonly SortedDictionary<IMatch, ISearchQuery> heap;
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
            this.heap = new SortedDictionary<IMatch, ISearchQuery>(MatchComparer.Instance);
            this.state = States.Initial;
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
                                    heap.Add(match, searchQuery);
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
                        return heap.First().Key;
                    case States.MatchAdvance:
                        {
                            var top = heap.First();
                            heap.Remove(top.Key);
                            var searchQuery = top.Value;
                            var match = searchQuery.NextMatch();
                            if (match != null)
                            {
                                heap.Add(match, searchQuery);
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
}
