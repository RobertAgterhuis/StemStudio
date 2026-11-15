using StemStudio.Contracts;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped artwork, metadata and band info samples (backend-api 1.4.0 examples).</summary>
public static class ArtworkData
{
    public const string Version = "3f2a9c01b7de";

    public const string PublicBase = "http://localhost:8081";

    public static ArtworkDto Artwork(ArtworkSourceDto source = ArtworkSourceDto.Spotify, string version = Version) => new(
        source,
        version,
        ApiRoutes.TrackArtwork(TestData.TrackId, ApiRoutes.ArtworkCover, version),
        ApiRoutes.TrackArtwork(TestData.TrackId, ApiRoutes.ArtworkThumb, version));

    public static string CoverUrl(string version = Version) => PublicBase + ApiRoutes.TrackArtwork(TestData.TrackId, ApiRoutes.ArtworkCover, version);

    public static string ThumbUrl(string version = Version) => PublicBase + ApiRoutes.TrackArtwork(TestData.TrackId, ApiRoutes.ArtworkThumb, version);

    public static TrackDto WithArtwork(ArtworkSourceDto source = ArtworkSourceDto.Spotify) => TestData.Track() with { Artwork = Artwork(source) };

    public static TrackMetadataDto Metadata(MetadataStatusDto status, string? album = null, int? year = null, string? reason = null) =>
        new() { Status = status, Album = album, Year = year, Reason = reason, ArtistName = status == MetadataStatusDto.Found ? "The Fixture Band" : null };

    public static TrackDto WithMetadata(MetadataStatusDto status, string? album = null, int? year = null) =>
        TestData.Track() with { Metadata = Metadata(status, album, year) };

    public static WikipediaSummaryDto Summary() => new()
    {
        Language = "en",
        Title = "The Fixture Band",
        Extract = "The Fixture Band is a fictional group invented for the StemStudio tests.",
        PageUrl = "https://en.wikipedia.org/wiki/The_Fixture_Band",
        HistoryUrl = "https://en.wikipedia.org/w/index.php?title=The_Fixture_Band&action=history",
    };

    public static ArtistInfoDto Artist(ArtistTypeDto type = ArtistTypeDto.Group) => new()
    {
        Mbid = Guid.Parse("816012e6-d855-599b-8cb1-0d8bd765ebe1"),
        Name = "The Fixture Band",
        Type = type,
        Country = "NL",
        BeginYear = 2001,
        Genres = ["synth-pop", "test rock"],
        Members = type == ArtistTypeDto.Group ? [new("Ada Sample", 2001), new("Bo Waveform", 2001, 2015), new("Cy Decibel")] : [],
        Website = "https://example.org/the-fixture-band",
        Summary = Summary(),
    };

    public static BandInfoDto Found(ArtistInfoDto? artist = null) => new()
    {
        Status = MetadataStatusDto.Found,
        Recording = new RecordingInfoDto("Never Gonna Give You Up", "Synthetic Sessions", 2020),
        Artist = artist ?? Artist(),
        Sources =
        [
            new(DataSourceDto.MusicBrainz, "https://musicbrainz.org/recording/1", "CC0"),
            new(DataSourceDto.Wikidata, "https://www.wikidata.org/wiki/Q1", "CC0"),
            new(DataSourceDto.Wikipedia, "https://en.wikipedia.org/wiki/The_Fixture_Band", "CC BY-SA 4.0"),
            new(DataSourceDto.CoverArtArchive, "https://coverartarchive.org/release-group/1", null),
        ],
    };

    public static BandInfoDto Status(MetadataStatusDto status, string? reason = null) => new() { Status = status, Reason = reason };
}
