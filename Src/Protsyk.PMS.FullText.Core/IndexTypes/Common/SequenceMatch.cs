using System.Linq;

namespace Protsyk.PMS.FullText.Core;

public class SequenceMatch : IMatch
{
    private readonly List<IMatch> matches;

    public SequenceMatch(params IMatch[] matches)
    {
        this.matches = new List<IMatch>(matches);
    }

    public override string ToString()
    {
        return $"{{{string.Join(", ", GetOccurrences().Select(o => o.ToString()))}}}";
    }

    public IEnumerable<Occurrence> GetOccurrences()
    {
        foreach (var match in matches)
        {
            foreach (var occurrence in match.GetOccurrences())
            {
                yield return occurrence;
            }
        }
    }

    public Occurrence Left => matches.First().Left;

    public Occurrence Right => matches.Last().Right;

    public Occurrence Max => GetOccurrences().Max();

    public Occurrence Min => GetOccurrences().Min();

    public ulong DocumentId => GetOccurrences().Min().DocumentId;
}
