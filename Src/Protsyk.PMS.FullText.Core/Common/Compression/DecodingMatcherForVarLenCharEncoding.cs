using System;
using System.Collections.Generic;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.Common.Compression
{
    internal class DecodingMatcherForVarLenCharEncoding : IDfaMatcher<byte>
    {
        private readonly IDfaMatcher<char> matcher;
        private readonly VarLenCharEncoding encoding;
        private readonly Stack<int> matcherSteps;

        private IBitDecoder decoder;
        private int decoderSteps;

        public DecodingMatcherForVarLenCharEncoding(IDfaMatcher<char> matcher, VarLenCharEncoding encoding)
        {
            this.matcher = matcher;
            this.encoding = encoding;
            this.matcherSteps = new Stack<int>();
            Reset();
        }

        public bool IsFinal()
        {
            return matcher.IsFinal();
        }

        public bool Next(byte p)
        {
            for (int i = 0; i < 8; ++i)
            {
                var c = VarLenCharEncoding.TerminalChar;
                if ((p & (1 << (7 - i))) > 0)
                {
                    c = decoder.Next(true);
                }
                else
                {
                    c = decoder.Next(false);
                }

                ++decoderSteps;

                if (c != VarLenCharEncoding.TerminalChar)
                {
                    if (!matcher.Next(c))
                    {
                        for (int j = 0; j <= i; ++j)
                        {
                            if ((matcherSteps.Count > 0) && (matcherSteps.Peek() == decoderSteps))
                            {
                                matcherSteps.Pop();
                                matcher.Pop();
                            }

                            --decoderSteps;
                            decoder.Pop();
                        }

                        return false;
                    }
                    else
                    {
                        matcherSteps.Push(decoderSteps);
                    }
                }
            }

            return true;
        }

        public void Pop()
        {
            for (int j = 0; j < 8; ++j)
            {
                if ((matcherSteps.Count > 0) && (matcherSteps.Peek() == decoderSteps))
                {
                    matcherSteps.Pop();
                    matcher.Pop();
                }

                --decoderSteps;
                decoder.Pop();
            }
        }

        public void Reset()
        {
            decoder = encoding.GetDecoder();
            decoderSteps = 0;

            matcher.Reset();
            matcherSteps.Clear();
        }
    }
}
