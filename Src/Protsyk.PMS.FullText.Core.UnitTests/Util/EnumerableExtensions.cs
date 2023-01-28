namespace Protsyk.PMS.FullText.Core.UnitTests;

public static class EnumerableExtensions
{
    public static T[] Shuffle<T>(this IEnumerable<T> input)
    {
        var r = new Random();
        var sf = input.ToArray();
        for (int i = 0; i < sf.Length; ++i)
        {
            int i1 = r.Next(sf.Length);
            int i2 = r.Next(sf.Length);

            var t = sf[i1];
            sf[i1] = sf[i2];
            sf[i2] = t;
        }
        return sf;
    }

}
