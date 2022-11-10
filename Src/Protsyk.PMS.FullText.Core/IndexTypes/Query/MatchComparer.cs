using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core
{
    public class MatchComparer : IComparer<IMatch>
    {
        public static readonly MatchComparer Instance = new MatchComparer();

        public int Compare(IMatch x, IMatch y)
        {
            using (var occurrenceX = x.GetOccurrences().GetEnumerator())
            using (var occurrenceY = y.GetOccurrences().GetEnumerator())
            {
                while (true)
                {
                    if (!occurrenceX.MoveNext())
                    {
                        if (!occurrenceY.MoveNext())
                        {
                            return 0;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    else
                    {
                        if (!occurrenceY.MoveNext())
                        {
                            return 1;
                        }
                        else
                        {
                            var r = occurrenceX.Current.CompareTo(occurrenceY.Current);
                            if (r != 0)
                            {
                                return r;
                            }
                        }
                    }
                }
            }
        }
    }
}
