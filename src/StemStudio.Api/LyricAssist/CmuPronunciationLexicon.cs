using System.Globalization;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// <see cref="IPronunciationLexicon"/> and English <see cref="IWordList"/> over the vendored CMU Pronouncing Dictionary
/// (<c>cmudict/cmudict.dict</c>, research R-04): loaded lazily once; variant entries (<c>word(2)</c>) follow the first one.
/// </summary>
public sealed class CmuPronunciationLexicon : IPronunciationLexicon
{
    private readonly Lazy<Dictionary<string, List<IReadOnlyList<string>>>> _entries;

    /// <summary>Creates the lexicon over a <c>cmudict.dict</c> file.</summary>
    /// <param name="path">The file.</param>
    public CmuPronunciationLexicon(string path) => _entries = new(() => VendoredDataCache.Get("cmudict:" + path, () => Load(path)));

    /// <summary>The default file next to the API binaries.</summary>
    public static string DefaultPath { get; } = Path.Combine(HunspellSpellChecker.DefaultRoot, "cmudict", "cmudict.dict");

    /// <summary>Every word of the dictionary (lower case).</summary>
    public IReadOnlyCollection<string> Words => _entries.Value.Keys;

    /// <inheritdoc />
    public IReadOnlyList<IReadOnlyList<string>> Pronunciations(string word) =>
        _entries.Value.TryGetValue((word ?? string.Empty).Trim().ToLower(CultureInfo.InvariantCulture), out var found) ? found : [];

    private static Dictionary<string, List<IReadOnlyList<string>>> Load(string path)
    {
        var entries = new Dictionary<string, List<IReadOnlyList<string>>>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split('#', 2)[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var word = parts[0].Split('(', 2)[0];
            if (!entries.TryGetValue(word, out var list))
            {
                entries[word] = list = [];
            }

            list.Add(parts[1..]);
        }

        return entries;
    }
}
