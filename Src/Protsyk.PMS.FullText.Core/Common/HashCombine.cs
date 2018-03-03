namespace Protsyk.PMS.FullText.Core
{
    public static class HashCombine
    {
        public static int Combine(params int[] values)
        {
            unchecked
            {
                int hash = 5381;
                for (int i = 0; i < values.Length; ++i)
                {
                    hash = ((hash << 5) + hash) ^ values[i];
                }
                return hash;
            }
        }
    }
}
