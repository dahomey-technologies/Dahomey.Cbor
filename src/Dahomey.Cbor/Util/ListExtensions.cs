namespace System.Collections.Generic
{
    public static class ListExtensions
    {
        public static int BinarySearch<T>(this List<T> list, Func<T, int> comparer)
        {
            int lo = 0;
            int hi = list.Count - 1;

            while (lo <= hi)
            {
                int i = lo + (hi - lo) / 2;
                int order = comparer(list[i]);

                if (order == 0)
                {
                    return i;
                }

                if (order < 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }

            return ~lo;
        }
    }
}
