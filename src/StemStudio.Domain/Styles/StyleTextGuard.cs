using System.Globalization;
using System.Text;

namespace StemStudio.Domain.Styles;

/// <summary>
/// Guard against artist imitation (ADR-018 D8): refuses texts with an imitation phrase (English and Dutch, versioned
/// resource) or an artist name from the user's library or the curated deny-list. Matching is case- and
/// diacritics-insensitive on whole words; a phrase ending in <c>*</c> matches word prefixes. A deny-list is never
/// complete: the guard is a safety net, not a guarantee.
/// </summary>
public sealed class StyleTextGuard
{
    /// <summary>The message of every guard error.</summary>
    public const string Message = "Describe the music (genre, instruments, mood) instead of naming an artist or saying \"sounds like\".";

    private static readonly HashSet<string> GenreWords = new(EmbeddedText.Lines("genre-words.v1.txt").Select(Normalize), StringComparer.Ordinal);

    private readonly string[] _phrases;
    private readonly string[] _names;

    private StyleTextGuard(string[] phrases, string[] names) => (_phrases, _names) = (phrases, names);

    /// <summary>The imitation phrases (resource <c>imitation-phrases.v1.txt</c>).</summary>
    public static IReadOnlyList<string> Phrases { get; } = EmbeddedText.Lines("imitation-phrases.v1.txt");

    /// <summary>The curated artist names (resource <c>artist-denylist.v1.txt</c>).</summary>
    public static IReadOnlyList<string> DenyList { get; } = EmbeddedText.Lines("artist-denylist.v1.txt");

    /// <summary>Creates a guard with the deny-list plus the artists of the user's library.</summary>
    /// <param name="libraryArtists">Artist names of the user's tracks; blank, one-letter and genre-word names are skipped.</param>
    /// <returns>The guard.</returns>
    public static StyleTextGuard Create(IEnumerable<string?> libraryArtists)
    {
        ArgumentNullException.ThrowIfNull(libraryArtists);
        var library = libraryArtists.Select(Normalize).Where(name => name.Length > 1 && !GenreWords.Contains(name));
        var names = DenyList.Select(Normalize).Concat(library).Distinct(StringComparer.Ordinal).Select(name => $" {name} ").ToArray();
        var phrases = Phrases.Select(PhrasePattern).ToArray();
        return new StyleTextGuard(phrases, names);
    }

    /// <summary>Whether a text contains an imitation phrase or an artist name.</summary>
    /// <param name="text">The text.</param>
    /// <returns>True on a hit.</returns>
    public bool IsImitation(string? text)
    {
        var padded = $" {Normalize(text)} ";
        return padded.Length > 2 && (_phrases.Any(padded.Contains) || _names.Any(padded.Contains));
    }

    /// <summary>Checks every text of a style.</summary>
    /// <param name="fields">The style fields.</param>
    /// <returns>One error per offending field (name, subGenre, instrumentation, moods, description).</returns>
    public IReadOnlyList<DomainError> Check(StyleFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var errors = new ValidationErrors();
        errors.Check(!IsImitation(fields.Name), "name", Message);
        errors.Check(!IsImitation(fields.SubGenre), "subGenre", Message);
        errors.Check(!fields.Instrumentation.Any(IsImitation), "instrumentation", Message);
        errors.Check(!fields.Moods.Any(IsImitation), "moods", Message);
        errors.Check(!IsImitation(fields.Description), "description", Message);
        return errors.Errors;
    }

    /// <summary>Lower case without diacritics; every run of non-letters and non-digits becomes one space; trimmed.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalised text.</returns>
    public static string Normalize(string? text)
    {
        var builder = new StringBuilder();
        foreach (var c in (text ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            Append(builder, c);
        }

        return builder.ToString().Trim();
    }

    private static void Append(StringBuilder builder, char c)
    {
        if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
        {
            return;
        }

        if (char.IsLetterOrDigit(c))
        {
            builder.Append(char.ToLowerInvariant(c));
        }
        else if (builder.Length > 0 && builder[^1] != ' ')
        {
            builder.Append(' ');
        }
    }

    private static string PhrasePattern(string phrase) =>
        phrase.EndsWith('*') ? $" {Normalize(phrase.TrimEnd('*'))}" : $" {Normalize(phrase)} ";
}
