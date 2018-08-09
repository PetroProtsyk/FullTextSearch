using System;
using System.Collections.Generic;
using System.Text;

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
