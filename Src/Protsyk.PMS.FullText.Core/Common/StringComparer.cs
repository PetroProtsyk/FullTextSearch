using System.Collections.Generic;

namespace Protsyk.PMS.FullText.Core.Common
{
    public class StringComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            return string.Equals(x, y);
        }

        public int GetHashCode(string s)
        {
            int h = 0;
            for (int i = 0; i < s.Length; ++i)
            {
                h += s[i];
                h ^= 17;
                h <<= 4;
            }
            return h;
        }
    }
}