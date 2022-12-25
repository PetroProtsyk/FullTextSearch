namespace Protsyk.PMS.FullText.Core;

public class DuplicateTermException : BaseException
{
    public DuplicateTermException(string term)
        : base($"A term \"{term}\" already present in the dictionary")
    {
    }
}
