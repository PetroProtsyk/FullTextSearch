using System;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
    public interface ITextEncoding
    {
        string GetName();

        string GetString(byte[] bytes, int index, int count);

        IEnumerable<byte> GetBytes(string text, int index, int count);

        int GetMaxEncodedLength(int maxTokenLength);
    }

    public static class TextEncodingExtensions
    {
        public static string GetString(this ITextEncoding encoding, byte[] bytes) => encoding.GetString(bytes, 0, bytes.Length);

        public static IEnumerable<byte> GetBytes(this ITextEncoding encoding, string text) => encoding.GetBytes(text, 0, text.Length);
    }
}
