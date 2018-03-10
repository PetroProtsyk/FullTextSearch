
namespace Protsyk.PMS.FullText.Core
{
    public class PersistentIndexName : IIndexName
    {
        public string Folder { get; }

        public PersistentIndexName(string folder)
        {
            Folder = folder;
        }
    }
}
