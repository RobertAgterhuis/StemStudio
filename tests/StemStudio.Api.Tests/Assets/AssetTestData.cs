using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Assets;

/// <summary>Shared data and helpers of the feature 007 asset tests.</summary>
internal static class AssetTestData
{
    public const string RenderJobId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

    public static readonly byte[] RenderedBytes = "fLaC-rendered-audio"u8.ToArray();

    public static string RenderedSha => Convert.ToHexStringLower(SHA256.HashData(RenderedBytes));

    /// <summary>A completed six-stem track titled "Never Gonna Give You Up" by Rick Astley.</summary>
    public static Track NamedTrack(string videoId = "dQw4w9WgXcQ")
    {
        var track = SyncTestData.UrlTrack(videoId);
        track.ApplyMetadata(new SourceMetadata("Never Gonna Give You Up", "Rick Astley"), SyncTestData.T0);
        track.Complete(SyncTestData.Completion(), SyncTestData.T0);
        return track;
    }

    public static CreateAudioExportRequest Raw(Guid trackId, params string[] channels) => new()
    {
        TrackId = trackId,
        Mode = ExportModeDto.Raw,
        Channels = channels,
        Format = new ExportFormatDto(AudioFileFormatDto.Flac),
    };

    public static WorkerRenderResult Result(string sha) => new("output.flac", "audio/flac", RenderedBytes.Length, sha, 213.04, 44100, 2, -0.3, 0);

    /// <summary>The worker reports the render job completed with <see cref="RenderedBytes"/>.</summary>
    public static void CompleteRenders(FakeWorkerClient worker, string? sha = null)
    {
        worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Completed, 1, Result(sha ?? RenderedSha)));
        worker.OnDownloadRender = (_, stream) => stream.WriteAsync(RenderedBytes).AsTask();
    }

    /// <summary>The worker reports every render job completed with the given bytes (feature 015 takes).</summary>
    public static void CompleteRenders(FakeWorkerClient worker, string sha, byte[] bytes)
    {
        worker.OnGetRender = id => Task.FromResult<WorkerRenderJob?>(FakeWorkerClient.RenderJob(id, WorkerRenderJobState.Completed, 1, Result(sha) with { SizeBytes = bytes.Length }));
        worker.OnDownloadRender = (_, stream) => stream.WriteAsync(bytes).AsTask();
    }

    public static Task<HttpResponseMessage> PostExportAsync(this HttpClient client, CreateAudioExportRequest request) =>
        client.PostAsJsonAsync(new Uri(ApiRoutes.AudioExports, UriKind.Relative), request, TestContext.Current.CancellationToken);

    public static Task<bool> SyncAssetsAsync(this ApiFactory factory) => factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<AssetSynchronizer>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    /// <summary>Creates a raw drums export and syncs it to ready.</summary>
    public static async Task<AssetDto> ReadyDrumsAsync(this ApiFactory factory, HttpClient client, Guid trackId)
    {
        var asset = await (await client.PostExportAsync(Raw(trackId, "drums"))).BodyAsync<AssetDto>();
        CompleteRenders(factory.Worker);
        await factory.SyncAssetsAsync();
        return asset;
    }
}

/// <summary>Disk space reported by a test.</summary>
internal sealed class FakeDiskSpace : IDiskSpace
{
    public long Available { get; set; } = long.MaxValue;

    public long AvailableBytes(string path) => Available;
}
