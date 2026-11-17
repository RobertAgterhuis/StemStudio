using StemStudio.Api.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 T025: the Hunspell adapter over tiny fixture dictionaries (US or GB accepted, sentence-start capitals) and the real ones.</summary>
public sealed class HunspellSpellCheckerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("stemstudio-hunspell-").FullName;

    public HunspellSpellCheckerTests()
    {
        Write("nl/nl", ["fiets", "zomer", "avond"]);
        Write("us/us", ["color", "night"]);
        Write("gb/gb", ["colour", "night"]);
    }

    [Fact]
    public void Fixture_EnglishAcceptsUsOrGbAndCapitalisedStarts()
    {
        var checker = new HunspellSpellChecker(_root, ["nl/nl.dic"], ["us/us.dic", "gb/gb.dic"]);

        (checker.IsKnown("color", AssistLanguage.English), checker.IsKnown("colour", AssistLanguage.English), checker.IsKnown("Night", AssistLanguage.English))
            .ShouldBe((true, true, true));
        (checker.IsKnown("colr", AssistLanguage.English), checker.IsKnown("fiets", AssistLanguage.English), checker.IsKnown("  ", AssistLanguage.Dutch))
            .ShouldBe((false, false, false));
        checker.IsKnown("Zomer", AssistLanguage.Dutch).ShouldBeTrue();
    }

    [Fact]
    public void Fixture_SuggestsDistinctCorrections()
    {
        var checker = new HunspellSpellChecker(_root, ["nl/nl.dic"], ["us/us.dic", "gb/gb.dic"]);

        checker.Suggest("nigth", AssistLanguage.English, 5).ShouldBe(["night"]);
        checker.Suggest("fiets", AssistLanguage.Dutch, 0).ShouldBeEmpty();
        checker.Suggest("", AssistLanguage.Dutch, 3).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("fietsen", AssistLanguage.Dutch, true)]
    [InlineData("Zomeravond", AssistLanguage.Dutch, true)]
    [InlineData("fluisterd", AssistLanguage.Dutch, false)]
    [InlineData("realise", AssistLanguage.English, true)]
    [InlineData("realize", AssistLanguage.English, true)]
    [InlineData("colour", AssistLanguage.English, true)]
    [InlineData("recieve", AssistLanguage.English, false)]
    public void RealDictionaries_CheckWords(string word, AssistLanguage language, bool known) =>
        new HunspellSpellChecker(HunspellSpellChecker.DefaultRoot).IsKnown(word, language).ShouldBe(known);

    [Fact]
    public void RealDictionaries_SuggestTheFixtureCorrections()
    {
        var checker = new HunspellSpellChecker(HunspellSpellChecker.DefaultRoot);

        checker.Suggest("recieve", AssistLanguage.English, 5).ShouldContain("receive");
        checker.Suggest("vergeeten", AssistLanguage.Dutch, 5).ShouldContain("vergeten");
    }

    public void Dispose() => Directory.Delete(_root, true);

    private void Write(string stem, string[] words)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_root, stem))!);
        File.WriteAllText(Path.Combine(_root, stem + ".aff"), "SET UTF-8\nTRY abcdefghijklmnopqrstuvwxyz\n");
        File.WriteAllText(Path.Combine(_root, stem + ".dic"), $"{words.Length}\n{string.Join('\n', words)}\n");
    }
}
