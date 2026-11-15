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
    [InlineData("Warm late-night jazz in the style of a famous crooner")]
    [InlineData("Big drums, sounds like a stadium band from the eighties")]
    [InlineData("A soft ballad with the voice of a well-known singer")]
    [InlineData("In The Style Of Someone")]
    [InlineData("sounds like radio")]
    [InlineData("Rustige piano in de stijl van een bekende zanger")]
    [InlineData("Gitaarpop die klinkt als de hitlijst")]
    [InlineData("Funky bass, SOUNDS   LIKE a famous band")]
    [InlineData("Dreamy pop in the stýle of an idol")]
    [InlineData("Upbeat pop in the style of a chart singer")]
    [InlineData("sounds like a famous tenor")]
    public void IsImitation_Hits(string text) => Guard.IsImitation(text).ShouldBeTrue();

    [Theory]
    [InlineData("Soul ballad")]
    [InlineData("Rock steady groove")]
    [InlineData("Blues shuffle")]
    [InlineData("Country waltz")]
    [InlineData("Heavy metal riffs")]
    [InlineData("Pop punk energy")]
    [InlineData("Classical strings")]
    [InlineData("Folk duo")]
    public void IsImitation_QaGenreNamesAreAccepted(string text) => Guard.IsImitation(text).ShouldBeFalse();

    [Fact]
    public void IsImitation_QaLibraryArtistIsRefused() =>
        StyleTextGuard.Create(["StemStudio Fixtures"]).IsImitation("Bright synth pop like StemStudio Fixtures").ShouldBeTrue();

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
