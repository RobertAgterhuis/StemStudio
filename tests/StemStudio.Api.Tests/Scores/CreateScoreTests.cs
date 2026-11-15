using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.Scores.ScoreTestData;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Feature 008 T041: score options, creating a score, syncing it to ready and serving its files (FR-001, FR-003, FR-010, FR-012).</summary>
public sealed class CreateScoreTests : IDisposable
{
    private readonly FakeDiskSpace _disk = new();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CreateScoreTests()
    {
        _factory = new ApiFactory { ConfigureServices = services => services.AddSingleton<IDiskSpace>(_disk) };
        _client = _factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private FakeWorkerClient Worker => _factory.Worker;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Options_ForTheBassOnACpuWorker()
    {
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await GetOptionsAsync(ApiRoutes.ScoreOptionsForChannel(track.Id, "bass"));

        await ContractAssert.MatchesAsync(response, "getScoreOptions");
        var options = await response.BodyAsync<ScoreOptionsDto>();
        (options.SuggestedProfile, options.DrumsRequireSplit, options.GridStatus, options.DefaultQuality, options.DurationSeconds)
            .ShouldBe((ScoreProfileDto.Bass, false, GridStatusDto.Reliable, ScoreQualityDto.Fast, 213.04));
        options.Source.ShouldBe(AudioSourceDto.FromChannel(track.Id, "bass"));
        options.AllowedProfiles.ShouldBe([ScoreProfileDto.Vocals, ScoreProfileDto.Bass, ScoreProfileDto.Guitar, ScoreProfileDto.Keys, ScoreProfileDto.Piano]);
        options.Estimates.ShouldBe(
        [
            new ScoreEstimateDto(ScoreProfileDto.Vocals, null, 16), new ScoreEstimateDto(ScoreProfileDto.Bass, null, 16), new ScoreEstimateDto(ScoreProfileDto.Guitar, null, 16),
            new ScoreEstimateDto(ScoreProfileDto.Keys, null, 16), new ScoreEstimateDto(ScoreProfileDto.Piano, ScoreQualityDto.Accurate, 161), new ScoreEstimateDto(ScoreProfileDto.Piano, ScoreQualityDto.Fast, 16),
        ]);
        options.Limits.Select(limit => limit.Profile).ShouldBe(options.AllowedProfiles);
        options.Attribution.ShouldBe(ProfileCatalog.PianoAttribution);
    }

    [Fact]
    public async Task Options_OnAGpuWorkerWithThePianoModelDefaultToAccurate()
    {
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack());
        Worker.OnGetHealth = () => Task.FromResult(Health(ProcessingDeviceKind.Gpu, pianoReady: true));

        var options = await (await GetOptionsAsync(ApiRoutes.ScoreOptionsForChannel(track.Id, "piano"))).BodyAsync<ScoreOptionsDto>();

        (options.SuggestedProfile, options.DefaultQuality).ShouldBe((ScoreProfileDto.Piano, ScoreQualityDto.Accurate));
        options.Estimates.ShouldContain(new ScoreEstimateDto(ScoreProfileDto.Piano, ScoreQualityDto.Accurate, 48));
    }

    [Fact]
    public async Task Options_ProblemsMatchTheContract()
    {
        var processing = await _factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack("bbbbbbbbbbb"));

        using var notReady = await GetOptionsAsync(ApiRoutes.ScoreOptionsForChannel(processing.Id, "bass"));
        using var unknownTrack = await GetOptionsAsync(ApiRoutes.ScoreOptionsForChannel(Guid.NewGuid(), "bass"));
        using var unknownAsset = await GetOptionsAsync(ApiRoutes.ScoreOptionsForAsset(Guid.NewGuid()));
        using var noSource = await GetOptionsAsync(ApiRoutes.ScoreOptions);
        using var both = await GetOptionsAsync($"{ApiRoutes.ScoreOptionsForChannel(track.Id, "bass")}&assetId={Guid.NewGuid()}");
        using var missingChannel = await GetOptionsAsync(ApiRoutes.ScoreOptionsForChannel(track.Id, "drums.kick"));

        (notReady.StatusCode, unknownTrack.StatusCode, unknownAsset.StatusCode).ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        (await notReady.JsonAsync()).GetProperty("type").GetString().ShouldBe("track-not-ready");
        foreach (var invalid in new[] { noSource, both, missingChannel })
        {
            invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }

        foreach (var response in new[] { notReady, unknownTrack, unknownAsset, noSource, both, missingChannel })
        {
            await ContractAssert.MatchesAsync(response, "getScoreOptions");
        }
    }

    [Fact]
    public async Task Create_QueuesAScoreItemAndSubmitsTheTranscription()
    {
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack());
        FixedJobId(Worker);

        using var response = await _client.PostScoreAsync(Request(track.Id, "bass"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "createScore");
        var asset = await response.BodyAsync<AssetDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Asset(asset.Id));
        (asset.Kind, asset.Status, asset.Name, asset.Score, asset.Audio).ShouldBe((AssetKindDto.Score, AssetStatusDto.Queued, "Never Gonna Give You Up - bass (sheet music)", null, null));
        var provenance = asset.Provenance;
        (provenance.Origin, provenance.Contents, provenance.Recipe, provenance.TrackId).ShouldBe((AssetOriginDto.Transcription, "bass, sheet music", null, track.Id));
        var recipe = provenance.ScoreRecipe!;
        (recipe.Kind, recipe.Profile, recipe.Quality, recipe.Resolution, recipe.Source).ShouldBe(("score", ScoreProfileDto.Bass, null, ScoreResolutionDto.Sixteenth, AudioSourceDto.FromChannel(track.Id, "bass")));
        var sent = Worker.TranscriptionCreates.Single();
        (sent.Profile, sent.Input, sent.DrumInputs, sent.Range, sent.Resolution, sent.KeyOverride).ShouldBe((ScoreProfile.Bass, new WorkerTranscriptionInput($"tracks/{track.Id:D}/stems/bass.flac"), null, null, ScoreResolution.Sixteenth, null));
        sent.Titles.ShouldBe(new WorkerScoreTitles("Never Gonna Give You Up - bass (sheet music)", "Never Gonna Give You Up - Rick Astley", "Bass"));
        var grid = sent.Grid.ShouldBeOfType<WorkerBeatGridTempo>();
        (grid.Bpm, grid.BeatsPerBar).ShouldBe((113.5, 4));
        grid.Beats.ShouldBe(track.BeatGrid!.Beats);
        grid.Downbeats.ShouldBe(track.BeatGrid.Downbeats);
    }

    [Fact]
    public async Task Create_ADetailChannelAndANamedRequest()
    {
        var track = await _factory.SeedAsync(DrumsSplitTrack());

        using var response = await _client.PostScoreAsync(Request(track.Id, "vocals.lead") with { Name = "Lead line" });

        var asset = await response.BodyAsync<AssetDto>();
        (asset.Name, asset.Provenance.ScoreRecipe!.Profile, asset.Provenance.ScoreRecipe.Resolution).ShouldBe(("Lead line", ScoreProfileDto.Vocals, ScoreResolutionDto.Eighth));
        Worker.TranscriptionCreates.Single().Input!.Path.ShouldBe($"tracks/{track.Id:D}/detail/vocals/lead.flac");
    }

    [Fact]
    public async Task Create_ProblemsMatchTheContract()
    {
        var processing = await _factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack("bbbbbbbbbbb"));

        using var notReady = await _client.PostScoreAsync(Request(processing.Id, "bass"));
        using var unknown = await _client.PostScoreAsync(Request(Guid.NewGuid(), "bass"));
        using var missingChannel = await _client.PostScoreAsync(Request(track.Id, "vocals.lead"));
        _disk.Available = 0;
        using var full = await _client.PostScoreAsync(Request(track.Id, "bass"));

        (notReady.StatusCode, unknown.StatusCode, missingChannel.StatusCode, full.StatusCode)
            .ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity, HttpStatusCode.Conflict));
        (await full.JsonAsync()).GetProperty("type").GetString().ShouldBe("insufficient-disk-space");
        foreach (var response in new[] { notReady, unknown, missingChannel, full })
        {
            await ContractAssert.MatchesAsync(response, "createScore");
        }

        Worker.TranscriptionCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WorkerProblemsAreReportedAndNothingIsStored()
    {
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack());
        Worker.OnCreateTranscription = _ => throw new WorkerRequestException(HttpStatusCode.NotFound, "Input tracks/x/stems/bass.flac was not found.");

        using var rejected = await _client.PostScoreAsync(Request(track.Id, "bass"));
        Worker.OnCreateTranscription = _ => throw new WorkerUnavailableException();
        using var unreachable = await _client.PostScoreAsync(Request(track.Id, "bass"));

        (rejected.StatusCode, unreachable.StatusCode).ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.ServiceUnavailable));
        (await rejected.JsonAsync()).GetProperty("type").GetString().ShouldBe("source-unavailable");
        (await (await _client.GetAsync(new Uri($"{ApiRoutes.Assets}?kind=score", UriKind.Relative), Ct)).BodyAsync<AssetListDto>()).Total.ShouldBe(0);
    }

    [Fact]
    public async Task Sync_CopiesTheThreeFilesAndFillsTheScore()
    {
        var asset = await CreateBassAsync();
        CompleteTranscriptions(Worker, device: new WorkerJobDevice(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", false, null));

        await _factory.SyncAssetsAsync();

        using var response = await _client.GetAsync(new Uri(ApiRoutes.Asset(asset.Id), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getAsset");
        var ready = await response.BodyAsync<AssetDto>();
        ready.Error.ShouldBeNull();
        (ready.Status, ready.Progress, ready.SizeBytes).ShouldBe((AssetStatusDto.Ready, 100, Roles.Sum(role => (long)FileBytes(role).Length)));
        ready.Files.Select(file => (file.Role, file.ContentType, file.Sha256)).ShouldBe(
            [(AssetFileRoleDto.Pdf, "application/pdf", Sha("pdf")), (AssetFileRoleDto.MusicXml, "application/vnd.recordare.musicxml+xml", Sha("musicxml")), (AssetFileRoleDto.Midi, "audio/midi", Sha("midi"))]);
        var score = ready.Score!;
        (score.Profile, score.Engine, score.Bpm, score.TimeSignature, score.Bars, score.Notes, score.Pages, score.Resolution).ShouldBe((ScoreProfileDto.Bass, ScoreEngineDto.BasicPitch, 118, "4/4", 125, 386, 3, ScoreResolutionDto.Sixteenth));
        score.Key.ShouldBe(new KeyInfoDto("A-", KeyModeDto.Major, 0.83, KeySourceDto.Estimated));
        score.Warnings.Single().ShouldBe(new ScoreWarningDto(ScoreWarningCodeDto.NotesCleaned, "87 very short or out-of-range notes were removed."));
        score.Device!.Kind.ShouldBe(ProcessingDeviceKindDto.Gpu);
        Worker.TranscriptionDeletes.ShouldBe([JobId]);
    }

    [Fact]
    public async Task Sync_ReportsProgressAndKeepsQueuedJobs()
    {
        var asset = await CreateBassAsync();
        Worker.OnGetTranscription = id => Task.FromResult<WorkerTranscriptionJob?>(FakeWorkerClient.TranscriptionJob(id, WorkerRenderJobState.Running, 0.42));

        await _factory.SyncAssetsAsync();
        var running = await GetAssetAsync(asset.Id);
        Worker.OnGetTranscription = id => Task.FromResult<WorkerTranscriptionJob?>(FakeWorkerClient.TranscriptionJob(id, WorkerRenderJobState.Queued));
        await _factory.SyncAssetsAsync();

        (running.Status, running.Progress).ShouldBe((AssetStatusDto.Rendering, 42));
        (await GetAssetAsync(asset.Id)).Progress.ShouldBe(42);
        Worker.TranscriptionDeletes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("lost")]
    [InlineData("damaged")]
    [InlineData("invalid")]
    [InlineData("unreadable")]
    public async Task Sync_FailuresFailTheItemWithAReason(string failure)
    {
        var asset = await CreateBassAsync();
        Fail(failure);

        await _factory.SyncAssetsAsync();

        var failed = await GetAssetAsync(asset.Id);
        (failed.Status, failed.Files.Count, failed.Score).ShouldBe((AssetStatusDto.Failed, 0, null));
        failed.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Sync_AnUnreachableWorkerKeepsTheItem()
    {
        var asset = await CreateBassAsync();
        Worker.OnGetTranscription = _ => throw new WorkerUnavailableException();

        await _factory.SyncAssetsAsync();

        (await GetAssetAsync(asset.Id)).Status.ShouldBe(AssetStatusDto.Queued);
    }

    [Theory]
    [InlineData("pdf", null, "attachment", ".pdf")]
    [InlineData("pdf", "true", "inline", ".pdf")]
    [InlineData("musicxml", null, "attachment", ".musicxml")]
    [InlineData("midi", null, "attachment", ".mid")]
    public async Task Files_AreServedPerRole(string role, string? inline, string disposition, string extension)
    {
        var asset = await ReadyBassAsync();
        var path = ApiRoutes.AssetFile(asset.Id, role) + (inline is null ? string.Empty : $"?inline={inline}");
        var contentType = Result().Files.Single(file => file.Role == role).ContentType;

        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getAssetFile");
        (response.Content.Headers.ContentType!.MediaType, response.Content.Headers.ContentDisposition!.DispositionType).ShouldBe((contentType, disposition));
        response.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("Never Gonna Give You Up - bass (sheet music)" + extension);
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(FileBytes(role));
    }

    [Fact]
    public async Task TheLibrary_FiltersScores()
    {
        await ReadyBassAsync();

        var scores = await (await _client.GetAsync(new Uri($"{ApiRoutes.Assets}?kind=score", UriKind.Relative), Ct)).BodyAsync<AssetListDto>();
        var audio = await (await _client.GetAsync(new Uri($"{ApiRoutes.Assets}?kind=audio", UriKind.Relative), Ct)).BodyAsync<AssetListDto>();

        (scores.Total, audio.Total).ShouldBe((1, 0));
    }

    [Fact]
    public async Task Retry_ResubmitsAFailedScoreFromItsRecipe()
    {
        var asset = await CreateBassAsync();
        Fail("failed");
        await _factory.SyncAssetsAsync();

        using var response = await _client.PostAsync(new Uri(ApiRoutes.RetryAsset(asset.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.BodyAsync<AssetDto>()).Status.ShouldBe(AssetStatusDto.Queued);
        Worker.TranscriptionCreates.Count.ShouldBe(2);
        Worker.TranscriptionCreates[1].ShouldBeEquivalentTo(Worker.TranscriptionCreates[0]);
    }

    [Fact]
    public async Task Delete_RemovesAnActiveTranscription()
    {
        var asset = await CreateBassAsync();

        using var response = await _client.DeleteAsync(new Uri(ApiRoutes.Asset(asset.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Worker.TranscriptionDeletes.ShouldBe([JobId]);
        Worker.RenderDeletes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Options_WithAnUnreachableWorkerAssumeTheCpu()
    {
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack());
        Worker.OnGetHealth = () => throw new WorkerUnavailableException();

        var options = await (await GetOptionsAsync(ApiRoutes.ScoreOptionsForChannel(track.Id, "piano"))).BodyAsync<ScoreOptionsDto>();

        options.DefaultQuality.ShouldBe(ScoreQualityDto.Fast);
    }

    [Fact]
    public async Task Create_AnInvalidNameIs422()
    {
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await _client.PostScoreAsync(Request(track.Id, "bass") with { Name = new string('x', 121) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("name")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Retry_OfAScoreWhoseTrackIsGoneIs404()
    {
        var asset = await CreateBassAsync();
        Fail("failed");
        await _factory.SyncAssetsAsync();
        (await _client.DeleteAsync(new Uri(ApiRoutes.Track(asset.Provenance.TrackId!.Value), UriKind.Relative), Ct)).Dispose();

        using var response = await _client.PostAsync(new Uri(ApiRoutes.RetryAsset(asset.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private void Fail(string failure)
    {
        switch (failure)
        {
            case "failed":
                Worker.OnGetTranscription = id => Task.FromResult<WorkerTranscriptionJob?>(FakeWorkerClient.TranscriptionJob(id, WorkerRenderJobState.Failed, 0.8));
                break;
            case "lost":
                Worker.OnGetTranscription = _ => Task.FromResult<WorkerTranscriptionJob?>(null);
                break;
            case "damaged":
                CompleteTranscriptions(Worker, Result(pdfSha: new string('0', 64)));
                break;
            case "invalid":
                CompleteTranscriptions(Worker, Result(Summary() with { Bpm = 5 }));
                break;
            default:
                CompleteTranscriptions(Worker);
                Worker.OnDownloadTranscriptionFile = (_, _, _) => throw new WorkerRequestException(HttpStatusCode.Conflict, "Transcription job is in state running.");
                break;
        }
    }

    private async Task<AssetDto> CreateBassAsync()
    {
        var track = await _factory.SeedAsync(AssetTestData.NamedTrack());
        FixedJobId(Worker);
        return await (await _client.PostScoreAsync(Request(track.Id, "bass"))).BodyAsync<AssetDto>();
    }

    private async Task<AssetDto> ReadyBassAsync()
    {
        var asset = await CreateBassAsync();
        CompleteTranscriptions(Worker);
        await _factory.SyncAssetsAsync();
        return asset;
    }

    private async Task<AssetDto> GetAssetAsync(Guid id) => await (await _client.GetAsync(new Uri(ApiRoutes.Asset(id), UriKind.Relative), Ct)).BodyAsync<AssetDto>();

    private Task<HttpResponseMessage> GetOptionsAsync(string path) => _client.GetAsync(new Uri(path, UriKind.Relative), Ct);
}
