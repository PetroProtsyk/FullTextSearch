using System;
using System.IO;
using System.Text.Json;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    internal sealed class PersistentIndexInfo : IDisposable
    {
        private readonly FileStorage persistentStorage;

        public static bool Exists(string folder, string fileName)
        {
            return FileStorage.Exists(Path.Combine(folder, fileName));
        }

        public PersistentIndexInfo(string folder, string fileName)
        {
            persistentStorage = new FileStorage(Path.Combine(folder, fileName));
        }

        public IndexHeaderData Read()
        {
            if (persistentStorage.Length == 0)
            {
                return null;
            }

            var buffer = new byte[persistentStorage.Length];
            persistentStorage.ReadAll(0, buffer);

            var result = JsonSerializer.Deserialize<IndexHeaderData>(buffer);
            return result;
        }

        public void Write(IFullTextIndexHeader header)
        {
            var headerData = new IndexHeaderData
            {
                Type = header.Type,
                MaxTokenSize = header.MaxTokenSize,
                NextDocumentId = header.NextDocumentId,
                CreatedDate = header.CreatedDate,
                ModifiedDate = header.ModifiedDate,
            };

            var data = JsonSerializer.SerializeToUtf8Bytes(headerData, new JsonSerializerOptions() { WriteIndented = true });

            persistentStorage.Truncate(data.Length);
            persistentStorage.WriteAll(0, data);
        }

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
    }
}
