using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Protsyk.PMS.FullText.Core.Common.Persistance;

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

            using (var reader = new StringReader(System.Text.Encoding.UTF8.GetString(buffer, 0, read)))
            {
                var result = new IndexHeaderData
                {
                    Type = reader.ReadLine(),
                    MaxTokenSize = int.Parse(reader.ReadLine()),
                    NextDocumentId = ulong.Parse(reader.ReadLine()),
                    CreatedDate = DateTime.Parse(reader.ReadLine()),
                    ModifiedDate = DateTime.Parse(reader.ReadLine()),
                };

                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    result.Settings.Add(line);
                }

                return result;
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
            foreach (var setting in header.Settings)
            {
                headerText.AppendLine(setting);
            }

            var data = Encoding.UTF8.GetBytes(headerText.ToString());
            persistentStorage.WriteAll(0, data, 0, data.Length);
        }

        public void Dispose()
        {
            persistentStorage?.Dispose();
        }
    }
}
