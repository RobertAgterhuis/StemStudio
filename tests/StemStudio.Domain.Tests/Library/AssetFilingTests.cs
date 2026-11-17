using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tests.Assets;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Library;

/// <summary>022 T014/T015: <c>Asset.File</c>, <c>Asset.SetLibraryInfo</c>, <c>Track.SetLibraryInfo</c> and the filing rules (data-model 2.7-2.9).</summary>
public sealed class AssetFilingTests
{
    private static readonly DateTime T1 = AssetTests.T0.AddMinutes(5);

    private static Track NewTrack() => Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", AssetTests.T0);

    private static Placement PlacementOf(string song) =>
        PlacementResolver.Resolve(new PlacementInput { Provenance = AssetTests.Provenance(), ItemInfo = new LibraryInfo { Song = song } });

    [Fact]
    public void NewItem_IsNotFiled()
    {
        var asset = AssetTests.NewAsset();

        (asset.Placement, asset.FiledVersion, asset.FiledAt, asset.IsFiled).ShouldBe((null, 0, null, false));
        asset.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
        LibraryFilingRules.NeedsFiling(asset).ShouldBeTrue();
    }

    [Fact]
    public void File_SetsPlacementVersionAndTime()
    {
        var asset = AssetTests.NewAsset();

        asset.File(PlacementOf("One"), T1).ShouldBeTrue();

        (asset.FiledVersion, asset.FiledAt, asset.IsFiled, asset.UpdatedAt).ShouldBe((LibraryFilingRules.Version, T1, true, T1));
        LibraryFilingRules.NeedsFiling(asset).ShouldBeFalse();
    }

    [Fact]
    public void File_SamePlacement_KeepsUpdatedAt()
    {
        var asset = AssetTests.NewAsset();
        asset.File(PlacementOf("One"), AssetTests.T0);

        asset.File(PlacementOf("One"), T1).ShouldBeFalse();

        (asset.UpdatedAt, asset.FiledAt).ShouldBe((AssetTests.T0, T1));
    }

    [Fact]
    public void File_Null_Throws() => Should.Throw<ArgumentNullException>(() => AssetTests.NewAsset().File(null!, T1));

    [Fact]
    public void SetLibraryInfo_ValidatesAndTouches()
    {
        var asset = AssetTests.NewAsset();

        asset.SetLibraryInfo(new LibraryInfo { Artist = " Me " }, T1);

        (asset.LibraryInfo.Artist, asset.UpdatedAt).ShouldBe(("Me", T1));
        Should.Throw<DomainValidationException>(() => asset.SetLibraryInfo(new LibraryInfo { Artist = "" }, T1));
    }

    [Fact]
    public void Track_SetLibraryInfo_ValidatesAndTouches()
    {
        var track = NewTrack();

        track.SetLibraryInfo(new LibraryInfo { Album = " Live " }, T1);

        (track.LibraryInfo.Album, track.UpdatedAt).ShouldBe(("Live", T1));
        Should.Throw<DomainValidationException>(() => track.SetLibraryInfo(new LibraryInfo { Song = new string('x', 201) }, T1));
    }

    [Fact]
    public void TrackFacts_From_CopiesTheNames()
    {
        var track = NewTrack();

        var facts = TrackFacts.From(track);

        (facts.Title, facts.Artist, facts.Metadata, facts.ArtistMbid).ShouldBe((track.Title, track.Artist, track.Metadata, track.ArtistId));
    }

    [Fact]
    public void Rules_EveryCurrentOriginIsFileable()
    {
        foreach (var origin in Enum.GetValues<AssetOrigin>())
        {
            LibraryFilingRules.IsFileable(AssetTests.Provenance() with { Origin = origin }).ShouldBeTrue();
        }

        LibraryFilingRules.IsFileable(AssetTests.Provenance() with { Origin = (AssetOrigin)99 }).ShouldBeFalse();
        Should.Throw<ArgumentNullException>(() => LibraryFilingRules.IsFileable(null!));
        Should.Throw<ArgumentNullException>(() => LibraryFilingRules.NeedsFiling(null!));
        Should.Throw<ArgumentNullException>(() => LibraryFilingRules.Cut(null!));
    }
}
