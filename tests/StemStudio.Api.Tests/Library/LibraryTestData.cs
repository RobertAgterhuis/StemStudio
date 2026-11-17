using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Library;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Library;

/// <summary>Shared data and helpers of the feature 022 API tests.</summary>
internal static class LibraryTestData
{
    public static readonly DateTime T0 = SyncTestData.T0;

    /// <summary>A completed track matched by the band info lookup (Metallica > Metallica (1991) > Enter Sandman).</summary>
    public static Track MatchedTrack(string videoId = "aaaaaaaaaaa", string artist = "Metallica", string album = "Metallica", string song = "Enter Sandman")
    {
        var track = AssetTestData.NamedTrack(videoId);
        Match(track, Found(artist, album, song));
        return track;
    }

    /// <summary>A found band info result.</summary>
    public static LookupResult Found(string artist, string album, string song, int? year = 1991) =>
        new(LookupOutcome.Found) { ArtistName = artist, Album = album, RecordingTitle = song, Year = year };

    /// <summary>Runs a lookup with the given result on a track.</summary>
    public static void Match(Track track, LookupResult result)
    {
        track.ScheduleLookup(new LookupQuery(result.RecordingTitle, result.ArtistName), T0);
        track.StartLookup("0123456789abcdef0123456789abcdee", T0);
        track.CompleteLookup(result, T0);
    }

    public static Provenance ExportProvenance(Guid? trackId, string title = "Never Gonna Give You Up", string? artist = "Rick Astley", string channel = "drums") => new()
    {
        Origin = AssetOrigin.Export,
        TrackId = trackId,
        TrackTitle = title,
        TrackArtist = artist,
        Contents = $"{channel}, raw",
        Recipe = new ExportRecipe { TrackId = trackId ?? Guid.Empty, Mode = ExportMode.Raw, Channels = [channel], Format = new ExportFormat(AudioFormat.Flac, 16, null) },
    };

    /// <summary>Adds items straight to the database (not filed), as a pre-022 library has them.</summary>
    public static Task<bool> AddUnfiledAsync(this ApiFactory factory, params Asset[] assets) => factory.WithScopeAsync(async sp =>
    {
        var repository = sp.GetRequiredService<IAssetRepository>();
        foreach (var asset in assets)
        {
            repository.Add(asset);
        }

        await repository.SaveAsync(CancellationToken.None);
        return true;
    });

    /// <summary>Files items in one scope and saves them.</summary>
    public static Task<bool> FileAsync(this ApiFactory factory, params Asset[] assets) => factory.WithScopeAsync(async sp =>
    {
        var filer = sp.GetRequiredService<ILibraryFiler>();
        var repository = sp.GetRequiredService<IAssetRepository>();
        foreach (var asset in assets)
        {
            await filer.FileAsync(asset, CancellationToken.None);
            repository.Add(asset);
        }

        await repository.SaveAsync(CancellationToken.None);
        return true;
    });

    public static Task<Asset?> FindAssetAsync(this ApiFactory factory, Guid id) =>
        factory.WithScopeAsync(sp => sp.GetRequiredService<IAssetRepository>().FindAsync(id, CancellationToken.None));

    public static (string Name, PlacementSource Source) Level(PlacementLevel level) => (level.Name, level.Source);
}
