namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>English pronunciations (CMU Pronouncing Dictionary adapter in the API): syllables and rhymes (research R-04).</summary>
public interface IPronunciationLexicon
{
    /// <summary>The pronunciations of a word, each as ARPAbet phones with stress digits on vowels (e.g. <c>HH AH0 L OW1</c>).</summary>
    /// <param name="word">A word (case-insensitive).</param>
    /// <returns>The pronunciations, the dictionary's first one first; empty when unknown.</returns>
    IReadOnlyList<IReadOnlyList<string>> Pronunciations(string word);
}
