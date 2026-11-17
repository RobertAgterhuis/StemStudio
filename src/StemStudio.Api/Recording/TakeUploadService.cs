using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Recording;

/// <summary>
/// <c>createRecordingTake</c> (data-model 2.1-2.4): checks the track and the take limit, streams the upload, validates the
/// WAV and the metadata, writes the padded <c>capture.wav</c>, creates the hidden asset (origin recording) and the take and
/// starts the render. Count check, insert and render start run under one <see cref="TakeLock"/>.
/// </summary>
/// <param name="data">Repositories.</param>
/// <param name="reader">Upload reader.</param>
/// <param name="disk">Free-space check.</param>
/// <param name="renderer">Starts the render.</param>
/// <param name="gate">The take lock.</param>
/// <param name="capture">Capture files.</param>
public sealed class TakeUploadService(TakeData data, TakeUploadReader reader, TakeDiskGuard disk, TakeRenderer renderer, TakeLock gate, TakeCapture capture)
{
    /// <summary>Longest stored track title.</summary>
    public const int MaxTrackTitleLength = 300;

    /// <summary>Stores an uploaded take.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="request">The multipart request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202 or a problem.</returns>
    public async Task<IResult> CreateAsync(Guid trackId, HttpRequest request, CancellationToken cancellationToken)
    {
        var track = await data.Tracks.FindAsync(trackId, cancellationToken);
        if (await AdmitAsync(track, trackId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var assetId = Guid.NewGuid();
        var (upload, problem) = await reader.ReadAsync<RecordingTakeUploadDto>(request, assetId, disk.Admit, cancellationToken);
        if (upload is null)
        {
            capture.Discard(assetId);
            return problem!;
        }

        try
        {
            return await StoreAsync(track!, assetId, upload, cancellationToken);
        }
        finally
        {
            TakeFiles.TryDelete(upload.SpoolPath);
        }
    }

    /// <summary>The track problem: 404, 409 <c>track-not-ready</c> or 409 <c>take-limit-reached</c>.</summary>
    private async Task<IResult?> AdmitAsync(Track? track, Guid trackId, CancellationToken cancellationToken)
    {
        if (track is null)
        {
            return Tracks.TrackService.NotFound(trackId);
        }

        if (track.Status != TrackStatus.Completed)
        {
            return RecordingProblems.TrackNotReady();
        }

        return TakeRules.CanAdd((await data.Takes.ListByTrackAsync(trackId, cancellationToken)).Count) ? null : RecordingProblems.Refused(TakeRefusal.LimitReached);
    }

    private async Task<IResult> StoreAsync(Track track, Guid assetId, TakeUpload<RecordingTakeUploadDto> upload, CancellationToken cancellationToken)
    {
        if (TakeUploadRules.Validate(upload.Metadata, upload.Header) is { Count: > 0 } invalid)
        {
            capture.Discard(assetId);
            return AssetProblems.Invalid(invalid, "metadata");
        }

        return await gate.RunAsync(() => StoreLockedAsync(track, new TakeDraft(assetId, upload), cancellationToken), cancellationToken);
    }

    /// <summary>Count, number, capture, rows and render start under the take lock.</summary>
    private async Task<IResult> StoreLockedAsync(Track track, TakeDraft draft, CancellationToken cancellationToken)
    {
        var existing = await data.Takes.ListByTrackAsync(track.Id, cancellationToken);
        var (metadata, header) = (draft.Upload.Metadata, draft.Upload.Header);
        var check = new TakeCheck(existing.Count, metadata.Speed, metadata.TransposeSemitones, metadata.StartSample, metadata.LengthSamples, header.Frames, LatencyConversion.TrackSamples(track.DurationSeconds ?? 0));
        var refusal = TakeRules.Check(check);
        var scan = refusal == TakeRefusal.None ? await capture.WriteAsync(draft.AssetId, new PaddedCapture(header, metadata.StartSample), draft.Upload.SpoolPath, cancellationToken) : null;
        if (scan is not { AllZero: false })
        {
            capture.Discard(draft.AssetId);
            return refusal == TakeRefusal.None ? RecordingProblems.Silent() : RecordingProblems.Refused(refusal);
        }

        var numbered = draft with { Number = TakeNaming.NextNumber(existing.Select(take => take.Number)), Scan = scan };
        return await StartAsync(track, await ValuesAsync(track.Id, numbered, cancellationToken), cancellationToken);
    }

    private async Task<NewTake> ValuesAsync(Guid trackId, TakeDraft draft, CancellationToken cancellationToken)
    {
        var (metadata, header) = (draft.Upload.Metadata, draft.Upload.Header);
        return new NewTake
        {
            TrackId = trackId,
            AssetId = draft.AssetId,
            Number = draft.Number,
            Name = metadata.Name,
            Part = TakeUploadRules.ToDomain(metadata.Part),
            SessionId = metadata.SessionId,
            PassIndex = metadata.PassIndex,
            Partial = metadata.Partial,
            StartSample = metadata.StartSample,
            LengthSamples = metadata.LengthSamples,
            InputMode = TakeUploadRules.ToDomain(metadata.InputMode),
            InputChannels = header.Channels,
            LatencySamples = metadata.LatencySamples,
            LatencyMethod = TakeUploadRules.ToDomain(metadata.LatencyMethod),
            CalibrationId = await KnownCalibrationAsync(metadata.CalibrationId, cancellationToken),
            DeviceLabel = metadata.DeviceLabel,
            MixId = await MixOfTrackAsync(metadata.MixId, trackId, cancellationToken),
            Scan = draft.Scan!,
            Dropouts = metadata.Dropouts,
            RecordedAt = metadata.RecordedAt.UtcDateTime,
        };
    }

    /// <summary>Informational links that no longer exist are dropped (a take never fails over them).</summary>
    private async Task<Guid?> KnownCalibrationAsync(Guid? calibrationId, CancellationToken cancellationToken) =>
        calibrationId is { } id && await data.Calibrations.FindAsync(id, cancellationToken) is not null ? id : null;

    private async Task<Guid?> MixOfTrackAsync(Guid? mixId, Guid trackId, CancellationToken cancellationToken) =>
        mixId is { } id && await data.Mixes.FindAsync(id, cancellationToken) is { } mix && mix.TrackId == trackId ? id : null;

    private async Task<IResult> StartAsync(Track track, NewTake values, CancellationToken cancellationToken)
    {
        var takeId = Guid.NewGuid();
        var take = RecordingTake.Create(takeId, values, renderer.Now);
        var asset = Asset.CreateRecording(values.AssetId, take.Name, Provenance(track, take), renderer.Now);
        if (await renderer.StartAsync(asset, cancellationToken) is { } unavailable)
        {
            capture.Discard(asset.Id);
            return unavailable;
        }

        data.Assets.Add(asset);
        data.Takes.Add(take);
        await data.Takes.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.RecordingTakeOf(take.Id), TakeMapper.ToDto(take, asset));
    }

    /// <summary>The provenance of a take's asset (data-model 2.2).</summary>
    /// <param name="track">The track.</param>
    /// <param name="take">The take.</param>
    /// <returns>The provenance.</returns>
    public static Provenance Provenance(Track track, RecordingTake take)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(take);
        return new Provenance
        {
            Origin = AssetOrigin.Recording,
            TrackId = track.Id,
            TrackTitle = track.Title.Length <= MaxTrackTitleLength ? track.Title : track.Title[..MaxTrackTitleLength],
            TrackArtist = track.Artist,
            Contents = TakeNaming.Contents(take.Number, take.Part),
            Recording = new RecordingSource(take.Id),
        };
    }
}

/// <summary>A take being stored: its new asset id, the upload and, once known, its number and level facts.</summary>
/// <param name="AssetId">The new asset.</param>
/// <param name="Upload">The read upload.</param>
public sealed record TakeDraft(Guid AssetId, TakeUpload<RecordingTakeUploadDto> Upload)
{
    /// <summary>The take number.</summary>
    public int Number { get; init; }

    /// <summary>The level facts of the written capture.</summary>
    public PeakScan? Scan { get; init; }
}
