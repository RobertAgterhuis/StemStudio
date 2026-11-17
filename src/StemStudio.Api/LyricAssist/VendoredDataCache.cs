using System.Collections.Concurrent;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Process-wide cache of parsed vendored data files (dictionaries, CMUdict, word lists): the files are read-only build output, so
/// each is parsed once per process, also when several hosts run in one process (tests).
/// </summary>
public static class VendoredDataCache
{
    private static readonly ConcurrentDictionary<string, Lazy<object>> Entries = new(StringComparer.Ordinal);

    /// <summary>The cached value of a key, created once.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Key (kind and file paths).</param>
    /// <param name="create">Creates the value.</param>
    /// <returns>The value.</returns>
    public static T Get<T>(string key, Func<T> create)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(create);
        return (T)Entries.GetOrAdd(key, _ => new Lazy<object>(() => create(), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }
}
