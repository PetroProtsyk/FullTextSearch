using System;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core
{
    public class TextOccurrenceSerializer : IDataSerializer<Occurrence>
    {
        public byte[] GetBytes(Occurrence value)
        {
            return System.Text.Encoding.UTF8.GetBytes(value.ToString());
        }

        public Occurrence GetValue(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public Occurrence GetValue(byte[] bytes, int startIndex)
        {
            throw new NotImplementedException();
        }
    }
}
