using StemStudio.Domain.LibraryCare;

namespace StemStudio.Domain.Tests.LibraryCare;

/// <summary>013 T013: the layout allow-list of data-model 3.3 and the archive entry-name rules (FR-034, SC-004).</summary>
public sealed class LibraryLayoutTests
{
    private const string Id = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";
    private static readonly Guid TrackId = Guid.Parse(Id);

    [Theory]
    [InlineData($"tracks/{Id}/stems/vocals.flac", LayoutKind.Stem)]
    [InlineData($"tracks/{Id}/stems/other.flac", LayoutKind.Stem)]
    [InlineData($"tracks/{Id}/artwork/auto/cover.jpg", LayoutKind.AutoArtwork)]
    [InlineData($"tracks/{Id}/artwork/user/thumb.jpg", LayoutKind.UserArtwork)]
    [InlineData($"tracks/{Id}/detail/drums/kick.flac", LayoutKind.DetailStem)]
    [InlineData($"tracks/{Id}/detail/guitar/acoustic-guitar.flac", LayoutKind.DetailStem)]
    [InlineData($"assets/{Id}/audio.flac", LayoutKind.AssetFile)]
    [InlineData($"generations/{Id}/take-1.flac", LayoutKind.GenerationFile)]
    [InlineData($"generations/{Id}/a/b/c.json", LayoutKind.GenerationFile)]
    [InlineData($"voices/{Id}/sample.flac", LayoutKind.VoiceFile)]
    public void Match_FindsTheKindAndTheEntity(string path, LayoutKind kind)
    {
        var match = LibraryLayout.Match(path).ShouldNotBeNull();

        (match.Kind, match.EntityId).ShouldBe((kind, TrackId));
        LibraryLayout.IsAllowed(path).ShouldBeTrue();
    }

    [Fact]
    public void Match_UploadHasNoEntityButKeepsTheFileName()
    {
        var name = new string('a', 64) + ".mp3";

        var match = LibraryLayout.Match("uploads/" + name).ShouldNotBeNull();

        (match.Kind, match.EntityId, match.FileName).ShouldBe((LayoutKind.Upload, null, name));
    }

    [Fact]
    public void Match_DetailStemKnowsItsParentAndName()
    {
        var match = LibraryLayout.Match($"tracks/{Id}/detail/vocals/lead.flac").ShouldNotBeNull();

        (match.Parent, match.FileName).ShouldBe(("vocals", "lead.flac"));
    }

    [Theory]
    [InlineData("tracks/../stemstudio.db")]
    [InlineData($"assets/{Id}/..")]
    [InlineData($"assets/{Id}/.")]
    [InlineData($"assets/{Id}/a..b")]
    [InlineData($"assets/{Id}/.hidden")]
    [InlineData($"generations/{Id}/a/b/c/d")]
    [InlineData("tracks/0B7E4F1C-6A2D-4C8E-9F10-2A3B4C5D6E7F/stems/vocals.flac")]
    [InlineData($"tracks/{Id}/stems/vocals.wav")]
    [InlineData($"tracks/{Id}/stems/kick.flac")]
    [InlineData($"tracks/{Id}/detail/bass/kick.flac")]
    [InlineData($"tracks/{Id}/run.sh")]
    [InlineData("library-care/jobs/x.json")]
    [InlineData("stemstudio.db")]
    [InlineData($"/assets/{Id}/audio.flac")]
    [InlineData("uploads/abc.mp3")]
    [InlineData("")]
    public void ForeignOrUnsafePaths_AreRefused(string path)
    {
        LibraryLayout.IsAllowed(path).ShouldBeFalse();
        LibraryLayout.Match(path).ShouldBeNull();
    }

    [Fact]
    public void LongNames_AreRefusedAtTheLimit()
    {
        LibraryLayout.IsAllowed($"assets/{Id}/" + new string('a', 100)).ShouldBeTrue();
        LibraryLayout.IsAllowed($"assets/{Id}/" + new string('a', 101)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("db/stemstudio.db", ArchiveEntryKind.Database)]
    [InlineData("manifest.json", ArchiveEntryKind.Manifest)]
    [InlineData("README.txt", ArchiveEntryKind.Readme)]
    [InlineData($"files/tracks/{Id}/stems/drums.flac", ArchiveEntryKind.LibraryFile)]
    [InlineData("files/tracks/", ArchiveEntryKind.Directory)]
    public void ArchiveEntries_AreClassified(string name, ArchiveEntryKind kind) =>
        LibraryLayout.ClassifyEntry(name).ShouldBe(kind);

    [Theory]
    [InlineData("files/../../etc/passwd")]
    [InlineData("/data/stemstudio.db")]
    [InlineData("C:\\x")]
    [InlineData("C:/x")]
    [InlineData("files\\tracks\\x")]
    [InlineData("files/tracks/\u0001.flac")]
    [InlineData("files/tracks/a\u007f")]
    [InlineData("files//tracks")]
    [InlineData("./manifest.json")]
    [InlineData("db/../manifest.json")]
    [InlineData("")]
    public void UnsafeEntryNames_AreUnsafe(string name)
    {
        LibraryLayout.IsSafeEntryName(name).ShouldBeFalse();
        LibraryLayout.ClassifyEntry(name).ShouldBe(ArchiveEntryKind.Unsafe);
    }

    [Fact]
    public void EntryNamesLongerThan260_AreUnsafe()
    {
        LibraryLayout.IsSafeEntryName("files/" + new string('a', 254)).ShouldBeTrue();
        LibraryLayout.IsSafeEntryName("files/" + new string('a', 255)).ShouldBeFalse();
    }

    [Theory]
    [InlineData($"files/tracks/{Id}/run.sh")]
    [InlineData("db/other.db")]
    [InlineData("notes.txt")]
    [InlineData("files/library-care/jobs/x.json")]
    public void SafeButForeignEntries_AreNotInTheLayout(string name) =>
        LibraryLayout.ClassifyEntry(name).ShouldBe(ArchiveEntryKind.Foreign);

    [Fact]
    public void EntryAndDataPaths_MapBothWays()
    {
        var data = $"tracks/{Id}/stems/bass.flac";

        LibraryLayout.ToEntryName(data).ShouldBe("files/" + data);
        LibraryLayout.ToDataPath("files/" + data).ShouldBe(data);
        Should.Throw<ArgumentException>(() => LibraryLayout.ToDataPath("manifest.json"));
    }
}
