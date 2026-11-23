using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Handouts;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Setlists;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Setlists;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>
/// Security review of the 020 handout endpoints: enum values only by their wire names (finding 1), the worker job of a deleted
/// handout (2), the active limit and the name under one lock (4), no orphan worker jobs (5), the setlist cap of the options
/// (8) and no worker text in the answer (9).
/// </summary>
public sealed class HandoutSecurityTests : ApiTestBase
{
    [Theory]
    [InlineData("\"family\":99", "family")]
    [InlineData("\"family\":3", "family")]
    [InlineData("\"family\":\"3\"", "family")]
    [InlineData("\"family\":\"tuba\"", "family")]
    [InlineData("\"family\":\"bass, drums\"", "family")]
    [InlineData("\"family\":null", "family")]
    [InlineData("\"family\":\"bass\",\"include\":[99]", "include")]
    [InlineData("\"family\":\"bass\",\"include\":[0]", "include")]
    [InlineData("\"family\":\"bass\",\"include\":[\"facts\",{\"x\":1}]", "include")]
    [InlineData("\"family\":\"melodic\",\"transposition\":42", "transposition")]
    [InlineData("\"family\":\"melodic\",\"transposition\":1", "transposition")]
    [InlineData("\"family\":\"melodic\",\"transposition\":\"g\"", "transposition")]
    [InlineData("\"family\":\"bass\",\"paper\":7", "paper")]
    [InlineData("\"family\":\"bass\",\"paper\":1", "paper")]
    [InlineData("\"family\":\"bass\",\"paper\":[\"a4\"]", "paper")]
    public async Task EnumValues_AreAcceptedOnlyByTheirWireNames(string members, string field)
    {
        var song = await Factory.SeedSongAsync();

        using var response = await PostRawAsync($$"""{"source":{"trackId":"{{song.Id}}"},{{members}}}""");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createHandout");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        Factory.InsightsWorker.HandoutRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UndefinedEnumValues_AreRefused_BeforeTheTrackIsLookedUp()
    {
        using var response = await PostRawAsync($$"""{"source":{"trackId":"{{Guid.NewGuid()}}"},"family":99,"include":["facts"]}""");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("family")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WireNames_StillWork()
    {
        var song = await Factory.SeedSongAsync();

        using var response = await PostRawAsync($$"""{"source":{"trackId":"{{song.Id}}"},"family":"melodic","transposition":"b-flat","paper":"letter","include":["facts","notation"]}""");
        using var nulls = await PostRawAsync($$"""{"source":{"trackId":"{{song.Id}}"},"family":"bass","transposition":null,"paper":null,"include":null}""");

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldContain("\"transposition\":\"b-flat\"");
        body.ShouldContain("\"paper\":\"letter\"");
        nulls.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task DeletingAQueuedHandout_DeletesItsHandoutJob()
    {
        var song = await Factory.SeedSongAsync();
        var created = await (await Client.PostHandoutAsync(Bass(song.Id))).BodyAsync<AssetDto>();
        var job = Factory.InsightsWorker.HandoutJobs.Keys.Single();

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Asset(created.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.InsightsWorker.Deleted.ShouldBe([$"handout:{job}"]);
        Factory.Worker.RenderDeletes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParallelCreates_OfOneSource_StopAtTheActiveLimit_WithDistinctNames()
    {
        var song = await Factory.SeedSongAsync();
        var other = await Factory.SeedSongAsync("bbbbbbbbbbb");

        var responses = await Task.WhenAll(Enumerable.Range(0, HandoutService.MaxActivePerSource + 3).Select(_ => Client.PostHandoutAsync(Bass(song.Id))));
        using var elsewhere = await Client.PostHandoutAsync(Bass(other.Id));

        var accepted = responses.Where(response => response.StatusCode == HttpStatusCode.Accepted).ToList();
        var refused = responses.Where(response => response.StatusCode == HttpStatusCode.Conflict).ToList();
        (accepted.Count, refused.Count, elsewhere.StatusCode).ShouldBe((HandoutService.MaxActivePerSource, 3, HttpStatusCode.Accepted));
        var names = new List<string>();
        foreach (var response in accepted)
        {
            names.Add((await response.BodyAsync<AssetDto>()).Name);
        }

        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(HandoutService.MaxActivePerSource);
        await ContractAssert.MatchesAsync(refused[0], "createHandout");
        (await refused[0].JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/jobs-active");
        Factory.InsightsWorker.HandoutJobs.Count.ShouldBe(HandoutService.MaxActivePerSource + 1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task AFinishedHandout_FreesItsPlace()
    {
        var song = await Factory.SeedSongAsync();
        for (var i = 0; i < HandoutService.MaxActivePerSource; i++)
        {
            (await Client.PostHandoutAsync(Bass(song.Id))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        using var full = await Client.PostHandoutAsync(Bass(song.Id));
        Factory.InsightsWorker.CompleteHandout(Factory.InsightsWorker.HandoutJobs.Keys.Order(StringComparer.Ordinal).First());
        await Factory.SyncAssetsAsync();
        using var again = await Client.PostHandoutAsync(Bass(song.Id));

        (full.StatusCode, again.StatusCode).ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.Accepted));
    }

    [Fact]
    public async Task AFailedSave_AfterTheSubmit_DeletesTheWorkerJob()
    {
        var failure = new SaveFailure();
        using var factory = new ApiFactory { ConfigureServices = failure.AddTo };
        using var client = factory.CreateClient();
        var song = await factory.SeedSongAsync();
        failure.When = context => context.ChangeTracker.Entries<Asset>().Any(entry => entry.State == EntityState.Added && entry.Entity.Kind == AssetKind.Handout);

        using var response = await client.PostHandoutAsync(Bass(song.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        failure.Failures.ShouldBe(1);
        factory.InsightsWorker.HandoutRequests.Count.ShouldBe(1);
        factory.InsightsWorker.Deleted.Single().ShouldStartWith("handout:");
        factory.InsightsWorker.HandoutJobs.ShouldBeEmpty();
        (await HandoutsAsync(factory)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AnInvalidWorkerJobId_StoresNothing_AndIsWorkerUnavailable()
    {
        var song = await Factory.SeedSongAsync();
        Factory.InsightsWorker.NextJobId = "../not-a-job";

        using var response = await Client.PostHandoutAsync(Bass(song.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "createHandout");
        Factory.InsightsWorker.Deleted.ShouldBe(["handout:../not-a-job"]);
        (await HandoutsAsync(Factory)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ACancelledRequest_AfterTheSubmit_StillStoresTheItem()
    {
        var song = await Factory.SeedSongAsync();
        using var cancel = new CancellationTokenSource();
        Factory.InsightsWorker.AfterSubmit = () => cancel.CancelAsync();

        var result = await Factory.WithScopeAsync(sp => sp.GetRequiredService<HandoutService>().CreateAsync(Bass(song.Id), cancel.Token));

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>()!.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        (await HandoutsAsync(Factory)).Single().WorkerJobId.ShouldBe(Factory.InsightsWorker.HandoutJobs.Keys.Single());
        Factory.InsightsWorker.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetlistOptions_ReadAtMostTheSongLimit()
    {
        var max = ContractLimits.MaxSetlistHandoutSongs;
        var entries = new List<SetlistEntryInputDto>();
        for (var i = 0; i <= max; i++)
        {
            var track = await Factory.SeedAsync(SyncTestData.CompletedTrack(string.Create(CultureInfo.InvariantCulture, $"v{i:D10}")));
            entries.Add(SetlistTestData.Entry(track.Id));
        }

        var setlist = await Client.CreateSetlistAsync("Long night");
        (await Client.PutEntriesAsync(setlist.Id, [.. entries])).EnsureSuccessStatusCode();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Setlist(setlist.Id), HandoutFamilyDto.Overview), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getHandoutOptions");
        (await response.BodyAsync<HandoutOptionsDto>()).Songs.ShouldBe(max);
    }

    private static Task<List<Asset>> HandoutsAsync(ApiFactory factory) => factory.WithScopeAsync(async sp =>
        (await sp.GetRequiredService<IAssetRepository>().ListActiveAsync(CancellationToken.None)).Where(asset => asset.Kind == AssetKind.Handout).ToList());

    private static CreateHandoutRequest Bass(Guid trackId) => new() { Source = HandoutSourceDto.Song(trackId), Family = HandoutFamilyDto.Bass };

    private Task<HttpResponseMessage> PostRawAsync(string json) =>
        Client.PostAsync(new Uri(ApiRoutes.Handouts, UriKind.Relative), new StringContent(json, Encoding.UTF8, "application/json"), Ct);
}
