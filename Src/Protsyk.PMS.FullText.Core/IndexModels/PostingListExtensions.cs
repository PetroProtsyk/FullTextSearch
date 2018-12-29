using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    public static class PostingListExtensions
    {
        public static ISkipList AsSkipList(this IPostingList list)
        {
            var native = list as ISkipList;
            if (native != null)
            {
                return native;
            }

            return new BasicSkipList(list);
        }
    }

}
