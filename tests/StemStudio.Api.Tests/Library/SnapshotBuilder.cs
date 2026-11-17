using StemStudio.Api.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Library;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Library;

/// <summary>Builds <see cref="LibrarySnapshot"/>s for the pure folder and search tests.</summary>
internal sealed class SnapshotBuilder
{
    public const string Revision = "0123456789abcdef";
    private const string JobId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
    private static readonly DateTime T0 = LibraryTestData.T0;

    private readonly List<Asset> _items = [];
    private readonly List<TrackArtRow> _tracks = [];
    private int _clock;

    public static TrackFacts Matched(string artist, string album, string song, int? year = null) => new(
        song,
        artist,
        new TrackMetadata { Status = MetadataStatus.Found, ArtistName = artist, Album = album, RecordingTitle = song, Year = year },
        null);

    public static TrackFacts Imported(string artist, string song, string? album = null) =>
        new(song, artist, new TrackMetadata { Status = MetadataStatus.NotFound, LookupArtist = artist, LookupTitle = song, LookupAlbum = album }, null);

    /// <summary>Adds an existing track with an optional picture.</summary>
    public SnapshotBuilder Track(Guid id, ArtworkSource? picture = null, string sha = "4f2a9c0e1b3d", int minutes = 0)
    {
        var artwork = picture is { } source ? new TrackArtwork(source, sha.PadRight(64, '0'), T0) : null;
        _tracks.Add(new TrackArtRow(id, artwork, T0.AddMinutes(minutes)));
        return this;
    }

    /// <summary>Adds a filed raw export of one channel.</summary>
    public Asset Export(Guid trackId, TrackFacts facts, string channel = "drums", long size = 1000)
    {
        var asset = Asset.Create(Guid.NewGuid(), $"{facts.Title} - {channel}", LibraryTestData.ExportProvenance(trackId, facts.Title, facts.Artist, channel), Next());
        return Add(Ready(asset, size), facts);
    }

    /// <summary>Adds a filed mix of several channels.</summary>
    public Asset Mix(Guid trackId, TrackFacts facts, params string[] channels)
    {
        var provenance = LibraryTestData.ExportProvenance(trackId, facts.Title, facts.Artist) with
        {
            Recipe = new ExportRecipe { TrackId = trackId, Mode = ExportMode.Mix, Channels = channels, Format = new ExportFormat(AudioFormat.Flac, 16, null) },
        };
        return Add(Asset.Create(Guid.NewGuid(), $"{facts.Title} - mix", provenance, Next()), facts);
    }

    /// <summary>Adds a filed score.</summary>
    public Asset Score(Guid trackId, TrackFacts facts, ScoreProfile profile = ScoreProfile.Bass)
    {
        var provenance = LibraryTestData.ExportProvenance(trackId, facts.Title, facts.Artist) with
        {
            Origin = AssetOrigin.Transcription,
            Recipe = null,
            ScoreRecipe = new ScoreRecipe { Source = ScoreSource.FromChannel(trackId, "bass"), Profile = profile, Resolution = ScoreResolution.Sixteenth },
        };
        return Add(Asset.CreateScore(Guid.NewGuid(), $"{facts.Title} - score", provenance, Next()), facts);
    }

    /// <summary>Adds a filed generated take without a track ("My songs").</summary>
    public Asset Generated(string title)
    {
        var provenance = new Provenance { Origin = AssetOrigin.Generation, TrackTitle = title, Contents = "take 1", Generation = new GenerationSource(Guid.NewGuid(), 1, GenerationKind.Song) };
        var stored = new GeneratedAudio($"{title} - take 1", [new AssetFile("audio", "audio.flac", "audio/flac", 10, new string('a', 64))], new AudioFacts(AudioFormat.Flac, 24, null, 10, -1, 0));
        return Add(Asset.CreateGenerated(Guid.NewGuid(), provenance, stored, Next()), null);
    }

    /// <summary>Adds an item that is not filed yet.</summary>
    public Asset Unfiled(Guid trackId, TrackFacts facts)
    {
        var asset = Asset.Create(Guid.NewGuid(), "unfiled", LibraryTestData.ExportProvenance(trackId, facts.Title, facts.Artist), Next());
        _items.Add(asset);
        return asset;
    }

    public LibrarySnapshot Build() => LibrarySnapshot.Build(Revision, _items, _tracks);

    private static Asset Ready(Asset asset, long size)
    {
        asset.AssignJob(JobId, asset.CreatedAt);
        asset.Complete([new AssetFile("audio", "audio.flac", "audio/flac", size, new string('a', 64))], new AudioFacts(AudioFormat.Flac, 16, null, 10, -1, 0), asset.CreatedAt);
        return asset;
    }

    private Asset Add(Asset asset, TrackFacts? facts)
    {
        asset.File(PlacementResolver.Resolve(new PlacementInput { Provenance = asset.Provenance, Track = facts }), asset.CreatedAt);
        _items.Add(asset);
        return asset;
    }

    private DateTime Next() => T0.AddMinutes(++_clock);
}
