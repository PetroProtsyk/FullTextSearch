namespace Protsyk.PMS.FullText.Core;

internal static class PersistentMetadataFactory
{
    public static IMetadataStorage<string> CreateStorage(string fieldsType, string folder, string fileName)
    {
        if (fieldsType == PersistentIndexName.DefaultValue || fieldsType == PersistentMetadataList.Id)
        {
            return new PersistentMetadataList(folder, fileName);
        }

        if (fieldsType == PersistentMetadataBtree.Id)
        {
            return new PersistentMetadataBtree(folder, fileName);
        }

        if (fieldsType == PersistentMetadataHashTable.Id)
        {
            return new PersistentMetadataHashTable(folder, fileName);
        }

        throw new NotSupportedException($"Fields type {fieldsType} is not supported");
    }

    public static string GetName(string fieldsType)
    {
        if (fieldsType == PersistentIndexName.DefaultValue)
        {
            return PersistentMetadataList.Id;
        }
        return fieldsType;
    }
}
