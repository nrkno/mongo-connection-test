namespace MongoConnectionTester.Events;

internal static class DictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> factory)
    {
        if (dictionary == null)
        {
            throw new ArgumentNullException(nameof(dictionary));
        }

        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        value = factory(key);
        dictionary[key] = value;
        return value;
    }
}