namespace Protsyk.PMS.FullText.Core;

public class SingleMatch : IMatch
{
    private readonly Occurrence occurrence;

    public SingleMatch(Occurrence occurrence)
    {
        this.occurrence = occurrence;
    }

    public override string ToString()
    {
        return $"{{{occurrence}}}";
    }

    public IEnumerable<Occurrence> GetOccurrences()
    {
        yield return occurrence;
    }

    public Occurrence Left => occurrence;

    public Occurrence Right => occurrence;

    public Occurrence Max => occurrence;

    public Occurrence Min => occurrence;

    public ulong DocumentId => occurrence.DocumentId;
}
