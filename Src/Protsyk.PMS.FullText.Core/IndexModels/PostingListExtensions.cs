namespace Protsyk.PMS.FullText.Core
{
    public static class PostingListExtensions
    {
        public static ISkipList AsSkipList(this IPostingList list)
        {
            if (list is ISkipList native)
            {
                return native;
            }

            return new BasicSkipList(list);
        }
    }
}