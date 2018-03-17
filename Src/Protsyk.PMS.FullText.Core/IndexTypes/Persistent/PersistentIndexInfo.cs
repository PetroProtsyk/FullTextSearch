using System;
using System.IO;
using System.Text;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    internal class PersistentIndexInfo : IDisposable
    {
        private readonly FileStorage persistentStorage;

        public PersistentIndexInfo(string folder, string fileName)
        {
            //TODO: Do not create file when index is opened for reading (i.e. index does not exists)
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

            using (var reader = new StringReader(System.Text.Encoding.UTF8.GetString(buffer)))
            {
                return new IndexHeaderData
                {
                    Type = reader.ReadLine(),
                    MaxTokenSize = int.Parse(reader.ReadLine()),
                    NextDocumentId = ulong.Parse(reader.ReadLine()),
                    CreatedDate = DateTime.Parse(reader.ReadLine()),
                    ModifiedDate = DateTime.Parse(reader.ReadLine())
                };
            }
        }

        public void Write(IFullTextIndexHeader header)
        {
            var headerText = new StringBuilder();
            headerText.AppendLine(header.Type);
            headerText.AppendLine(header.MaxTokenSize.ToString());
            headerText.AppendLine(header.NextDocumentId.ToString());
            headerText.AppendLine(header.CreatedDate.ToString("o"));
            headerText.AppendLine(header.ModifiedDate.ToString("o"));
            var data = Encoding.UTF8.GetBytes(headerText.ToString());
            persistentStorage.WriteAll(0, data, 0, data.Length);
        }

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
    }
}
