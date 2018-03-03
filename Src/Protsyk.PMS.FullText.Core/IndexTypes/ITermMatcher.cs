using System;

namespace Protsyk.PMS.FullText.Core
{
     public interface ITermMatcher<in T>
    {
        void Reset();

        bool Next(T p);

        bool IsFinal();

        void Pop();
    }
}