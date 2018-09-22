using System;
using System.Collections.Generic;
using System.IO;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    internal class PersistentMetadataHashTable : IMetadataStorage<string>
    {
        private const int InitialHashCapacity = 65536;

        public static readonly string Id = "HashTable";

        private readonly PersistentHashTable<ulong, string> fields;

        public PersistentMetadataHashTable(string folder, string fileNameFields)
        {
            fields = new PersistentHashTable<ulong, string>(new FileStorage(Path.Combine(folder, fileNameFields)), InitialHashCapacity, EqualityComparer<ulong>.Default);
        }

        public string GetMetadata(ulong id)
        {
            return fields[id];
        }

        public void SaveMetadata(ulong id, string data)
        {
            fields[id] = data;
        }

        public void Dispose()
        {
            fields?.Dispose();
        }
    }
}
