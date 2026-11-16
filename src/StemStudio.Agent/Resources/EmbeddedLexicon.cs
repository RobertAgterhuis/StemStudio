namespace StemStudio.Agent.Resources;

/// <summary>
/// The 009 imitation lexicon v1 (ADR-018 D8), read from the Domain's resource files that StemStudio.Agent embeds as links
/// (<c>src/StemStudio.Domain/Styles/*.v1.txt</c>, logical names <c>StemStudio.Agent.Lexicon.&lt;file&gt;</c>), so the agent's
/// pre-check uses the same lists as the backend's authoritative guard without a Domain reference.
/// </summary>
public static class EmbeddedLexicon
{
    private const string Prefix = "StemStudio.Agent.Lexicon.";

    /// <summary>Imitation phrases (English and Dutch); a trailing * matches word prefixes.</summary>
    public static IReadOnlyList<string> Phrases { get; } = Lines("imitation-phrases.v1.txt");

    /// <summary>Curated artist deny-list.</summary>
    public static IReadOnlyList<string> DenyList { get; } = Lines("artist-denylist.v1.txt");

    /// <summary>Genre and common music words: library artist names equal to one of these are ignored.</summary>
    public static IReadOnlyList<string> GenreWords { get; } = Lines("genre-words.v1.txt");

    /// <summary>The non-blank lines of an embedded lexicon file that do not start with <c>#</c>, trimmed.</summary>
    /// <param name="fileName">File name, e.g. <c>genre-words.v1.txt</c>.</param>
    /// <returns>The entries in file order.</returns>
    /// <exception cref="InvalidOperationException">The resource is not embedded.</exception>
    public static IReadOnlyList<string> Lines(string fileName)
    {
        using var stream = typeof(EmbeddedLexicon).Assembly.GetManifestResourceStream(Prefix + fileName)
            ?? throw new InvalidOperationException($"The lexicon resource {fileName} is missing.");
        using var reader = new StreamReader(stream);
        return [.. reader.ReadToEnd().Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'))];
    }
}
