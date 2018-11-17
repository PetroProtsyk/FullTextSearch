using System;

namespace Protsyk.PMS.FullText.Core
{
    public interface IIndexBuilder : IDisposable
    {
        void Start();
        void AddText(string text, string metadata);
        void AddFile(string fileName, string metadata);
        IndexBuilderStatistics StopAndWait();
    }

    public class IndexBuilderStatistics
    {
        public long Terms { get; set; }

        public long Occurrences { get; set; }
    }
}
