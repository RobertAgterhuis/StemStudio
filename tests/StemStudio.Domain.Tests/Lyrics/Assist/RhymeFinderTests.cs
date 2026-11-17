using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T045: rhyme finder over a fixture word list and the language guess (FR-001, FR-016).</summary>
public sealed class RhymeFinderTests
{
    private static readonly FixtureWords Words = new(["nacht", "lacht", "kracht", "dacht", "maar", "maan", "zon", "zee", "licht", "gezicht", "zacht", "achterkant"], ["night", "light", "bright"]);

    [Fact]
    public void Dutch_PerfectBeforeNearAndNeverItself()
    {
        var finder = new RhymeFinder(Words, new LyricPhonology(new NoLexicon()));

        var rhymes = finder.Find("Nacht", AssistLanguage.Dutch);

        rhymes.Where(r => r.Kind == RhymeKind.Perfect).Select(r => r.Word).ShouldBe(["dacht", "kracht", "lacht", "zacht"]);
        rhymes.ShouldContain(r => r.Word == "achterkant" && r.Kind == RhymeKind.Near);
        rhymes.ShouldNotContain(r => r.Word == "nacht");
        rhymes.SkipWhile(r => r.Kind == RhymeKind.Perfect).ShouldAllBe(r => r.Kind == RhymeKind.Near);
        finder.Find("nacht", AssistLanguage.Dutch, 2).Count.ShouldBe(2);
        finder.Find("'k", AssistLanguage.Dutch).ShouldBeEmpty();
    }

    [Fact]
    public void English_UsesTheSpellingFallbackWithoutLexicon()
    {
        var finder = new RhymeFinder(Words, new LyricPhonology(new NoLexicon()));

        finder.Find("sight", AssistLanguage.English, 60).Select(r => r.Word).ShouldBe(["bright", "light", "night"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("de wind is zacht en het licht is mooi", "Dutch")]
    [InlineData("the night is bright and the light is soft", "English")]
    [InlineData("qqq zzz xxx", null)]
    [InlineData("[verse]\n\n42", null)]
    public void Guess_ByDictionaryHits(string text, string? expected)
    {
        var (language, confidence) = LanguageGuesser.Guess(text, new SetSpelling());

        language?.ToString().ShouldBe(expected);
        if (expected is null)
        {
            language.ShouldBeNull();
        }
        else
        {
            confidence.ShouldBeGreaterThanOrEqualTo(LanguageGuesser.MinHitRate);
        }
    }

    private sealed class FixtureWords(string[] nl, string[] en) : IWordList
    {
        public IReadOnlyCollection<string> Words(AssistLanguage language) => language == AssistLanguage.Dutch ? nl : en;
    }

    private sealed class NoLexicon : IPronunciationLexicon
    {
        public IReadOnlyList<IReadOnlyList<string>> Pronunciations(string word) => [];
    }

    private sealed class SetSpelling : ISpellChecker
    {
        private static readonly string[] Nl = ["de", "wind", "is", "zacht", "en", "het", "licht", "mooi"];
        private static readonly string[] En = ["the", "night", "is", "bright", "and", "light", "soft"];

        public bool IsKnown(string word, AssistLanguage language) => (language == AssistLanguage.Dutch ? Nl : En).Contains(word);

        public IReadOnlyList<string> Suggest(string word, AssistLanguage language, int max) => [];
    }
}
