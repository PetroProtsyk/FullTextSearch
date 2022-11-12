using System;

namespace Protsyk.PMS.FullText.Core
{
    public interface IOccurrenceWriter : IDisposable
    {
        void StartList(string token);

        void AddOccurrence(Occurrence occurrence);

        PostingListAddress EndList();

        void UpdateNextList(PostingListAddress address, PostingListAddress nextList);
    }
}
