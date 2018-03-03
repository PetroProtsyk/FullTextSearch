using System;
using System.Collections.Generic;
using System.IO;

namespace PMS.FullText.Core.Tokenizer
{
    public interface ITextTokenizer : IDisposable
    {
        IEnumerable<ScopedToken> Tokenize(TextReader reader);
    }

    /// <summary>
    /// Token that exists in the context of IEnumerator instance.
    /// When tokenizer moves to the next token, this instance becomes invalid
    /// </summary>
    public struct ScopedToken
    {
        public int CharOffset { get; }

        public int Length { get; }

        public char[] Text { get; }

        public string AsString() => new string(Text, 0, Math.Min(Text.Length, Length));

        public ScopedToken(int charOffset, int length, char[] buffer)
        {
            CharOffset = charOffset;
            Length = length;
            Text = buffer;
        }
    }
}
