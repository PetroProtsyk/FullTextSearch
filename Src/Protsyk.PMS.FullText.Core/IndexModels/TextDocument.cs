using System.Linq;
using System.Text;

namespace Protsyk.PMS.FullText.Core;

public interface ITextDocument
{
    string TokenAt(int offset);
    string Token(int index);
}

public class TextDocument : ITextDocument
{
    #region Fields
    private readonly TextPosition[] positions;
    private readonly string text;
    #endregion

    #region Constructor
    public TextDocument(string text, IEnumerable<TextPosition> positions)
    {
        this.text = text;
        this.positions = positions.ToArray();
    }
    #endregion

    #region Methods
    #endregion

    #region ITextDocument
    public string TokenAt(int offset)
    {
        if (positions.Length > 4)
        {
            var a = 0;
            var b = positions.Length;
            while (a != b)
            {
                var mid = (a + b) >> 1;
                var s = positions[mid];
                if (s.Offset > offset)
                {
                    b = mid;
                }
                else if (s.Offset < offset)
                {
                    a = mid + 1;
                }
                else
                {
                    a = mid;
                    b = mid;
                }
            }

            if (a == positions.Length || positions[a].Offset > offset)
            {
                if (a > 0)
                {
                    a = a - 1;
                }
            }

            if (a >= 0 && a < positions.Length)
            {
                if (positions[a].Offset <= offset && (positions[a].Offset + positions[a].Length) > offset)
                {
                    return text.Substring(positions[a].Offset, positions[a].Length);
                }
            }
        }
        else
        {
            for (int a = 0; a<positions.Length; ++a)
            {
                if (positions[a].Offset <= offset && (positions[a].Offset + positions[a].Length) > offset)
                {
                    return text.Substring(positions[a].Offset, positions[a].Length);
                }
            }
        }

        throw new Exception($"Whitespace? No token");
    }

    public string Token(int index)
    {
        return text.Substring(positions[index].Offset, positions[index].Length);
    }

    public string Annotate(IEnumerable<int> hits)
    {
        var hi = new StringBuilder(text.ToLowerInvariant());
        foreach (var hit in hits)
        {
            for (int i=0; i<positions[hit - 1].Length; ++i)
            {
                hi[i + positions[hit - 1].Offset] = char.ToUpperInvariant(hi[i + positions[hit - 1].Offset]);
            }
        }
        return hi.ToString();
    }
    #endregion
}
