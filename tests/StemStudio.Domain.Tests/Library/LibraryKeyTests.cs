using StemStudio.Domain.Library;

namespace StemStudio.Domain.Tests.Library;

/// <summary>022 T010: name normalisation (research R-02, FR-004, FR-005, SC-002: at least 40 cases).</summary>
public sealed class LibraryKeyTests
{
    /// <summary>Pairs of artist names that must land in one folder.</summary>
    public static TheoryData<string, string> SameArtist => new()
    {
        { "Metallica", "metallica" },
        { "Metallica", "METALLICA" },
        { "Metallica", "  Metallica  " },
        { "Iron Maiden", "Iron  Maiden" },
        { "Iron Maiden", "Iron Maiden" },
        { "Iron Maiden", "Iron\tMaiden" },
        { "Motörhead", "Motorhead" },
        { "Björk", "Bjork" },
        { "Sigur Rós", "Sigur Ros" },
        { "Beyoncé", "Beyonce" },
        { "Beyoncé", "Beyoncé" },
        { "Die Ärzte", "Die Arzte" },
        { "Straße", "Strasse" },
        { "Æther", "aether" },
        { "Mø", "Mo" },
        { "Łódź", "Lodz" },
        { "Đorđe Balašević", "Dorde Balasevic" },
        { "Þursaflokkurinn", "Thursaflokkurinn" },
        { "Œuvre", "oeuvre" },
        { "Guns N’ Roses", "Guns N' Roses" },
        { "Guns N‘ Roses", "Guns N' Roses" },
        { "Guns N´ Roses", "Guns N' Roses" },
        { "“Weird Al” Yankovic", "\"Weird Al\" Yankovic" },
        { "Jay–Z", "Jay-Z" },
        { "Jay—Z", "Jay-Z" },
        { "Ｍｅｔａｌｌｉｃａ", "Metallica" },
        { "Simon ＆ Garfunkel", "Simon & Garfunkel" },
        { "The Beatles", "Beatles" },
        { "the beatles", "BEATLES" },
        { "Daft Punk feat. Pharrell", "Daft Punk" },
        { "Daft Punk ft. Pharrell Williams", "Daft Punk" },
        { "Daft Punk featuring Pharrell", "Daft Punk" },
        { "Daft Punk (feat. Pharrell)", "Daft Punk" },
        { "Daft Punk [ft Pharrell]", "Daft Punk" },
        { "ДДТ", "ддт" },
        { "Σωκράτης", "σωκρατης" },
        { "ＡＣ／ＤＣ", "AC/DC" },
    };

    [Theory]
    [MemberData(nameof(SameArtist))]
    public void For_Artist_SameKeyForVariants(string a, string b) =>
        LibraryKey.For(FolderLevel.Artist, a).ShouldBe(LibraryKey.For(FolderLevel.Artist, b));

    /// <summary>Names that must stay apart (or keep their exact key).</summary>
    public static TheoryData<FolderLevel, string, string> ExactKeys => new()
    {
        { FolderLevel.Artist, "Thee Oh Sees", "thee oh sees" },
        { FolderLevel.Artist, "The The", "the" },
        { FolderLevel.Artist, "The", "the" },
        { FolderLevel.Artist, "De Dijk", "de dijk" },
        { FolderLevel.Artist, "Les Négresses Vertes", "les negresses vertes" },
        { FolderLevel.Artist, "Simon & Garfunkel", "simon & garfunkel" },
        { FolderLevel.Artist, "Crosby, Stills, Nash & Young", "crosby, stills, nash & young" },
        { FolderLevel.Artist, "Rage Against the Machine", "rage against the machine" },
        { FolderLevel.Artist, "Featherweight", "featherweight" },
        { FolderLevel.Album, "The Wall", "wall" },
        { FolderLevel.Album, "Live (feat. Friends)", "live (feat. friends)" },
        { FolderLevel.Song, "The Unforgiven", "the unforgiven" },
        { FolderLevel.Song, "Song feat. Someone", "song feat. someone" },
        { FolderLevel.Artist, "東京事変", "東京事変" },
        { FolderLevel.Artist, "ﬁnal ﬂight", "final flight" },
    };

    [Theory]
    [MemberData(nameof(ExactKeys))]
    public void For_GivesTheExpectedKey(FolderLevel level, string name, string key) => LibraryKey.For(level, name).ShouldBe(key);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("🎸🎸")]
    [InlineData("-- . --")]
    public void IsMeaningful_FalseForEmptyPunctuationAndEmoji(string name) => LibraryKey.IsMeaningful(name).ShouldBeFalse();

    [Theory]
    [InlineData("ABBA")]
    [InlineData("2Pac")]
    [InlineData("ДДТ")]
    public void IsMeaningful_TrueWithALetterOrDigit(string name) => LibraryKey.IsMeaningful(name).ShouldBeTrue();

    [Theory]
    [InlineData(FolderLevel.Artist, "Unknown artist")]
    [InlineData(FolderLevel.Album, "Unknown album")]
    [InlineData(FolderLevel.Song, "Untitled song")]
    public void For_EmptyResult_UsesTheFallbackKey(FolderLevel level, string fallback) =>
        LibraryKey.For(level, "🎸").ShouldBe(LibraryKey.For(level, fallback));

    [Fact]
    public void For_FeaturingOnly_UsesTheFallbackKey() =>
        LibraryKey.For(FolderLevel.Artist, "(feat. X)").ShouldBe(LibraryKey.For(FolderLevel.Artist, LibraryKey.UnknownArtist));

    [Fact]
    public void For_LongNames_AreCutTo400() => LibraryKey.For(FolderLevel.Song, new string('ß', 300)).Length.ShouldBe(LibraryKey.MaxKeyLength);

    [Fact]
    public void For_RemovesControlCharacters() => LibraryKey.For(FolderLevel.Song, "a\u001fb\u0000c").ShouldBe("abc");

    [Fact]
    public void For_NullName_Throws() => Should.Throw<ArgumentNullException>(() => LibraryKey.For(FolderLevel.Song, null!));

    [Fact]
    public void IsVariousArtists_ByMbidOrName()
    {
        LibraryKey.IsVariousArtists("Various Artists", null).ShouldBeTrue();
        LibraryKey.IsVariousArtists("Someone", LibraryKey.VariousArtistsMbid).ShouldBeTrue();
        LibraryKey.IsVariousArtists("Someone", Guid.NewGuid()).ShouldBeFalse();
        LibraryKey.IsVariousArtists(null, null).ShouldBeFalse();
    }

    [Fact]
    public void SortKey_IgnoresTheForArtists()
    {
        LibraryKey.SortKey(FolderLevel.Artist, "The Beatles").ShouldBe("beatles");
        LibraryKey.SortKey(FolderLevel.Song, "The Unforgiven").ShouldBe("the unforgiven");
    }
}
