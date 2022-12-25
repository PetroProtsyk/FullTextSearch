namespace Protsyk.PMS.FullText.Core;

public class TermNotFoundException : BaseException
{
    public TermNotFoundException(string term)
        : base($"A term \"{term}\" is not found in the dictionary")
    {
    }
}
