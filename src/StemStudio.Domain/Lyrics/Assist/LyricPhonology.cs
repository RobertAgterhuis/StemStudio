namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>Syllables and rhymes per language (Dutch by spelling, English over the pronunciation lexicon).</summary>
/// <param name="lexicon">English pronunciation lexicon.</param>
public sealed class LyricPhonology(IPronunciationLexicon lexicon)
{
    private readonly EnglishPhonology _english = new(lexicon);

    /// <summary>Syllables of a word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="language">Language.</param>
    /// <returns>The count.</returns>
    public int WordSyllables(string word, AssistLanguage language) =>
        language == AssistLanguage.Dutch ? DutchPhonology.Syllables(word) : _english.Syllables(word);

    /// <summary>Syllables of a line; markers and blank lines count 0.</summary>
    /// <param name="line">The line.</param>
    /// <param name="language">Language.</param>
    /// <returns>The count.</returns>
    public int LineSyllables(string line, AssistLanguage language) => SheetText.IsLyric(line)
        ? LyricTokens.Of(line).Where(token => !token.Text.All(char.IsDigit)).Sum(token => WordSyllables(token.Text, language))
        : 0;

    /// <summary>The rhyme key of a word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="language">Language.</param>
    /// <returns>Vowel and tail.</returns>
    public (string Vowel, string Tail) RhymeKey(string word, AssistLanguage language) =>
        language == AssistLanguage.Dutch ? DutchPhonology.RhymeKey(word) : _english.RhymeKey(word);

    /// <summary>How two words rhyme; a word never rhymes with itself.</summary>
    /// <param name="word">A word.</param>
    /// <param name="other">The other word.</param>
    /// <param name="language">Language.</param>
    /// <returns>The kind or null.</returns>
    public RhymeKind? Rhyme(string word, string other, AssistLanguage language) =>
        string.Equals(LyricTokens.Key(word), LyricTokens.Key(other), StringComparison.Ordinal) ? null : RhymeMatch.Kind(RhymeKey(word, language), RhymeKey(other, language));
}
