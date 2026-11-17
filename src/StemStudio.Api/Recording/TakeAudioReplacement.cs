using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Assets;
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
        var take = await data.Takes.FindAsync(takeId, cancellationToken);
        var asset = take is null ? null : await data.Assets.FindAsync(take.AssetId, cancellationToken);
        if (take is null || asset is null)
        {
            return RecordingProblems.TakeNotFound(takeId);
        }

        if (asset.IsActive)
        {
            return RecordingProblems.Saving();
        }

        var (upload, problem) = await reader.ReadAsync<ReplaceTakeAudioMetadataDto>(request, asset.Id, disk.Admit, cancellationToken);
        if (upload is null)
        {
            return problem!;
        }

        try
        {
            return await StoreAsync(take, asset, upload, cancellationToken);
        }
        finally
        {
            TakeFiles.TryDelete(upload.SpoolPath);
        }
    }

    private async Task<IResult> StoreAsync(RecordingTake take, Asset asset, TakeUpload<ReplaceTakeAudioMetadataDto> upload, CancellationToken cancellationToken)
    {
        var metadata = upload.Metadata;
        if (TakeUploadRules.Validate(metadata) is { Count: > 0 } invalid)
        {
            return AssetProblems.Invalid(invalid, "metadata");
        }

        var track = await data.Tracks.FindAsync(take.TrackId, cancellationToken);
        var trackSamples = LatencyConversion.TrackSamples(track?.DurationSeconds ?? 0);
        var refusal = TakeRules.Check(new TakeCheck(0, 1, 0, metadata.StartSample, metadata.LengthSamples, upload.Header.Frames, trackSamples));
        if (refusal != TakeRefusal.None)
        {
            return RecordingProblems.Refused(refusal);
        }

        using var held = await gate.EnterAsync(cancellationToken);
        if (asset.IsActive)
        {
            return RecordingProblems.Saving();
        }

        var scan = await capture.WriteAsync(asset.Id, upload.Header, metadata.StartSample, upload.SpoolPath, cancellationToken);
        return scan.AllZero ? RecordingProblems.Silent() : await RenderAsync(take, asset, metadata, scan, cancellationToken);
    }

    private async Task<IResult> RenderAsync(RecordingTake take, Asset asset, ReplaceTakeAudioMetadataDto metadata, PeakScan scan, CancellationToken cancellationToken)
    {
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
