using System.Globalization;
using System.Text;
using StemStudio.Agent.Resources;

namespace StemStudio.Agent.Policy;

/// <summary>
/// The agent's artist-imitation pre-check (FR-025, tool catalogue "Guardrail artist-imitation"): the same matching as
/// the 009 backend guard (whole words, case- and diacritics-insensitive, trailing * as prefix) over
/// the embedded 009 lexicon (<see cref="EmbeddedLexicon"/>) plus the library's artist names. The backend guard stays authoritative.
/// </summary>
public sealed class ImitationGuard
{
    private static readonly HashSet<string> GenreWords = new(EmbeddedLexicon.GenreWords.Select(Normalize), StringComparer.Ordinal);
    private static readonly string[] Phrases = [.. EmbeddedLexicon.Phrases.Select(PhrasePattern)];

    private readonly string[] _names;

    private ImitationGuard(string[] names) => _names = names;

    /// <summary>Creates a guard for the deny-list plus the library's artists.</summary>
    /// <param name="libraryArtists">Artist names; blank, one-letter and genre-word names are skipped.</param>
    /// <returns>The guard.</returns>
    public static ImitationGuard Create(IEnumerable<string?> libraryArtists)
    {
        ArgumentNullException.ThrowIfNull(libraryArtists);
        var library = libraryArtists.Select(Normalize).Where(name => name.Length > 1 && !GenreWords.Contains(name));
        return new ImitationGuard([.. EmbeddedLexicon.DenyList.Select(Normalize).Concat(library).Distinct(StringComparer.Ordinal).Select(name => $" {name} ")]);
    }

    /// <summary>True when the text contains an imitation phrase or an artist name.</summary>
    /// <param name="text">Free text.</param>
    /// <returns>True on a hit.</returns>
    public bool IsImitation(string? text)
    {
        var padded = $" {Normalize(text)} ";
        return padded.Length > 2 && (Phrases.Any(padded.Contains) || _names.Any(padded.Contains));
    }

    /// <summary>Lower case without diacritics; runs of non-letters/digits become one space; trimmed.</summary>
    /// <param name="text">Text.</param>
    /// <returns>Normalised text.</returns>
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
