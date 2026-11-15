using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;
using static StemStudio.Api.Tests.Assets.AssetTestData;

namespace StemStudio.Api.Tests.Assets;

/// <summary>Feature 007 T035: raw exports, their sync and the file route (FR-001, FR-004, FR-015, SC-004).</summary>
public sealed class RawExportTests : IDisposable
{
    private readonly FakeDiskSpace _disk = new();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RawExportTests()
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
    public async Task RawStem_CreatesAQueuedItemAndARawRender()
    {
        var track = await _factory.SeedAsync(NamedTrack());

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        var asset = await response.BodyAsync<AssetDto>();
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Asset(asset.Id));
        (asset.Status, asset.Name, asset.Progress, asset.Files.Count).ShouldBe((AssetStatusDto.Queued, "Never Gonna Give You Up - drums", 0, 0));
        var provenance = asset.Provenance;
        (provenance.Contents, provenance.TrackId, provenance.TrackTitle, provenance.TrackArtist, provenance.TrackAvailable)
            .ShouldBe(("drums, raw", (Guid?)track.Id, "Never Gonna Give You Up", "Rick Astley", true));
        provenance.Recipe!.Format.ShouldBe(new ExportFormatDto(AudioFileFormatDto.Flac, 16, null));
        var render = Worker.RenderCreates.Single();
        render.Inputs.ShouldBe([new WorkerRenderInput($"tracks/{track.Id:D}/stems/drums.flac", 1, 0)]);
        (render.MasterGain, render.Range, render.Output).ShouldBe((1, null, new WorkerRenderOutput(WorkerRenderFormat.Flac) { BitDepth = 16, Dither = false }));
        render.Metadata.ShouldBe(new WorkerRenderMetadata("Never Gonna Give You Up", "Rick Astley", "drums, raw"));
    }

    [Fact]
    public async Task RawDetailStem_RendersTheDetailFile()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums.kick"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.RenderCreates.Single().Inputs.Single().Path.ShouldBe($"tracks/{track.Id:D}/detail/drums/kick.flac");
    }

    [Fact]
    public async Task TwoRawChannels_Use24BitsWithDitherAndAUniqueDefaultName()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        (await _client.PostExportAsync(Raw(track.Id, "vocals", "bass"))).Dispose();

        var second = await (await _client.PostExportAsync(Raw(track.Id, "bass", "vocals"))).BodyAsync<AssetDto>();

        second.Name.ShouldBe("Never Gonna Give You Up - vocals + bass (2)");
        Worker.RenderCreates[1].Output.ShouldBe(new WorkerRenderOutput(WorkerRenderFormat.Flac) { BitDepth = 24, Dither = true });
    }

    [Fact]
    public async Task AGivenNameAndMp3_AreUsed()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        var request = Raw(track.Id, "drums") with { Name = " Drums for practice ", Format = new ExportFormatDto(AudioFileFormatDto.Mp3) };

        var asset = await (await _client.PostExportAsync(request)).BodyAsync<AssetDto>();

        asset.Name.ShouldBe("Drums for practice");
        Worker.RenderCreates.Single().Output.ShouldBe(new WorkerRenderOutput(WorkerRenderFormat.Mp3) { Mp3BitrateKbps = 320, Dither = false });
        asset.Provenance.Recipe!.Format.ShouldBe(new ExportFormatDto(AudioFileFormatDto.Mp3, null, 320));
    }

    [Fact]
    public async Task ATrackStillProcessing_Is409TrackNotReady()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("track-not-ready");
    }

    [Fact]
    public async Task AnUnknownTrack_Is404()
    {
        using var response = await _client.PostExportAsync(Raw(Guid.NewGuid(), "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
    }

    [Fact]
    public async Task LittleFreeSpace_Is409InsufficientDiskSpace()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        _disk.Available = ContractLimits.MinFreeDiskBytesDefault - 1;

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("insufficient-disk-space");
        Worker.RenderCreates.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("piano.kick", "channels[0]")]
    [InlineData("drums.kick", "channels[0]")]
    public async Task UnknownChannels_Are422NamingTheEntry(string channel, string field)
    {
        var track = await _factory.SeedAsync(NamedTrack());

        using var response = await _client.PostExportAsync(Raw(track.Id, channel));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(AudioFileFormatDto.Flac, 20, null)]
    [InlineData(AudioFileFormatDto.Mp3, null, 128)]
    [InlineData(AudioFileFormatDto.Wav, null, 320)]
    [InlineData(AudioFileFormatDto.Mp3, 16, null)]
    public async Task InvalidFormats_Are422(AudioFileFormatDto format, int? bitDepth, int? bitrate)
    {
        var track = await _factory.SeedAsync(NamedTrack());

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums") with { Format = new ExportFormatDto(format, bitDepth, bitrate) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("format")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AnInvalidName_Is422()
    {
        var track = await _factory.SeedAsync(NamedTrack());

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums") with { Name = "tab\tname" });

        (await response.FieldErrorsAsync("name")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AnUnreachableWorker_Is503AndStoresNothing()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        Worker.OnCreateRender = _ => throw new WorkerUnavailableException();

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        (await ListAsync()).Total.ShouldBe(0);
    }

    [Fact]
    public async Task AMissingStemFile_Is409SourceUnavailable()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        Worker.OnCreateRender = _ => throw new WorkerRequestException(HttpStatusCode.NotFound, "Input not found: tracks/x/stems/drums.flac");

        using var response = await _client.PostExportAsync(Raw(track.Id, "drums"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "createAudioExport");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("source-unavailable");
    }

    [Fact]
    public async Task Sync_RunningIsRenderingWithProgress()
    {
        var asset = await CreateDrumsAsync();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Running, 0.62));

        await _factory.SyncAssetsAsync();

        var dto = await GetAssetAsync(asset.Id);
        (dto.Status, dto.Progress).ShouldBe((AssetStatusDto.Rendering, 62));
    }

    [Fact]
    public async Task Sync_CompletedStoresTheVerifiedFileAndDeletesTheWorkerJob()
    {
        var asset = await CreateDrumsAsync();
        CompleteRenders(Worker);

        await _factory.SyncAssetsAsync();

        using var response = await _client.GetAsync(new Uri(ApiRoutes.Asset(asset.Id), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getAsset");
        var dto = await response.BodyAsync<AssetDto>();
        (dto.Status, dto.Progress, dto.SizeBytes).ShouldBe((AssetStatusDto.Ready, 100, (long)RenderedBytes.Length));
        dto.Files.Single().ShouldBe(new AssetFileDto(AssetFileRoleDto.Audio, "audio/flac", RenderedBytes.Length, RenderedSha, ApiRoutes.AssetFile(asset.Id, ApiRoutes.AssetFileAudio)));
        dto.Audio.ShouldBe(new AudioFactsDto { Format = AudioFileFormatDto.Flac, BitDepth = 16, DurationSeconds = 213.04, PeakDbfs = -0.3, AppliedGainDb = 0 });
        (await File.ReadAllBytesAsync(Path.Combine(_factory.DataDir, "assets", asset.Id.ToString(), "audio.flac"), Ct)).ShouldBe(RenderedBytes);
        Worker.RenderDeletes.ShouldBe([RenderJobId]);
    }

    [Fact]
    public async Task Sync_AHashMismatchFailsAndKeepsNoFile()
    {
        var asset = await CreateDrumsAsync();
        CompleteRenders(Worker, new string('0', 64));

        await _factory.SyncAssetsAsync();

        var dto = await GetAssetAsync(asset.Id);
        (dto.Status, dto.Error).ShouldBe((AssetStatusDto.Failed, AssetJobApplier.DamagedFile));
        Directory.Exists(Path.Combine(_factory.DataDir, "assets", asset.Id.ToString())).ShouldBeFalse();
    }

    [Theory]
    [InlineData(WorkerRenderJobState.Failed)]
    [InlineData(WorkerRenderJobState.Cancelled)]
    public async Task Sync_AFailedOrCancelledJobFailsWithTheWorkerError(WorkerRenderJobState state)
    {
        var asset = await CreateDrumsAsync();
        Worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, state));

        await _factory.SyncAssetsAsync();

        var dto = await GetAssetAsync(asset.Id);
        (dto.Status, dto.Error).ShouldBe((AssetStatusDto.Failed, "interrupted by restart"));
        await ContractAssert.MatchesAsync(await _client.GetAsync(new Uri(ApiRoutes.Asset(asset.Id), UriKind.Relative), Ct), "getAsset");
    }

    [Fact]
    public async Task Sync_AJobTheWorkerLostFails()
    {
        var asset = await CreateDrumsAsync();
        Worker.OnGetRender = _ => Task.FromResult<WorkerRenderJob?>(null);

        await _factory.SyncAssetsAsync();

        (await GetAssetAsync(asset.Id)).Error.ShouldBe(AssetJobApplier.LostJob);
    }

    [Fact]
    public async Task Sync_AnUnreachableWorkerKeepsTheState()
    {
        var asset = await CreateDrumsAsync();
        Worker.OnGetRender = _ => throw new WorkerUnavailableException();

        await _factory.SyncAssetsAsync();

        (await GetAssetAsync(asset.Id)).Status.ShouldBe(AssetStatusDto.Queued);
    }

    [Fact]
    public async Task Sync_ADownloadRejectionFails()
    {
        var asset = await CreateDrumsAsync();
        CompleteRenders(Worker);
        Worker.OnDownloadRender = (_, _) => throw new WorkerRequestException(HttpStatusCode.Conflict, "Render job is in state running.");

        await _factory.SyncAssetsAsync();

        (await GetAssetAsync(asset.Id)).Status.ShouldBe(AssetStatusDto.Failed);
    }

    [Fact]
    public async Task GetAsset_UnknownIs404()
    {
        using var response = await _client.GetAsync(new Uri(ApiRoutes.Asset(Guid.NewGuid()), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "getAsset");
    }

    [Fact]
    public async Task GetFile_ServesAnAttachmentWithRangeAndETag()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        var asset = await _factory.ReadyDrumsAsync(_client, track.Id);
        var url = ApiRoutes.AssetFile(asset.Id, ApiRoutes.AssetFileAudio);

        using var full = await _client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        using var partial = await SendAsync(HttpMethod.Get, url, request => request.Headers.Range = new RangeHeaderValue(0, 3));
        using var notModified = await SendAsync(HttpMethod.Get, url, request => request.Headers.IfNoneMatch.Add(full.Headers.ETag!));

        (await full.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(RenderedBytes);
        full.Content.Headers.ContentType!.MediaType.ShouldBe("audio/flac");
        full.Content.Headers.ContentDisposition!.ToString()
            .ShouldBe("attachment; filename=\"Never Gonna Give You Up - drums.flac\"; filename*=UTF-8''Never%20Gonna%20Give%20You%20Up%20-%20drums.flac");
        (partial.StatusCode, notModified.StatusCode).ShouldBe((HttpStatusCode.PartialContent, HttpStatusCode.NotModified));
        foreach (var response in new[] { full, partial, notModified })
        {
            await ContractAssert.MatchesAsync(response, "getAssetFile");
        }
    }

    [Fact]
    public async Task GetFile_InlineAndHead()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        var asset = await _factory.ReadyDrumsAsync(_client, track.Id);

        using var inline = await _client.GetAsync(new Uri(ApiRoutes.AssetFile(asset.Id, ApiRoutes.AssetFileAudio, inline: true), UriKind.Relative), Ct);
        using var head = await SendAsync(HttpMethod.Head, ApiRoutes.AssetFile(asset.Id, ApiRoutes.AssetFileAudio), _ => { });

        inline.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        head.StatusCode.ShouldBe(HttpStatusCode.OK);
        head.Content.Headers.ContentLength.ShouldBe(RenderedBytes.Length);
        (await head.Content.ReadAsByteArrayAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFile_NotReadyUnknownRoleAndUnknownItemAre404()
    {
        var asset = await CreateDrumsAsync();

        using var notReady = await _client.GetAsync(new Uri(ApiRoutes.AssetFile(asset.Id, ApiRoutes.AssetFileAudio), UriKind.Relative), Ct);
        using var role = await _client.GetAsync(new Uri(ApiRoutes.AssetFile(asset.Id, "pdf"), UriKind.Relative), Ct);
        using var unknown = await _client.GetAsync(new Uri(ApiRoutes.AssetFile(Guid.NewGuid(), ApiRoutes.AssetFileAudio), UriKind.Relative), Ct);

        foreach (var response in new[] { notReady, role, unknown })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await ContractAssert.MatchesAsync(notReady, "getAssetFile");
    }

    [Theory]
    [InlineData("Song - drums", "flac", "attachment; filename=\"Song - drums.flac\"; filename*=UTF-8''Song%20-%20drums.flac")]
    [InlineData("Ünï \"q\"", "mp3", "attachment; filename=\"_n_ _q_.mp3\"; filename*=UTF-8''%C3%9Cn%C3%AF%20_q_.mp3")]
    public void ContentDisposition_HasAnAsciiFallbackAndTheUtf8Name(string name, string extension, string expected)
    {
        ContentDisposition.Attachment(AssetNaming.DownloadFileName(name, extension), inline: false).ShouldBe(expected);
    }

    private async Task<AssetDto> CreateDrumsAsync()
    {
        var track = await _factory.SeedAsync(NamedTrack());
        Worker.OnCreateRender = _ => Task.FromResult(FakeWorkerClient.RenderJob(RenderJobId, WorkerRenderJobState.Queued));
        return await (await _client.PostExportAsync(Raw(track.Id, "drums"))).BodyAsync<AssetDto>();
    }

    private async Task<AssetDto> GetAssetAsync(Guid id) =>
        await (await _client.GetAsync(new Uri(ApiRoutes.Asset(id), UriKind.Relative), Ct)).BodyAsync<AssetDto>();

    private Task<AssetPage> ListAsync() =>
        _factory.WithScopeAsync(sp => sp.GetRequiredService<IAssetRepository>().ListAsync(new AssetQuery(), CancellationToken.None));

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Action<HttpRequestMessage> configure)
    {
        using var request = new HttpRequestMessage(method, path);
        configure(request);
        return await _client.SendAsync(request, Ct);
    }
}
