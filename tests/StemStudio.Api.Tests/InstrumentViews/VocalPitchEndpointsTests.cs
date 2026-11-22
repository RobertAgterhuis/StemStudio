using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T095: the reference melody operations <c>getVocalPitch</c>, <c>analyseVocalPitch</c>, <c>deleteVocalPitch</c> (FR-040).</summary>
public sealed class VocalPitchEndpointsTests : ApiTestBase
{
    private const string ProblemBase = "https://stemstudio.local/problems/";

    private FakeWorkerAnalysisClient Analysis => Factory.AnalysisWorker;

    private static Uri Url(string route) => new(route, UriKind.Relative);

    private static VocalPitchDocument Melody(int frames) => new()
    {
        Engine = "pyin",
        Frames = Enumerable.Range(0, frames).Select(index => index % 3 == 0 ? 0 : 5700 + (index % 40)).ToList(),
        Source = VocalSource.VocalsLead,
        VoicedRatio = 0.66,
        Range = new PitchRange(5700, 5739),
    };

    private Task<HttpResponseMessage> PostAsync(Guid trackId) => Client.PostAsync(Url(ApiRoutes.VocalPitch(trackId)), null, Ct);

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage response) => (await response.JsonAsync()).GetProperty("type").GetString();

    [Fact]
    public async Task Get_NeverRun_IsNone_And404ForAnUnknownTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await Client.GetAsync(Url(ApiRoutes.VocalPitch(track.Id)), Ct);
        using var unknown = await Client.GetAsync(Url(ApiRoutes.VocalPitch(Guid.NewGuid())), Ct);

        await ContractAssert.MatchesAsync(response, "getVocalPitch");
        var dto = await response.BodyAsync<TrackAnalysisDto<VocalPitchDocumentDto>>();
        (dto.Kind, dto.Status, dto.Document, dto.Limits).ShouldBe((AnalysisKindDto.VocalPitch, AnalysisStatusDto.None, null, InstrumentViewMapper.VocalPitchLimits));
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analyse_UsesTheKeptLeadStem_WithAnEstimate_ThenTheMelodyIsReady()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        using var queued = await PostAsync(track.Id);

        queued.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(queued, "analyseVocalPitch");
        var dto = await queued.BodyAsync<TrackAnalysisDto<VocalPitchDocumentDto>>();
        (dto.Status, dto.EstimateSeconds > 0).ShouldBe((AnalysisStatusDto.Queued, true));
        Analysis.Requests.Single().ShouldBeOfType<WorkerVocalPitchRequest>().Source.ShouldBe("vocals.lead");

        Analysis.Complete(Analysis.Jobs.Keys.Single(), FakeWorkerAnalysisClient.VocalPitch());
        await Factory.SyncAnalysesAsync();

        using var ready = await Client.GetAsync(Url(ApiRoutes.VocalPitch(track.Id)), Ct);
        await ContractAssert.MatchesAsync(ready, "getVocalPitch");
        var melody = await ready.BodyAsync<TrackAnalysisDto<VocalPitchDocumentDto>>();
        (melody.Status, melody.Engine, melody.Document!.Source, melody.Document.Frames.Count).ShouldBe((AnalysisStatusDto.Ready, AnalysisEngineDto.Pyin, VocalSourceDto.VocalsLead, 4));
    }

    [Fact]
    public async Task Analyse_WithoutAVocalsSplit_UsesTheVocalsStem()
    {
        var track = await Factory.SeedAsync(DrumsSplitVocalsNot());

        using var queued = await PostAsync(track.Id);

        queued.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var request = Analysis.Requests.Single().ShouldBeOfType<WorkerVocalPitchRequest>();
        (request.Source, request.Input).ShouldBe(("vocals", $"tracks/{track.Id:D}/stems/vocals.flac"));
    }

    [Fact]
    public async Task Analyse_RefusesNoVocalsUnreadyTracksRunningAnalysesAndADownWorker()
    {
        var silent = await Factory.SeedAsync(WithoutStems());
        var importing = await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var running = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        await Factory.SeedQueuedAsync(running.Id, AnalysisKind.VocalPitch, JobId);

        using var noVocals = await PostAsync(silent.Id);
        using var notReady = await PostAsync(importing.Id);
        using var busy = await PostAsync(running.Id);
        using var unknown = await PostAsync(Guid.NewGuid());

        await ContractAssert.MatchesAsync(noVocals, "analyseVocalPitch");
        (await ProblemTypeAsync(noVocals), await ProblemTypeAsync(notReady), await ProblemTypeAsync(busy)).ShouldBe(
            (ProblemBase + InstrumentViewProblemTypes.NoVocals, ProblemBase + InstrumentViewProblemTypes.TrackNotReady, ProblemBase + InstrumentViewProblemTypes.AnalysisRunning));
        (noVocals.StatusCode, notReady.StatusCode, busy.StatusCode, unknown.StatusCode).ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.NotFound));

        Analysis.Unavailable = true;
        var fresh = await Factory.SeedAsync(SyncTestData.CompletedTrack("ccccccccccc"));
        using var down = await PostAsync(fresh.Id);
        down.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task TheMelody_NeverStartsOnItsOwn()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        await Factory.WithServiceAsync<bool, AnalysisAutoStarter>(async starter =>
        {
            await starter.OnCompletedAsync(track, Ct);
            await starter.OnDetailCompletedAsync(track, Ct);
            return true;
        });

        Analysis.Requests.ShouldNotContain(request => request is WorkerVocalPitchRequest);
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.VocalPitch)).ShouldBeNull();
    }

    [Fact]
    public async Task ALargeMelody_IsServedCompressed()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        await Factory.SeedReadyAsync(track.Id, Melody(90_000), new string('1', 64));
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(ApiRoutes.VocalPitch(track.Id)));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.ShouldBe(["gzip"]);
        var compressed = await response.Content.ReadAsByteArrayAsync(Ct);
        await using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var json = await JsonDocument.ParseAsync(gzip, cancellationToken: Ct);
        json.RootElement.GetProperty("document").GetProperty("frames").GetArrayLength().ShouldBe(90_000);
        compressed.Length.ShouldBeLessThan(200_000);
    }

    [Fact]
    public async Task OtherResponses_AreNotCompressed()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(ApiRoutes.Chords(track.Id)));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await Client.SendAsync(request, Ct);

        response.Content.Headers.ContentEncoding.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_ClearsTheMelody_And404ForAnUnknownTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        await Factory.SeedReadyAsync(track.Id, Melody(10), new string('1', 64));

        using var deleted = await Client.DeleteAsync(Url(ApiRoutes.VocalPitch(track.Id)), Ct);
        using var unknown = await Client.DeleteAsync(Url(ApiRoutes.VocalPitch(Guid.NewGuid())), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.MatchesAsync(deleted, "deleteVocalPitch");
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.VocalPitch))!.Status.ShouldBe(AnalysisStatus.None);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
