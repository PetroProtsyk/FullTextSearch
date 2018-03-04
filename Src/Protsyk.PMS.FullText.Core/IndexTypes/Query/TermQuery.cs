using System;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    public class TermQuery : ISearchQuery
    {
        #region Fields
        private readonly IPostingList postings;
        private readonly MatchIterator matchIterator;
        private bool consumed;
        #endregion

        #region Methods
        public TermQuery(IPostingList postings)
        {
            this.postings = postings;
            this.matchIterator = new MatchIterator(postings.GetEnumerator());
            this.consumed = false;
        }
        #endregion

        #region ISearchQuery
        public IMatch NextMatch()
        {
            if (consumed)
            {
                return null;
            }

            if (matchIterator.NextMatch())
            {
                return matchIterator;
            }

            consumed = true;
            return null;
        }

        public void Dispose()
        {
            matchIterator?.Dispose();
        }
        #endregion

        #region Types
        private class MatchIterator: IMatch, IDisposable
        {
            private readonly IEnumerator<Occurrence> postings;

            public MatchIterator(IEnumerator<Occurrence> postings)
            {
                this.postings = postings;
            }

            public bool NextMatch()
            {
                if (postings.MoveNext())
                {
                    return true;
                }
                return false;
            }

            public override string ToString()
            {
                return $"{{{postings.Current.ToString()}}}";
            }

            public IEnumerable<Occurrence> GetOccurrences()
            {
                yield return postings.Current;
            }

            public Occurrence Left => postings.Current;

            public Occurrence Right => postings.Current;

            public Occurrence Max => postings.Current;

            public Occurrence Min => postings.Current;

            public ulong DocumentId => postings.Current.DocumentId;

            public void Dispose()
            {
                postings?.Dispose();
            }
        }
        #endregion
    }
}
