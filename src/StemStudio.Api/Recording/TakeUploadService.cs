using StemStudio.Api.Assets;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Recording;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Recording;

/// <summary>The repositories of the recording services (one scope, one unit of work).</summary>
/// <param name="Tracks">Tracks.</param>
/// <param name="Takes">Takes.</param>
/// <param name="Assets">Library items.</param>
/// <param name="Mixes">Mixes.</param>
/// <param name="Calibrations">Calibrations.</param>
public sealed record TakeData(ITrackRepository Tracks, IRecordingTakeRepository Takes, IAssetRepository Assets, IMixRepository Mixes, IInputCalibrationRepository Calibrations);

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
    /// <param name="header">The upload's format.</param>
    /// <param name="startSample">Song sample of the first take sample.</param>
    /// <param name="spoolPath">The spooled data.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The level facts.</returns>
    public async Task<PeakScan> WriteAsync(Guid assetId, TakeWavHeader header, long startSample, string spoolPath, CancellationToken cancellationToken)
    {
        var target = storage.CapturePath(assetId);
        var temp = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            PeakScan scan;
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await using (var source = new FileStream(spoolPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                scan = await TakeWavPadder.WriteAsync(output, header, startSample, source, cancellationToken);
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

        using var held = await gate.EnterAsync(cancellationToken);
        var existing = await data.Takes.ListByTrackAsync(track.Id, cancellationToken);
        var metadata = upload.Metadata;
        var check = new TakeCheck(existing.Count, metadata.Speed, metadata.TransposeSemitones, metadata.StartSample, metadata.LengthSamples, upload.Header.Frames, LatencyConversion.TrackSamples(track.DurationSeconds ?? 0));
        var refusal = TakeRules.Check(check);
        var scan = refusal == TakeRefusal.None ? await capture.WriteAsync(assetId, upload.Header, metadata.StartSample, upload.SpoolPath, cancellationToken) : null;
        if (refusal != TakeRefusal.None || scan!.AllZero)
        {
            capture.Discard(assetId);
            return refusal == TakeRefusal.None ? RecordingProblems.Silent() : RecordingProblems.Refused(refusal);
        }

        var values = await ValuesAsync(track, assetId, TakeNaming.NextNumber(existing.Select(take => take.Number)), upload, scan, cancellationToken);
        return await StartAsync(track, values, cancellationToken);
    }

    private async Task<NewTake> ValuesAsync(Track track, Guid assetId, int number, TakeUpload<RecordingTakeUploadDto> upload, PeakScan scan, CancellationToken cancellationToken)
    {
        var metadata = upload.Metadata;
        return new NewTake
        {
            TrackId = track.Id,
            AssetId = assetId,
            Number = number,
            Name = metadata.Name,
            Part = TakeUploadRules.ToDomain(metadata.Part),
            SessionId = metadata.SessionId,
            PassIndex = metadata.PassIndex,
            Partial = metadata.Partial,
            StartSample = metadata.StartSample,
            LengthSamples = metadata.LengthSamples,
            InputMode = TakeUploadRules.ToDomain(metadata.InputMode),
            InputChannels = upload.Header.Channels,
            LatencySamples = metadata.LatencySamples,
            LatencyMethod = TakeUploadRules.ToDomain(metadata.LatencyMethod),
            CalibrationId = await KnownCalibrationAsync(metadata.CalibrationId, cancellationToken),
            DeviceLabel = metadata.DeviceLabel,
            MixId = await MixOfTrackAsync(metadata.MixId, track.Id, cancellationToken),
            Scan = scan,
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
