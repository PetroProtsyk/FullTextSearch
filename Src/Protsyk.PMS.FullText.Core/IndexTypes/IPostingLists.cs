namespace Protsyk.PMS.FullText.Core;

public interface IPostingLists : IDisposable
{
    IPostingList Get(PostingListAddress address);
}
