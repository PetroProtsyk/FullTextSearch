
namespace Protsyk.PMS.FullText.Core.Collections
{
    /// <summary>
    /// Match all elements in Trie
    /// </summary>
    public class AnyMatcher<T> : IDfaMatcher<T>
    {
        public void Reset()
        {
        }

        public bool Next(T p)
        {
            return true;
        }

        public bool IsFinal()
        {
            return true;
        }

        public void Pop()
        {
        }
    }
}