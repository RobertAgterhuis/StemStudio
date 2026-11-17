using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T012: intentional marks (FR-011): word case-sensitive and whole-word, phrase, line, not in the text.</summary>
public sealed class IntentionalMarkMatcherTests
{
    private static IntentionalMark Mark(IntentionalScope scope, string text) => IntentionalMark.Create(Guid.NewGuid(), scope, text, null, AssistFixtures.Now);

    [Fact]
    public void Word_IsCaseSensitiveAndWholeWord()
    {
        var mark = Mark(IntentionalScope.Word, "nie");

        IntentionalMarkMatcher.Covers(mark, "ik wil nie weg", new Anchor(1, 7, 3, "nie")).ShouldBeTrue();
        IntentionalMarkMatcher.Covers(mark, "Nie weg", new Anchor(1, 0, 3, "Nie")).ShouldBeFalse();
        IntentionalMarkMatcher.Covers(mark, "niet weg", new Anchor(1, 0, 3, "nie")).ShouldBeFalse();
        IntentionalMarkMatcher.Covers(mark, "ik wil nie weg", new Anchor(1, 3, 7, "wil nie")).ShouldBeFalse();
    }

    [Fact]
    public void Phrase_CoversAnchorsInside()
    {
        var mark = Mark(IntentionalScope.Phrase, "  Naar huis   ik ga ");

        mark.Text.ShouldBe("Naar huis ik ga");
        IntentionalMarkMatcher.Covers(mark, "Naar huis ik ga, de straat", new Anchor(1, 10, 5, "ik ga")).ShouldBeTrue();
        IntentionalMarkMatcher.Covers(mark, "Naar huis ik ga, de straat", new Anchor(1, 10, 9, "ik ga, de")).ShouldBeFalse();
    }

    [Fact]
    public void Line_CoversTheWholeLine()
    {
        var mark = Mark(IntentionalScope.Line, "Gij zijt de ster");

        IntentionalMarkMatcher.Covers(mark, "Gij zijt de ster", new Anchor(1, 4, 4, "zijt")).ShouldBeTrue();
        IntentionalMarkMatcher.Covers(mark, "Gij zijt de ster!", new Anchor(1, 4, 4, "zijt")).ShouldBeFalse();
        IntentionalMarkMatcher.Covers(mark, "Gij zijt de ster", new Anchor(0, 0, 4, "Song")).ShouldBeFalse();
    }

    [Fact]
    public void Occurs_ReportsNotInTheText()
    {
        var mark = Mark(IntentionalScope.Word, "'k");

        IntentionalMarkMatcher.Occurs(mark, "regel\n'k Heb je lief").ShouldBeTrue();
        IntentionalMarkMatcher.Occurs(mark, "ik heb je lief").ShouldBeFalse();
    }

    [Theory]
    [InlineData(IntentionalScope.Word, "two words")]
    [InlineData(IntentionalScope.Word, "")]
    [InlineData(IntentionalScope.Phrase, " ")]
    public void InvalidText_IsRefused(IntentionalScope scope, string text) =>
        Should.Throw<DomainValidationException>(() => Mark(scope, text)).Errors.Single().Field.ShouldBe("text");

    [Fact]
    public void LimitsPerScope()
    {
        IntentionalMarkMatcher.IsValidText(IntentionalScope.Word, new string('a', 41)).ShouldBeFalse();
        IntentionalMarkMatcher.IsValidText(IntentionalScope.Phrase, new string('a', 200)).ShouldBeTrue();
        IntentionalMarkMatcher.IsValidText(IntentionalScope.Phrase, new string('a', 201)).ShouldBeFalse();
        IntentionalMarkMatcher.IsValidText(IntentionalScope.Line, new string('a', 401)).ShouldBeFalse();
        IntentionalMarkMatcher.TextRule(IntentionalScope.Line).ShouldContain("400");
        Should.Throw<DomainValidationException>(() => IntentionalMark.Create(Guid.NewGuid(), IntentionalScope.Word, "a", new string('n', 121), AssistFixtures.Now));
        IntentionalMark.Create(Guid.NewGuid(), IntentionalScope.Word, "a", " note ", AssistFixtures.Now).Note.ShouldBe("note");
    }
}
