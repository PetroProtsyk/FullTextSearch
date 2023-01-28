using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.Common.Compression;

internal sealed class DecodingMatcherForUTF8 : IDfaMatcher<byte>
{
    private readonly IDfaMatcher<char> matcher;
    private int symbol;
    private int index;
    private int tail;
    private readonly ValueTuple<int, int>[] stack;

    public DecodingMatcherForUTF8(IDfaMatcher<char> matcher, int maxLength)
    {
        this.matcher = matcher;
        this.stack = new ValueTuple<int, int>[maxLength];
    }

    public bool IsFinal()
    {
        if (tail > 0)
        {
            return false;
        }
        return matcher.IsFinal();
    }

    public bool Next(byte p)
    {
        var state = (symbol, tail);

        if (tail > 0)
        {
            if (((p & 0b1000_0000) == 0) || ((p & 0b0100_0000) != 0))
            {
                throw new Exception("Not UTF8");
            }

            --tail;
            symbol = (symbol << 6) | (p & 0b0011_1111);
            if (tail > 0)
            {
                stack[index++] = state;
                return true;
            }
            else
            {
                if (matcher.Next((char)symbol))
                {
                    stack[index++] = state;
                    stack[index++] = (0, -1);
                    return true;
                }

                symbol = state.Item1;
                tail = state.Item2;
                return false;
            }
        }
        else if (p < 0b1000_0000)
        {
            tail = 0;
            symbol = p;
            if (matcher.Next((char)p))
            {
                stack[index++] = state;
                stack[index++] = (0, -1);
                return true;
            }
            symbol = state.Item1;
            tail = state.Item2;
            return false;
        }
        else if (p < 0b1110_0000)
        {
            stack[index++] = state;
            tail = 1;
            symbol = p & 0b0001_1111;
            return true;
        }
        else if (p < 0b1111_0000)
        {
            stack[index++] = state;
            tail = 2;
            symbol = p & 0b0000_1111;
            return true;
        }
        else if (p < 0b1111_1000)
        {
            stack[index++] = state;
            tail = 3;
            symbol = p & 0b0000_0111;
            return true;
        }

        throw new Exception("Not UTF8");
    }

    public void Pop()
    {
        if (index == 0)
            throw new InvalidOperationException();

        --index;

        if (stack[index].Item2 == -1)
        {
            if (index == 0)
                throw new InvalidOperationException();

            --index;
            matcher.Pop();
        }

        symbol = stack[index].Item1;
        tail = stack[index].Item2;
    }

    public void Reset()
    {
        tail = 0;
        index = 0;
        symbol = 0;
        matcher.Reset();
    }
}
