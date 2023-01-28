namespace Protsyk.PMS.FullText.Core;

public interface ISearchQuery : IDisposable
{
    IMatch NextMatch();
}
