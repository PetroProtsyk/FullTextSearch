
namespace Protsyk.PMS.FullText.Core
{
    public class PersistentIndexName : IIndexName
    {
        public static string DefaultValue = "Default";

        public string Folder { get; }
        public string FieldsType { get; set; }
        public string PostingType { get; set; }

        public PersistentIndexName(string folder)
            : this(folder, DefaultValue, DefaultValue)
        {
        }

        public PersistentIndexName(string folder, string fieldsType, string postingType)
        {
            Folder = folder;
            FieldsType = fieldsType;
            PostingType = postingType;
        }

    }
}
