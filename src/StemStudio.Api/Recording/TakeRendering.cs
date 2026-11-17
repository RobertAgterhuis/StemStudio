using StemStudio.Api.Agent.Stores;
using Microsoft.Extensions.Options;
using StemStudio.Api.Assets;
using StemStudio.Api.Storage;
using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Recording;

/// <summary>
/// Starts the 007 render job that turns a take's padded <c>capture.wav</c> into <c>audio.flac</c> (DELTA.md 2): one input at
/// gain 1, pan 0, master 1, FLAC 24-bit without dither.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="clock">Clock.</param>
public sealed class TakeRenderer(IWorkerClient worker, TimeProvider clock)
{
    /// <summary>Current time (UTC).</summary>
    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The worker request of a take's asset.</summary>
    /// <param name="asset">The take's asset.</param>
    /// <returns>The request.</returns>
    public static WorkerRenderJobRequest Request(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return new WorkerRenderJobRequest
        {
            Inputs = [new WorkerRenderInput(TakeFiles.CaptureInput(asset.Id), 1, 0)],
            MasterGain = 1,
            Output = new WorkerRenderOutput(WorkerRenderFormat.Flac) { BitDepth = RecordingConstants.BitDepth, Dither = false },
            Metadata = new WorkerRenderMetadata(Tag(asset.Provenance.TrackTitle), Tag(asset.Provenance.TrackArtist), Tag(asset.Provenance.Contents)),
        };
    }

    /// <summary>Starts the render and records the job; null on success, else 503 (worker unreachable or refusing).</summary>
    /// <param name="asset">A queued take asset.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Null or the problem.</returns>
    public async Task<IResult?> StartAsync(Asset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        try
        {
            asset.AssignJob((await worker.CreateRenderJobAsync(Request(asset), cancellationToken)).RenderJobId, Now);
            return null;
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return ApiProblems.Unavailable();
        }
    }

    private static string? Tag(string? value) => value is null || value.Length <= AssetSubmitter.MaxTagLength ? value : value[..AssetSubmitter.MaxTagLength];
}

/// <summary>Free space for a take: at least <c>MinFreeDiskBytes</c> plus twice the upload (padded WAV and FLAC, data-model 2.1).</summary>
/// <param name="disk">Free space source.</param>
/// <param name="data">The data directory.</param>
/// <param name="options">API options.</param>
public sealed class TakeDiskGuard(IDiskSpace disk, DataDirectory data, IOptions<StemStudioApiOptions> options)
{
    /// <summary>Null when the take fits, else 409 <c>insufficient-disk-space</c>.</summary>
    /// <param name="header">The upload's WAV header.</param>
    /// <returns>Null or the problem.</returns>
    public IResult? Admit(TakeWavHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        return disk.AvailableBytes(data.Root) >= options.Value.MinFreeDiskBytes + (2 * header.DataBytes) ? null : RecordingProblems.InsufficientDiskSpace();
    }
}

/// <summary>
/// One process-wide lock around "check the take count and number, store the take, start its render" (LESSONS backend: a
/// database check alone lets two concurrent uploads both pass).
/// </summary>
public sealed class TakeLock : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Runs the body while holding the lock.</summary>
    /// <typeparam name="T">Result of the body.</typeparam>
    /// <param name="body">The body.</param>
    /// <param name="cancellationToken">Cancellation of the wait.</param>
    /// <returns>The body's result.</returns>
    public Task<T> RunAsync<T>(Func<Task<T>> body, CancellationToken cancellationToken) => _gate.RunExclusiveAsync(body, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
