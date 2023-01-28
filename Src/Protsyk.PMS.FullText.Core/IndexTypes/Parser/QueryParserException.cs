namespace Protsyk.PMS.FullText.Core;

public class QueryParserException : Exception
{
    public int Position { get; }

    public QueryParserException(string message)
        : this(message, -1)
    {
    }

    public QueryParserException(string message, int position)
        : base(message)
    {
        Position = position;
    }
}
