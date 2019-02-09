
using System;
using System.IO;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Persistance
{
    //From MSDN: https://docs.microsoft.com/en-us/dotnet/api/system.io.textwriter
    //A derived class must minimally implement the Write(Char) method to make a useful instance of TextWriter.
    public class NullTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.Default;

        public NullTextWriter()
        {
        }

        public override void Write(char value)
        {
        }

        public override void Write(char[] buffer, int index, int count)
        {
        }
    }
}
