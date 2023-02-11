namespace Protsyk.PMS;

internal ref struct StringSplitter
{
    private readonly ReadOnlySpan<char> _text;
    private readonly char _separator;

    private int _position;

    public StringSplitter(ReadOnlySpan<char> text, char separator)
    {
        _text = text;
        _separator = separator;
        _position = 0;
    }

    public bool TryRead(out ReadOnlySpan<char> result)
    {
        if (_position == _text.Length)
        {
            result = default;

            return false;
        }

        int start = _position;

        int separatorIndex = _text.Slice(_position).IndexOf(_separator);

        if (separatorIndex > -1)
        {
            _position += separatorIndex + 1;

            result = _text.Slice(start, separatorIndex);
        }
        else
        {
            _position = _text.Length;

            result = _text.Slice(start);
        }

        return true;
    }
}
