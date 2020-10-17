using Protsyk.PMS.FullText.Core.Common.Persistance;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Protsyk.PMS.FullText.Core
{
    internal class PersistentIndexInfo : IDisposable
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
            var buffer = new byte[1024];
            int read = persistentStorage.Read(0, buffer, 0, buffer.Length);
            if (read == 0)
            {
                return null;
            }

            using (var reader = new StringReader(Encoding.UTF8.GetString(buffer, 0, read)))
            {
                var result = JsonSerializer.Deserialize<IndexHeaderData>(reader.ReadToEnd());
                return result;
            }
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

            var headerText = JsonSerializer.Serialize<IndexHeaderData>(headerData, new JsonSerializerOptions() { WriteIndented = true });
            var data = Encoding.UTF8.GetBytes(headerText);
            persistentStorage.WriteAll(0, data, 0, data.Length);
        }

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
    }
}
