using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StemStudio.Api.Handouts;
using StemStudio.Api.Handouts.Sources;
using StemStudio.Api.Insights;
using StemStudio.Api.Tests.Setlists;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Scores;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>
/// Independent code review of feature 020 (handouts): a PDF above the download cap fails the item with a readable text (1),
/// the score key is the written key of a concert-pitch score (2), the setlist table shows written keys (4), retries run under
/// the start lock and the active cap (5), and nothing is dropped silently (6).
/// </summary>
public sealed class HandoutReviewFixTests : ApiTestBase
{
    private const long Mib = 1024 * 1024;

    private static CreateHandoutRequest Bass(Guid trackId) => new() { Source = HandoutSourceDto.Song(trackId), Family = HandoutFamilyDto.Bass };

    [Fact]
    public async Task APdfAboveTheDownloadCap_FailsTheItem_WithAReadableText()
    {
        var song = await Factory.SeedSongAsync();
        var created = await (await Client.PostHandoutAsync(Bass(song.Id))).BodyAsync<AssetDto>();
        var job = Factory.InsightsWorker.HandoutJobs.Keys.Single();
        Factory.InsightsWorker.CompleteHandout(job);
        Factory.InsightsWorker.DownloadFailure = new WorkerResponseTooLargeException();

        await Factory.SyncAssetsAsync();

        var asset = (await Factory.FindAssetAsync(created.Id))!;
        (asset.Status, asset.Error, asset.Files.Count).ShouldBe((AssetStatus.Failed, HandoutJobApplier.TooLarge, 0));
        HandoutJobApplier.TooLarge.ShouldContain("192 MiB");
    }

    [Fact]
    public void TheDownloadCap_HoldsEveryAcceptedNotationTotal_WithAMargin()
    {
        WorkerClient.MaxHandoutBytes.ShouldBe(192 * Mib);
        (WorkerClient.MaxHandoutBytes - ContractLimits.MaxHandoutNotationBytes).ShouldBeGreaterThanOrEqualTo(64 * Mib);
    }

    [Fact]
    public async Task WithoutAChordKey_TheScoreKey_IsTheWrittenKeyOfAConcertPitchScore()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var bass = HandoutTestData.Score(track.Id, ("bass", ScoreProfile.Bass), 1);
        var part = HandoutTestData.Score(track.Id, ("other", ScoreProfile.Brass), 2, facts => facts with
        {
            Key = new KeyInfo("C", KeyMode.Major, 0.9, KeySource.Estimated),
            Part = new PartFacts { Instrument = Domain.InstrumentViews.WrittenInstrument.TrumpetBb, Name = "Trumpet", Interval = "M2", Clef = Domain.InstrumentViews.ClefChoice.Treble },
        });
        await Factory.SeedAssetsAsync(bass, part);

        using var response = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Song(track.Id), Family = HandoutFamilyDto.Melodic, Transposition = TranspositionDto.BFlat });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var facts = Factory.InsightsWorker.HandoutRequests.Single().Document.Songs.Single().Facts;
        (facts.Key, facts.KeySource).ShouldBe(("B minor", "score"));
    }

    [Fact]
    public async Task AFlatScoreKey_IsSpelledWithFlats_AndAnUnreadableTonicIsNoKey()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedAssetsAsync(HandoutTestData.Score(track.Id, ("bass", ScoreProfile.Bass), 1, facts => facts with { Key = new KeyInfo("B-", KeyMode.Major, 0.9, KeySource.Estimated) }));
        var other = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        await Factory.SeedAssetsAsync(HandoutTestData.Score(other.Id, ("bass", ScoreProfile.Bass), 1, facts => facts with { Key = new KeyInfo("H#", KeyMode.Major, 0.9, KeySource.Estimated) }));

        (await Client.PostHandoutAsync(Bass(track.Id))).Dispose();
        (await Client.PostHandoutAsync(Bass(other.Id))).Dispose();

        var keys = Factory.InsightsWorker.HandoutRequests.Select(request => request.Document.Songs.Single().Facts.Key).ToList();
        keys.ShouldBe(["Bb major", null]);
    }

    [Fact]
    public async Task TheSetlistTable_ShowsTheWrittenKeys()
    {
        var song = await Factory.SeedSongAsync();
        var setlist = await Client.CreateSetlistAsync("Horns");
        (await Client.PutEntriesAsync(setlist.Id, SetlistTestData.Entry(song.Id))).EnsureSuccessStatusCode();

        using var response = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Setlist(setlist.Id), Family = HandoutFamilyDto.Melodic, Transposition = TranspositionDto.BFlat });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var document = Factory.InsightsWorker.HandoutRequests.Single().Document;
        (document.Setlist!.Entries.Single().Key, document.Songs.Single().Facts.Key).ShouldBe(("D major", "D major"));
    }

    [Fact]
    public async Task ASecondRetryWhileTheFirstIsSubmitting_StartsNoSecondWorkerJob()
    {
        var song = await Factory.SeedSongAsync();
        var created = await FailedHandoutAsync(song.Id);
        var retry = new Uri(ApiRoutes.RetryAsset(created.Id), UriKind.Relative);
        Task<HttpResponseMessage>? second = null;
        Factory.InsightsWorker.AfterSubmit = () =>
        {
            Factory.InsightsWorker.AfterSubmit = null;
            second = Client.PostAsync(retry, null, Ct);
            return Task.Delay(200, Ct);
        };

        using var first = await Client.PostAsync(retry, null, Ct);
        using var late = await second!;

        (first.StatusCode, late.StatusCode).ShouldBe((HttpStatusCode.Accepted, HttpStatusCode.Conflict));
        (await late.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/asset-not-failed");
        Factory.InsightsWorker.HandoutRequests.Count.ShouldBe(2);
        (await Factory.FindAssetAsync(created.Id))!.WorkerJobId.ShouldBe(Factory.InsightsWorker.HandoutJobs.Keys.Single());
    }

    [Fact]
    public async Task ParallelRetries_StartExactlyOneWorkerJob()
    {
        var song = await Factory.SeedSongAsync();
        var created = await FailedHandoutAsync(song.Id);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Client.PostAsync(new Uri(ApiRoutes.RetryAsset(created.Id), UriKind.Relative), null, Ct)));

        responses.Count(response => response.StatusCode == HttpStatusCode.Accepted).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(4);
        Factory.InsightsWorker.HandoutRequests.Count.ShouldBe(2);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task ARetry_CountsAgainstTheActiveCap_ButNotTwice()
    {
        var song = await Factory.SeedSongAsync();
        var created = await FailedHandoutAsync(song.Id);
        for (var i = 1; i < HandoutService.MaxActivePerSource; i++)
        {
            (await Client.PostHandoutAsync(Bass(song.Id))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        using var fits = await Client.PostAsync(new Uri(ApiRoutes.RetryAsset(created.Id), UriKind.Relative), null, Ct);
        Factory.InsightsWorker.SetHandout(await JobOfAsync(created.Id), WorkerRenderJobState.Failed, 0, "boom");
        await Factory.SyncAssetsAsync();
        (await Client.PostHandoutAsync(Bass(song.Id))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var full = await Client.PostAsync(new Uri(ApiRoutes.RetryAsset(created.Id), UriKind.Relative), null, Ct);

        fits.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        full.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await full.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/handouts-active");
        (await Factory.FindAssetAsync(created.Id))!.Status.ShouldBe(AssetStatus.Failed);
    }

    [Fact]
    public async Task NotationBeyondEightPerSong_IsReported_AsNotationOmitted()
    {
        var song = await Factory.SeedSongAsync();
        var scores = Enumerable.Range(1, 10).Select(minute => HandoutTestData.Score(song.Id, ("bass", ScoreProfile.Bass), minute)).ToArray();
        await Factory.SeedAssetsAsync(scores);

        var created = await (await Client.PostHandoutAsync(Bass(song.Id) with { NotationAssetIds = [.. scores.Select(score => score.Id)] })).BodyAsync<AssetDto>();
        Factory.InsightsWorker.CompleteHandout(Factory.InsightsWorker.HandoutJobs.Keys.Single(), FakeWorkerInsightsResults.WithWarnings());
        await Factory.SyncAssetsAsync();

        Factory.InsightsWorker.HandoutRequests.Single().Document.Songs.Single().Notation.Count.ShouldBe(8);
        var asset = (await Factory.FindAssetAsync(created.Id))!;
        var omitted = asset.Handout!.Warnings.Where(warning => warning.Code == HandoutWarningCode.NotationOmitted).ToList();
        omitted.ShouldContain(warning => warning.Text == $"2 sheet music items of \"{song.Title}\" were left out: a handout prints at most 8 per song.");
        asset.Handout.Warnings.Count.ShouldBe(FakeWorkerInsightsResults.WithWarnings().Warnings.Count + 1);
    }

    [Fact]
    public async Task AnEmptyNotationId_Is422()
    {
        var song = await Factory.SeedSongAsync();

        using var response = await Client.PostHandoutAsync(Bass(song.Id) with { NotationAssetIds = [Guid.Empty] });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("notationAssetIds")).ShouldBe(["A notation item id cannot be empty."]);
        Factory.InsightsWorker.HandoutRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASongDeletedMeanwhile_LeavesNoGapInThePositions()
    {
        var (first, second) = (await Factory.SeedSongAsync("aaaaaaaaaaa"), await Factory.SeedSongAsync("bbbbbbbbbbb"));
        var snapshot = new SetlistSnapshot(Guid.NewGuid(), "Gig", null, [new SetlistSong(1, first.Id, null), new SetlistSong(2, Guid.NewGuid(), null), new SetlistSong(3, second.Id, "Last.")]);

        var draft = await Factory.WithScopeAsync(async sp =>
        {
            var planner = new HandoutPlanner(sp.GetRequiredService<ITrackRepository>(), new FixedSetlist(snapshot), sp.GetRequiredService<HandoutMaterialLoader>(), sp.GetRequiredService<IOptions<InsightsOptions>>());
            var recipe = new HandoutRecipe { Kind = HandoutKind.Setlist, SetlistId = snapshot.Id, Family = HandoutFamily.Overview, Paper = Paper.A4 };
            return (await planner.PlanAsync(new HandoutAsk(recipe, null), CancellationToken.None)).Draft!;
        });

        draft.Handout.Setlist!.Entries.Select(entry => (entry.Position, entry.TrackId, entry.Notes)).ShouldBe([(1, first.Id, (string?)null), (2, second.Id, "Last.")]);
        draft.Facts.Songs.ShouldBe(2);
    }

    private async Task<AssetDto> FailedHandoutAsync(Guid trackId)
    {
        var created = await (await Client.PostHandoutAsync(Bass(trackId))).BodyAsync<AssetDto>();
        Factory.InsightsWorker.SetHandout(await JobOfAsync(created.Id), WorkerRenderJobState.Failed, 0, "boom");
        await Factory.SyncAssetsAsync();
        return created;
    }

    private async Task<string> JobOfAsync(Guid assetId) => (await Factory.FindAssetAsync(assetId))!.WorkerJobId!;

    private sealed class FixedSetlist(SetlistSnapshot snapshot) : ISetlistSource
    {
        public Task<SetlistSnapshot?> FindAsync(Guid setlistId, CancellationToken cancellationToken) => Task.FromResult<SetlistSnapshot?>(snapshot);
    }
}
