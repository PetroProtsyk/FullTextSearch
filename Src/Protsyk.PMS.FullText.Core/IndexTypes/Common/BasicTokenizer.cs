using System;
using System.Collections.Generic;
using System.IO;

namespace Protsyk.PMS.FullText.Core
{
    public class BasicTokenizer : ITextTokenizer
    {
        #region Fields
        private readonly int maxTokenSize;
        #endregion

        #region Constructor
        public BasicTokenizer(int maxTokenSize)
        {
            this.maxTokenSize = maxTokenSize;
        }
        #endregion

        #region ITextTokenizer
        public IEnumerable<ScopedToken> Tokenize(TextReader reader)
        {
            var buffer = new char[maxTokenSize];
            var length = 0;
            var offset = 0;
            var startOffset = 0;
            var state = States.Read;

            var readBuffer = new char[maxTokenSize];
            var readData = 0;
            var readIndex = 0;

            while (state != States.Eof)
            {
                switch (state)
                {
                    case States.Read:
                        {
                            readData = reader.Read(readBuffer, 0, readBuffer.Length);
                            if (readData == 0)
                            {
                                state = States.BeforeEof;
                            }
                            else
                            {
                                state = States.Token;
                            }
                            readIndex = 0;
                            break;
                        }
                    case States.Token:
                        {
                            if (readIndex >= readData)
                            {
                                state = States.Read;
                            }
                            else
                            {
                                var next = readBuffer[readIndex++];
                                if (IsTokenChar(next))
                                {
                                    if (length < buffer.Length)
                                    {
                                        buffer[length] = char.ToLowerInvariant(next);
                                    }
                                    if (length == 0)
                                    {
                                        startOffset = offset;
                                    }
                                    ++length;
                                }
                                else
                                {
                                    state = States.TokenEnd;
                                }
                                ++offset;
                            }
                            break;
                        }
                    case States.BeforeEof:
                    case States.TokenEnd:
                        {
                            if (length > 0)
                            {
                                yield return new ScopedToken(startOffset, length, buffer);
                                length = 0;
                                startOffset = -1;
                            }
                            state = state == States.BeforeEof ? States.Eof : States.Token;
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        private bool IsTokenChar(char c)
        {
            if (char.IsLetterOrDigit(c))
            {
                return true;
            }

            if (c == '-' || c == '_')
            {
                return true;
            }

            return false;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
        }
        #endregion

        #region Types
        private enum States
        {
            Read,
            Token,
            TokenEnd,
            BeforeEof,
            Eof
        }
        #endregion
    }
}
