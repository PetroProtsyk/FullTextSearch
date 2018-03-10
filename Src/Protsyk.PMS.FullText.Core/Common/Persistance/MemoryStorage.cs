using System.IO;

namespace Protsyk.PMS.FullText.Core.Common.Persistance
{
    public class MemoryStorage : StreamStorage<MemoryStream>
    {
        public MemoryStorage()
            : base(new MemoryStream()) { }
    }
}
