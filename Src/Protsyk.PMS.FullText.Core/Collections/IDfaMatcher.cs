
namespace Protsyk.PMS.FullText.Core.Collections
{
    public interface IDfaMatcher<in T>
    {
        void Reset();

        bool Next(T p);

        bool IsFinal();

        void Pop();
    }
}