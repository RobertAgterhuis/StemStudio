using StemStudio.Domain.Styles;

namespace StemStudio.Domain.Tests.Styles;

public sealed class StyleTextGuardTests
{
    private static readonly StyleTextGuard Guard = StyleTextGuard.Create(["Mägi Lindström", "The Velvet Folks"]);

    [Theory]
    [InlineData("in the style of somebody")]
    [InlineData("Sounds like a famous band")]
    [InlineData("sound like them")]
    [InlineData("the voice of an icon")]
    [InlineData("imitate the singer")]
    [InlineData("an impersonation")]
    [InlineData("cover by a band")]
    [InlineData("in de stijl van iemand")]
    [InlineData("klinkt als een band")]
    [InlineData("de stem van iemand")]
    [InlineData("like Taylor Swift")]
    [InlineData("BEATLES vibes")]
    [InlineData("a bit of Beyoncé")]
    [InlineData("magi lindstrom")]
    [InlineData("sunny the velvet folks groove")]
    public void IsImitation_Hits(string text) => Guard.IsImitation(text).ShouldBeTrue();

    [Theory]
    [InlineData("rock")]
    [InlineData("soul")]
    [InlineData("country")]
    [InlineData("Classic soul with horns")]
    [InlineData("punk rock, raw")]
    [InlineData("heavy metal")]
    [InlineData("a queen of the night")]
    [InlineData("journey through the stars")]
    [InlineData("beatless ambient")]
    [InlineData("magician")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("I like jazz")]
    public void IsImitation_NoFalsePositives(string? text) => Guard.IsImitation(text).ShouldBeFalse();

    [Fact]
    public void Check_NamesEveryOffendingField()
    {
        var fields = StyleTestData.RainyCafe() with
        {
            Name = "Beatles mood",
            SubGenre = "sounds like",
            Instrumentation = ["piano", "voice of x"],
            Moods = ["Madonna"],
            Description = "klinkt als",
        };

        var errors = Guard.Check(fields);

        errors.Select(e => e.Field).ShouldBe(["name", "subGenre", "instrumentation", "moods", "description"]);
        errors.ShouldAllBe(e => e.Message == StyleTextGuard.Message);
    }

    [Fact]
    public void Check_CleanStyleHasNoErrors() => Guard.Check(StyleTestData.RainyCafe()).ShouldBeEmpty();

    [Fact]
    public void Create_IgnoresBlankShortAndGenreWordLibraryArtists()
    {
        var guard = StyleTextGuard.Create(["", "  ", "Soul", "X", null]);

        guard.IsImitation("soul jazz").ShouldBeFalse();
        guard.IsImitation("x factor").ShouldBeFalse();
    }

    [Fact]
    public void Resources_AreLoaded()
    {
        StyleTextGuard.DenyList.Count.ShouldBeGreaterThanOrEqualTo(200);
        StyleTextGuard.Phrases.ShouldContain("in the style of");
        StyleTextGuard.Phrases.ShouldContain("klinkt als");
    }

    [Theory]
    [InlineData("Ça Va", "ca va")]
    [InlineData("  A--B  c ", "a b c")]
    [InlineData(null, "")]
    public void Normalize_FoldsCaseDiacriticsAndPunctuation(string? text, string expected) =>
        StyleTextGuard.Normalize(text).ShouldBe(expected);
}
