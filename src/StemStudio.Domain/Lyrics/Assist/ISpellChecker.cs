namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>Spelling lookup (Hunspell adapter in the API over the vendored OpenTaal and SCOWL dictionaries, research R-02).</summary>
public interface ISpellChecker
{
    /// <summary>True when the word is correct; English accepts US or GB spelling.</summary>
    /// <param name="word">A word as written (a capitalised sentence start is accepted when the lower-case form is).</param>
    /// <param name="language">Language.</param>
    /// <returns>True when known.</returns>
    bool IsKnown(string word, AssistLanguage language);

    /// <summary>Corrections, best first.</summary>
    /// <param name="word">A word the dictionary does not know.</param>
    /// <param name="language">Language.</param>
    /// <param name="max">Maximum suggestions.</param>
    /// <returns>Up to <paramref name="max"/> corrections.</returns>
    IReadOnlyList<string> Suggest(string word, AssistLanguage language, int max);
}
