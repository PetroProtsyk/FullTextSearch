
namespace Protsyk.PMS.FullText.Core
{
    public class PersistentIndexName : IIndexName
    {
        public string Folder { get; }
        public string FieldsType { get; set; }

        public PersistentIndexName(string folder)
            : this(folder, PersistentMetadataList.Id)
        {
        }

        public PersistentIndexName(string folder, string fieldsType)
        {
            Folder = folder;
            FieldsType = fieldsType;
        }

    }
}
