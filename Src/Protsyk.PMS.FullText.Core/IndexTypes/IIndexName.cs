using System;

namespace Protsyk.PMS.FullText.Core
{
    /// <summary>
    /// Name of the index.
    /// Different index types will have different implementations of this
    /// interface.
    /// </summary>
    public interface IIndexName
    {
    }

    /// <summary>
    /// Name of the index stored in the folder
    /// </summary>
    public class IndexFolderName : IIndexName
    {
        private readonly string folder;

        public IndexFolderName(string folder)
        {
            this.folder = folder;
        }
    }
}
