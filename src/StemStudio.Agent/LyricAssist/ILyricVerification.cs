using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>
/// Deterministic checks the assistant needs (research R-04, R-07). StemStudio.Agent references no Domain project (dependency rule
/// of feature 011), so the API implements this port with the Domain services (<c>SyllableCounter*</c>, <c>RhymeKey*</c>,
/// <c>VerbatimGuard</c> over <c>ILyricsCorpus</c>).
/// </summary>
public interface ILyricVerification
{
    /// <summary>Syllables of one line (markers and blank lines count 0).</summary>
    /// <param name="line">The line text.</param>
    /// <param name="language">Language.</param>
    /// <returns>The count.</returns>
    int CountSyllables(string line, AssistLanguageDto language);

    /// <summary>How two words rhyme.</summary>
    /// <param name="word">A word.</param>
    /// <param name="other">The other word.</param>
    /// <param name="language">Language.</param>
    /// <returns><c>perfect</c> or <c>near</c>; null when they do not rhyme.</returns>
    RhymeKindDto? Rhyme(string word, string other, AssistLanguageDto language);

    /// <summary>True when generated text contains a 7-word run or a full 5+-word line of library lyrics (it must be withheld).</summary>
    /// <param name="generatedText">Proposal text.</param>
    /// <returns>True when it must be withheld.</returns>
    bool IsVerbatim(string generatedText);
}
