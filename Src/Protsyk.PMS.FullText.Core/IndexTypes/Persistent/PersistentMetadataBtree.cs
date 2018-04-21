using System;
using System.Collections.Generic;
using System.IO;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    internal class PersistentMetadataBtree : IMetadataStorage<string>
    {
        private BtreePersistent<ulong, string> fields;

        public PersistentMetadataBtree(string folder, string fileNameFields)
        {
            fields = new BtreePersistent<ulong, string>(new FileStorage(Path.Combine(folder, fileNameFields)), 32);
        }

        public string GetMetadata(ulong id)
        {
            return fields[id];
        }

        public void SaveMetadata(ulong id, string data)
        {
            fields.Add(id, data);
        }

        public void Dispose()
        {
            fields?.Dispose();
        }
    }
}
