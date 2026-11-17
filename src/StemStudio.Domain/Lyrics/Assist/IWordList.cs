namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>Candidate words for rhymes (OpenTaal for Dutch, SCOWL/CMUdict for English), loaded by the API.</summary>
public interface IWordList
{
    /// <summary>The words of a language, lower case, without duplicates.</summary>
    /// <param name="language">Language.</param>
    /// <returns>The words.</returns>
    IReadOnlyCollection<string> Words(AssistLanguage language);
}
