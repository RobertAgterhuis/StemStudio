using StemStudio.Domain.Library;

namespace StemStudio.Domain.Tests.Library;

/// <summary>022 T012: derived folder ids (research R-03) and library info rules (FR-031).</summary>
public sealed class FolderIdTests
{
    [Fact]
    public void Ids_AreDeterministicWithLevelPrefixes()
    {
        FolderId.Artist("metallica").ShouldBe(FolderId.Artist("metallica"));
        FolderId.Artist("metallica").ShouldStartWith("ar");
        FolderId.Album("metallica", "wall").ShouldStartWith("al");
        FolderId.Song("metallica", "wall", "one").ShouldStartWith("so");
    }

    [Fact]
    public void Ids_MatchThePattern()
    {
        foreach (var id in new[] { FolderId.Artist("a"), FolderId.Album("a", "b"), FolderId.Song("a", "b", "c") })
        {
            id.ShouldMatch("^(ar|al|so)[0-9a-f]{16}$");
        }
    }

    [Fact]
    public void Ids_SeparateTheKeys()
    {
        FolderId.Album("ab", "c").ShouldNotBe(FolderId.Album("a", "bc"));
        FolderId.Song("a", "b", "c").ShouldNotBe(FolderId.Album("a", "b"));
    }

    [Fact]
    public void Ids_MatchTheContractExampleRule()
    {
        // backend-api 1.22.0 examples: Rick Astley > Unknown album > Never Gonna Give You Up (merge_full.py placement_lines).
        var artist = LibraryKey.For(FolderLevel.Artist, "Rick Astley");
        var album = LibraryKey.For(FolderLevel.Album, "Unknown album");
        var song = LibraryKey.For(FolderLevel.Song, "Never Gonna Give You Up");

        FolderId.Artist(artist).ShouldBe("arb385da54bfe8c6a9");
        FolderId.Album(artist, album).ShouldBe("alb7d8a7e8ed84d5af");
        FolderId.Song(artist, album, song).ShouldBe("soc02cef10277ea444");
    }

    [Theory]
    [InlineData("ar0123456789abcdef", FolderLevel.Artist)]
    [InlineData("al0123456789abcdef", FolderLevel.Album)]
    [InlineData("so0123456789abcdef", FolderLevel.Song)]
    public void TryParse_ValidIds(string id, FolderLevel expected)
    {
        FolderId.TryParse(id, out var level).ShouldBeTrue();
        level.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx0123456789abcdef")]
    [InlineData("ar0123456789ABCDEF")]
    [InlineData("ar0123456789abcde")]
    [InlineData("ar0123456789abcdef0")]
    [InlineData("../etc/passwd")]
    public void TryParse_InvalidIds(string? id) => FolderId.TryParse(id, out _).ShouldBeFalse();

    [Fact]
    public void Of_NullKey_Throws() => Should.Throw<ArgumentNullException>(() => FolderId.Artist(null!));
}

/// <summary>022 T012: library info rules (FR-031).</summary>
public sealed class LibraryInfoRulesTests
{
    [Fact]
    public void Validate_TrimsAndKeepsNullsAutomatic()
    {
        var info = LibraryInfoRules.Validate(new LibraryInfo { Artist = "  Metallica ", Album = null, Song = "One" });

        info.ShouldBe(new LibraryInfo { Artist = "Metallica", Song = "One" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\u0007b")]
    [InlineData("line\nbreak")]
    public void Validate_RejectsBlankAndControlCharacters(string name)
    {
        var error = Should.Throw<DomainValidationException>(() => LibraryInfoRules.Validate(new LibraryInfo { Album = name }));

        error.Errors.Single().Field.ShouldBe("album");
    }

    [Fact]
    public void Validate_LengthLimit()
    {
        LibraryInfoRules.Validate(new LibraryInfo { Song = new string('x', 200) }).Song!.Length.ShouldBe(200);
        Should.Throw<DomainValidationException>(() => LibraryInfoRules.Validate(new LibraryInfo { Song = new string('x', 201) }));
    }

    [Fact]
    public void Validate_ReportsEveryField()
    {
        var error = Should.Throw<DomainValidationException>(() => LibraryInfoRules.Validate(new LibraryInfo { Artist = "", Album = "", Song = "" }));

        error.Errors.Select(e => e.Field).ShouldBe(["artist", "album", "song"]);
    }

    [Fact]
    public void For_GivesEachLevel()
    {
        var info = new LibraryInfo { Artist = "a", Album = "b", Song = "c" };

        (info.For(FolderLevel.Artist), info.For(FolderLevel.Album), info.For(FolderLevel.Song)).ShouldBe(("a", "b", "c"));
    }

    [Fact]
    public void Cut_LongNamesGetAnEllipsis()
    {
        var cut = LibraryFilingRules.Cut(new string('x', 500));

        cut.Length.ShouldBe(200);
        cut.ShouldEndWith("…");
        LibraryFilingRules.Cut(" a \u0001 b ").ShouldBe("a b");
    }

    [Fact]
    public void Cut_DoesNotSplitASurrogatePair()
    {
        var cut = LibraryFilingRules.Cut(new string('x', 196) + "😀😀😀");

        char.IsHighSurrogate(cut[^2]).ShouldBeFalse();
        cut.ShouldEndWith("…");
    }
}
