using System;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
    public class TextEncodingFactory
    {
        public static string GetName(string textEncoding)
        {
            if (textEncoding == "Default")
                return TextEncoding.Default.GetName();

            return textEncoding;
        }

        public static ITextEncoding GetByName(string textEncoding)
        {
            var name = GetName(textEncoding);

            var encoding = Encoding.GetEncoding(name);
            if (encoding != null)
                return new TextEncoding(encoding);

            throw new Exception("Encoding is not defined");
        }

    }
}
