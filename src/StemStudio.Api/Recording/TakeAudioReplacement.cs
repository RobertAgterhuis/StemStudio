using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Recording;

/// <summary>
/// <c>replaceRecordingTakeAudio</c> (US7, data-model 2.4): the engine's re-encoded span with the adjusted start replaces
/// <c>capture.wav</c> and is rendered again; the previous <c>audio.flac</c> stays until the new one is ready (and after a
/// failed render). The take's revision counts up and its adjustment is summed.
/// </summary>
/// <param name="data">Repositories.</param>
/// <param name="reader">Upload reader.</param>
/// <param name="disk">Free-space check.</param>
/// <param name="renderer">Starts the render.</param>
/// <param name="gate">The take lock.</param>
/// <param name="capture">Capture files.</param>
public sealed class TakeAudioReplacement(TakeData data, TakeUploadReader reader, TakeDiskGuard disk, TakeRenderer renderer, TakeLock gate, TakeCapture capture)
{
    /// <summary>Stores the take again.</summary>
    /// <param name="takeId">The take.</param>
    /// <param name="request">The multipart request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202 or a problem.</returns>
    public async Task<IResult> ReplaceAsync(Guid takeId, HttpRequest request, CancellationToken cancellationToken)
    {
        var stored = await data.FindAsync(takeId, cancellationToken);
        if (stored is null || stored.Asset.IsActive)
        {
            return stored is null ? RecordingProblems.TakeNotFound(takeId) : RecordingProblems.Saving();
        }

        var (upload, problem) = await reader.ReadAsync<ReplaceTakeAudioMetadataDto>(request, stored.Asset.Id, disk.Admit, cancellationToken);
        if (upload is null)
        {
            return problem!;
        }

        try
        {
            return await CheckAsync(stored, upload, cancellationToken) ?? await gate.RunAsync(() => StoreLockedAsync(stored, upload, cancellationToken), cancellationToken);
        }
        finally
        {
            TakeFiles.TryDelete(upload.SpoolPath);
        }
    }

    /// <summary>422 for invalid metadata, a length that differs from the WAV or a span outside the track; else null.</summary>
    private async Task<IResult?> CheckAsync(StoredTake stored, TakeUpload<ReplaceTakeAudioMetadataDto> upload, CancellationToken cancellationToken)
    {
        var metadata = upload.Metadata;
        if (TakeUploadRules.Validate(metadata) is { Count: > 0 } invalid)
        {
            return AssetProblems.Invalid(invalid, "metadata");
        }

        var track = await data.Tracks.FindAsync(stored.Take.TrackId, cancellationToken);
        var trackSamples = LatencyConversion.TrackSamples(track?.DurationSeconds ?? 0);
        var refusal = TakeRules.Check(new TakeCheck(0, 1, 0, metadata.StartSample, metadata.LengthSamples, upload.Header.Frames, trackSamples));
        return refusal == TakeRefusal.None ? null : RecordingProblems.Refused(refusal);
    }

    private async Task<IResult> StoreLockedAsync(StoredTake stored, TakeUpload<ReplaceTakeAudioMetadataDto> upload, CancellationToken cancellationToken)
    {
        var (take, asset, metadata) = (stored.Take, stored.Asset, upload.Metadata);
        if (asset.IsActive)
        {
            return RecordingProblems.Saving();
        }

        var scan = await capture.WriteAsync(asset.Id, new PaddedCapture(upload.Header, metadata.StartSample), upload.SpoolPath, cancellationToken);
        if (scan.AllZero)
        {
            return RecordingProblems.Silent();
        }

        asset.Rerender(renderer.Now);
        if (await renderer.StartAsync(asset, cancellationToken) is { } unavailable)
        {
            return unavailable;
        }

        take.Realign(metadata.StartSample, metadata.LengthSamples, metadata.AdjustmentSamples, scan, renderer.Now);
        await data.Takes.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.RecordingTakeOf(take.Id), TakeMapper.ToDto(take, asset));
    }
}
