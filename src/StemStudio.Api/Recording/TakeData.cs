using StemStudio.Api.Assets;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Recording;

/// <summary>A take with its hidden library item.</summary>
/// <param name="Take">The take.</param>
/// <param name="Asset">Its item.</param>
public sealed record StoredTake(RecordingTake Take, Asset Asset);

/// <summary>The repositories of the recording services (one scope, one unit of work).</summary>
/// <param name="Tracks">Tracks.</param>
/// <param name="Takes">Takes.</param>
/// <param name="Assets">Library items.</param>
/// <param name="Mixes">Mixes.</param>
/// <param name="Calibrations">Calibrations.</param>
public sealed record TakeData(ITrackRepository Tracks, IRecordingTakeRepository Takes, IAssetRepository Assets, IMixRepository Mixes, IInputCalibrationRepository Calibrations)
{
    /// <summary>A take with its item, tracked; null when either is missing.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stored take or null.</returns>
    public async Task<StoredTake?> FindAsync(Guid takeId, CancellationToken cancellationToken)
    {
        var take = await Takes.FindAsync(takeId, cancellationToken);
        var asset = take is null ? null : await Assets.FindAsync(take.AssetId, cancellationToken);
        return asset is null ? null : new StoredTake(take!, asset);
    }
}

/// <summary>Writes and discards a take's capture file.</summary>
/// <param name="storage">Take files.</param>
/// <param name="files">Asset folders.</param>
public sealed class TakeCapture(TakeFiles storage, IAssetFileStore files)
{
    /// <summary>
    /// Writes <c>capture.wav</c> from the spooled data through a temporary file; an existing capture is replaced only by complete,
    /// audible audio (digital silence is never stored).
    /// </summary>
    /// <param name="assetId">The take's asset.</param>
    /// <param name="capture">The upload's format and start.</param>
    /// <param name="spoolPath">The spooled data.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The level facts.</returns>
    public async Task<PeakScan> WriteAsync(Guid assetId, PaddedCapture capture, string spoolPath, CancellationToken cancellationToken)
    {
        var target = storage.CapturePath(assetId);
        var temp = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            PeakScan scan;
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await using (var source = new FileStream(spoolPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                scan = await TakeWavPadder.WriteAsync(output, capture, source, cancellationToken);
            }

            if (!scan.AllZero)
            {
                File.Move(temp, target, overwrite: true);
            }

            return scan;
        }
        finally
        {
            TakeFiles.TryDelete(temp);
        }
    }

    /// <summary>Deletes the asset folder of a take that was not stored.</summary>
    /// <param name="assetId">The asset.</param>
    public void Discard(Guid assetId) => files.DeleteAll(assetId);
}
