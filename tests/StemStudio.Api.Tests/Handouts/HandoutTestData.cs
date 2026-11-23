using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Tests.InstrumentViews;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Assets;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>Seeding and request helpers of the feature 020 handout tests.</summary>
internal static class HandoutTestData
{
    public static readonly DateTime T0 = SyncTestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A completed song with word-synced lyrics, detected sections and chords (C major chart).</summary>
    public static async Task<Track> SeedSongAsync(this ApiFactory factory, string videoId = "dQw4w9WgXcQ")
    {
        var track = await factory.SeedAsync(LyricsTestData.Found(videoId));
        await factory.SeedSongExtrasAsync(track.Id);
        return track;
    }

    /// <summary>Detected sections and a ready chord chart for a seeded track.</summary>
    public static async Task SeedSongExtrasAsync(this ApiFactory factory, Guid trackId)
    {
        await factory.SeedDetectedAsync(trackId);
        await factory.SeedReadyAsync(trackId, AnalysisTestData.Chart(), new string('f', 64));
    }

    /// <summary>A ready score item of a track.</summary>
    public static Asset Score(Guid trackId, (string Channel, ScoreProfile Profile) source, int minute, Func<ScoreFacts, ScoreFacts>? change = null)
    {
        var provenance = new Provenance
        {
            Origin = AssetOrigin.Transcription,
            TrackId = trackId,
            TrackTitle = "Song",
            Contents = "Sheet music",
            ScoreRecipe = new ScoreRecipe { Source = ScoreSource.FromChannel(trackId, source.Channel), Profile = source.Profile, Resolution = ScoreResolution.Sixteenth },
        };
        var asset = Asset.CreateScore(Guid.NewGuid(), $"Song {minute}", provenance, T0.AddMinutes(minute));
        asset.AssignJob("c4e6a8b0d2f4416385a7c9e1b3d5f7a9", T0);
        var facts = new ScoreFacts
        {
            Profile = source.Profile,
            Engine = ScoreEngine.BasicPitch,
            Key = new KeyInfo("A", KeyMode.Minor, 0.8, KeySource.Estimated),
            Bpm = 120,
            TimeSignature = "4/4",
            Bars = 10,
            Notes = 40,
            Pages = 1,
            Resolution = ScoreResolution.Sixteenth,
        };
        asset.CompleteScore([new AssetFile("pdf", "score.pdf", "application/pdf", 100, new string('a', 64))], change?.Invoke(facts) ?? facts, T0.AddMinutes(minute));
        return asset;
    }

    /// <summary>Stores items.</summary>
    public static Task<bool> SeedAssetsAsync(this ApiFactory factory, params Asset[] items) => factory.WithScopeAsync(async sp =>
    {
        var assets = sp.GetRequiredService<IAssetRepository>();
        foreach (var item in items)
        {
            assets.Add(item);
        }

        await assets.SaveAsync(CancellationToken.None);
        return true;
    });

    /// <summary>A tablature facts change.</summary>
    public static ScoreFacts Tab(ScoreFacts facts) =>
        facts with { Tablature = new TablatureFacts { Tuning = TuningId.Bass4Standard, Strings = 4, Capo = 0, Layout = TabLayout.NotationAndTab } };

    /// <summary>POST a handout request.</summary>
    public static Task<HttpResponseMessage> PostHandoutAsync(this HttpClient client, CreateHandoutRequest request) =>
        client.PostAsJsonAsync(ApiRoutes.Handouts, request, Ct);

    /// <summary>One asset synchroniser pass.</summary>
    public static Task<bool> SyncAssetsAsync(this ApiFactory factory) => factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<AssetSynchronizer>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    /// <summary>The stored item.</summary>
    public static Task<Asset?> FindAssetAsync(this ApiFactory factory, Guid id) =>
        factory.WithScopeAsync(sp => sp.GetRequiredService<IAssetRepository>().FindAsync(id, CancellationToken.None));

    /// <summary>The only analysis-free chart used by the songs: C major, N then C then G.</summary>
    public static ChordChart Chart => AnalysisTestData.Chart();
}
