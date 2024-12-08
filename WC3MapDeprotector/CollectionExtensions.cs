namespace WC3MapDeprotector
{
    public static class CollectionExtensions
    {
        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                hashSet.Add(item);
            }
        }

        public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            foreach (var kvp in items)
            {
                if (!dictionary.ContainsKey(kvp.Key))
                {
                    dictionary.Add(kvp.Key, kvp.Value);
                }
            }
        }
    }
}