using System;
using System.IO;
using PMS.Common.Collections.List;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    internal class PersistentMetadataList : IMetadataStorage<string>
    {
        private PersistentDictionary<string> fields;

        public PersistentMetadataList(string folder, string fileNameFields)
        {
            var dataFile = Path.Combine(folder, Path.GetFileNameWithoutExtension(fileNameFields) + "-data" + Path.GetExtension(fileNameFields));
            var keysFile = Path.Combine(folder, Path.GetFileNameWithoutExtension(fileNameFields) + "-keys" + Path.GetExtension(fileNameFields));
            fields = new PersistentDictionary<string>(new FileStorage(dataFile), new FileStorage(keysFile));
        }

        public string GetMetadata(ulong id)
        {
            return fields[(long)id];
        }

        public void SaveMetadata(ulong id, string data)
        {
            fields[(long)id] = data;
        }

        public void Dispose()
        {
            fields?.Dispose();
        }
    }
}
