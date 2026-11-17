using System.Globalization;
using System.Text.RegularExpressions;

namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A word of a lyric line.</summary>
/// <param name="Start">UTF-16 offset in the line.</param>
/// <param name="Text">The word as written.</param>
public sealed record LyricToken(int Start, string Text);

/// <summary>Splits lyric lines into words: letters and digits with inner apostrophes or hyphens, a leading or trailing apostrophe.</summary>
public static partial class LyricTokens
{
    /// <summary>The words of a line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The words in order.</returns>
    public static IReadOnlyList<LyricToken> Of(string line) =>
        Word().Matches(line ?? string.Empty).Select(match => new LyricToken(match.Index, match.Value)).ToList();

    /// <summary>The word in lower case with the typographic apostrophe normalised.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The key.</returns>
    public static string Key(string word) => (word ?? string.Empty).Replace('’', '\'').ToLower(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(?<![\p{L}\p{N}'’])['’]?[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*(?:(?<=in)['’](?![\p{L}\p{N}]))?", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}

/// <summary>
/// The curated poetic-licence lexicon per language (research R-03 step 2; embedded <c>licence-lexicon-{nl,en}.txt</c>) plus the
/// form rules: Dutch dropped final <c>-n</c> (<c>lope</c>), English <c>-in'</c> endings (<c>runnin'</c>).
/// </summary>
public static class LicenceLexicon
{
    private static readonly Dictionary<string, SuggestionType> Dutch = Load("licence-lexicon-nl.txt");
    private static readonly Dictionary<string, SuggestionType> English = Load("licence-lexicon-en.txt");

    /// <summary>The licence type of a lexicon word, or null.</summary>
    /// <param name="word">The word as written.</param>
    /// <param name="language">Language.</param>
    /// <returns>The type or null.</returns>
    public static SuggestionType? TypeOf(string word, AssistLanguage language) =>
        (language == AssistLanguage.Dutch ? Dutch : English).TryGetValue(LyricTokens.Key(word), out var type) ? type : null;

    /// <summary>The licence type of a word by lexicon or form rule (the form rules need the spell checker).</summary>
    /// <param name="word">The word as written.</param>
    /// <param name="language">Language.</param>
    /// <param name="spelling">Spell checker for the form rules.</param>
    /// <returns>The type or null.</returns>
    public static SuggestionType? Recognise(string word, AssistLanguage language, ISpellChecker spelling)
    {
        ArgumentNullException.ThrowIfNull(spelling);
        if (TypeOf(word, language) is { } type)
        {
            return type;
        }

        var key = LyricTokens.Key(word);
        return language == AssistLanguage.Dutch
            ? DroppedFinalN(key, spelling)
            : DroppedG(key, spelling);
    }

    private static SuggestionType? DroppedFinalN(string key, ISpellChecker spelling) =>
        key.Length > 2 && key.EndsWith('e') && spelling.IsKnown(key + "n", AssistLanguage.Dutch) ? SuggestionType.LicenceElision : null;

    private static SuggestionType? DroppedG(string key, ISpellChecker spelling) =>
        key.Length > 3 && key.EndsWith("in'", StringComparison.Ordinal) && spelling.IsKnown(key[..^1] + "g", AssistLanguage.English) ? SuggestionType.LicenceElision : null;

    private static Dictionary<string, SuggestionType> Load(string resource) => EmbeddedText.Lines(resource)
        .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .ToDictionary(parts => LyricTokens.Key(parts[0]), parts => Type(parts[1]), StringComparer.Ordinal);

    private static SuggestionType Type(string name) => name switch
    {
        "elision" => SuggestionType.LicenceElision,
        "archaism" => SuggestionType.LicenceArchaism,
        "sound-spelling" => SuggestionType.LicenceSoundSpelling,
        _ => SuggestionType.LicenceSlang,
    };
}
