using System;

namespace Protsyk.PMS.FullText.Core
{
    public interface IIndexVisitor
    {
        bool VisitTerm(DictionaryTerm term);
    }
}
