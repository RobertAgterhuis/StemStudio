using StemStudio.Domain.Lyrics.Assist;
using WeCantSpell.Hunspell;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// <see cref="ISpellChecker"/> over the vendored Hunspell dictionaries (research R-02): OpenTaal for Dutch; SCOWL en_US, en_GB-ise and
/// en_GB-ize for English (a word is known when any of them accepts it). Dictionaries load lazily on first use and stay in memory.
/// </summary>
public sealed class HunspellSpellChecker : ISpellChecker
{
    private static readonly string[] DutchFiles = ["nl/nl.dic"];
    private static readonly string[] EnglishFiles = ["en_US/en_US.dic", "en_GB/en_GB-ise.dic", "en_GB/en_GB-ize.dic"];

    private readonly Lazy<WordList[]> _dutch;
    private readonly Lazy<WordList[]> _english;

    /// <summary>Creates the checker over a dictionary folder.</summary>
    /// <param name="root">Folder with <c>nl/</c>, <c>en_US/</c>, <c>en_GB/</c>.</param>
    /// <param name="dutchFiles">Dutch .dic files relative to the root (default the OpenTaal file).</param>
    /// <param name="englishFiles">English .dic files relative to the root (default the three SCOWL files).</param>
    public HunspellSpellChecker(string root, IReadOnlyList<string>? dutchFiles = null, IReadOnlyList<string>? englishFiles = null)
    {
        _dutch = new Lazy<WordList[]>(() => Load(root, dutchFiles ?? DutchFiles));
        _english = new Lazy<WordList[]>(() => Load(root, englishFiles ?? EnglishFiles));
    }

    /// <summary>The default folder: <c>LyricAssist/Dictionaries</c> next to the API binaries.</summary>
    public static string DefaultRoot { get; } = Path.Combine(AppContext.BaseDirectory, "LyricAssist", "Dictionaries");

    /// <inheritdoc />
    public bool IsKnown(string word, AssistLanguage language)
    {
        var normalized = Normalize(word);
        return normalized.Length > 0 && Lists(language).Any(list => list.Check(normalized));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Suggest(string word, AssistLanguage language, int max)
    {
        var normalized = Normalize(word);
        return normalized.Length == 0 || max <= 0
            ? []
            : Lists(language).SelectMany(list => list.Suggest(normalized)).Distinct(StringComparer.Ordinal).Take(max).ToList();
    }

    private WordList[] Lists(AssistLanguage language) => language == AssistLanguage.Dutch ? _dutch.Value : _english.Value;

    private static string Normalize(string? word) => (word ?? string.Empty).Trim().Replace('’', '\'');

    private static WordList[] Load(string root, IReadOnlyList<string> files) =>
        [.. files.Select(file => WordList.CreateFromFiles(Path.Combine(root, file)))];
}
