using System.Collections;
using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    internal class BasicSkipList : ISkipList
    {
        private readonly IPostingList list;

        public BasicSkipList(IPostingList list)
        {
            this.list = list;
        }

        #region ISkipList
        public IEnumerable<Occurrence> LowerBound(Occurrence c)
        {
            var skip = false;
            foreach (var o in list)
            {
                skip = o < c;
                if (!skip)
                {
                    yield return o;
                }
            }
        }

        public IEnumerator<Occurrence> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}
