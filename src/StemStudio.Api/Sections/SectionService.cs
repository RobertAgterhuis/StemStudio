using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Sections;
using StemStudio.Domain;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.SongSections;

/// <summary>One detection start at a time: the check, the submission and the save run under this lock (LESSONS backend).</summary>
public sealed class SectionDetectionGate : IDisposable
{
    /// <summary>The lock.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => Lock.Dispose();
}

/// <summary>Reads, replaces and (re-)detects the sections of a track (014 FR-001, FR-006, FR-008).</summary>
/// <param name="tracks">Tracks.</param>
/// <param name="sections">Section sets.</param>
/// <param name="worker">The worker practice lane.</param>
/// <param name="gate">The detection lock.</param>
/// <param name="clock">Clock.</param>
public sealed class SectionService(ITrackRepository tracks, ISectionRepository sections, IWorkerPracticeClient worker, SectionDetectionGate gate, TimeProvider clock)
{
    /// <summary>Start of the failure text when the worker refuses the section job (4xx); the worker's detail follows.</summary>
    public const string SubmitRejected = "The processing service could not start finding the sections:";

    private const int MaxDetailInputs = 30;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary><c>GET /api/tracks/{trackId}/sections</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 with the set (status <c>none</c> when never detected) or 404.</returns>
    public async Task<IResult> GetAsync(Guid trackId, CancellationToken cancellationToken)
    {
        if (await tracks.FindAsync(trackId, cancellationToken) is not { } track)
        {
            return TrackService.NotFound(trackId);
        }

        return TypedResults.Ok(SectionMapper.ToDto(track, await sections.FindAsync(trackId, cancellationToken)));
    }

    /// <summary><c>PUT /api/tracks/{trackId}/sections</c>: replaces the list (origin <c>edited</c>).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="request">Revision and sections.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 <c>sections-changed</c> or 422.</returns>
    public async Task<IResult> ReplaceAsync(Guid trackId, SectionsUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await tracks.FindAsync(trackId, cancellationToken) is not { } track)
        {
            return TrackService.NotFound(trackId);
        }

        var set = await sections.FindAsync(trackId, cancellationToken);
        var isNew = set is null;
        set ??= TrackSections.Create(trackId, Now);
        if (Replace(set, track, request) is { } problem)
        {
            return problem;
        }

        await (isNew ? sections.AddAsync(set, cancellationToken) : sections.SaveAsync(cancellationToken));
        return TypedResults.Ok(SectionMapper.ToDto(track, set));
    }

    /// <summary><c>POST /api/tracks/{trackId}/sections/detect</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="overwrite">Replace edited sections.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202 with the queued set, 404, 409 or 503.</returns>
    public async Task<IResult> DetectAsync(Guid trackId, bool overwrite, CancellationToken cancellationToken)
    {
        await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            if (await tracks.FindAsync(trackId, cancellationToken) is not { } track)
            {
                return TrackService.NotFound(trackId);
            }

            return await DetectLockedAsync(track, overwrite, cancellationToken);
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    /// <summary>
    /// The automatic start after a separation: only when the set is missing or <c>none</c>. An unreachable worker leaves <c>none</c>
    /// (the mixer triggers it again); a refused job (4xx) stores <c>failed</c> with the reason, so the UI offers a retry.
    /// </summary>
    /// <param name="track">The completed track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when the set was stored (queued, or failed after a refusal).</returns>
    public async Task<bool> DetectAutomaticallyAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            var set = await sections.FindAsync(track.Id, cancellationToken);
            return set is null or { Status: SectionSetStatus.None } && track.Status == TrackStatus.Completed
                && await SubmitAsync(track, set, cancellationToken) is null;
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    /// <summary>The section job request of a track: its stems, kept detail stems, grid and length.</summary>
    /// <param name="track">A completed track.</param>
    /// <returns>The request.</returns>
    public static WorkerSectionJobRequest JobRequest(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var details = SectionChannels.Of(track).Where(channel => channel.Contains('.', StringComparison.Ordinal)).Order(StringComparer.Ordinal)
            .Select(channel => new WorkerDetailPath(channel, RenderChannel.Parse(channel)!.PathFor(track.Id))).Take(MaxDetailInputs).ToList();
        return new WorkerSectionJobRequest
        {
            Stems = track.Stems.ToDictionary(stem => stem.Name, stem => new RenderChannel(stem.Name, null).PathFor(track.Id), StringComparer.Ordinal),
            Details = details,
            Grid = track.BeatGrid,
            DurationSeconds = track.DurationSeconds ?? 0,
        };
    }

    private IResult? Replace(TrackSections set, Track track, SectionsUpdateRequest request)
    {
        try
        {
            var update = new SectionSetUpdate(request.Revision, (request.Sections ?? []).Select(SectionMapper.ToInput).ToList());
            set.Replace(update, new SectionContext(track.DurationSeconds ?? 0, SectionChannels.Of(track)), Guid.NewGuid, Now);
            return null;
        }
        catch (SectionsChangedException)
        {
            return PracticeFlowProblems.SectionsChanged();
        }
        catch (DomainValidationException invalid)
        {
            return PracticeFlowProblems.Invalid(invalid.Errors);
        }
    }

    private async Task<IResult> DetectLockedAsync(Track track, bool overwrite, CancellationToken cancellationToken)
    {
        var set = await sections.FindAsync(track.Id, cancellationToken);
        var refusal = track.Status != TrackStatus.Completed ? PracticeFlowProblems.TrackNotReady() : Refusal(set?.CheckDetect(overwrite));
        return refusal ?? await SubmitAsync(track, set, cancellationToken) ?? TypedResults.Accepted((string?)null, SectionMapper.ToDto(track, await sections.FindAsync(track.Id, cancellationToken)));
    }

    private static IResult? Refusal(DetectRefusal? refusal) => refusal switch
    {
        DetectRefusal.DetectionRunning => PracticeFlowProblems.DetectionRunning(),
        DetectRefusal.SectionsEdited => PracticeFlowProblems.SectionsEdited(),
        _ => null,
    };

    /// <summary>Submits the job: null when queued or refused (stored as <c>failed</c>), 503 when the worker cannot be reached.</summary>
    private async Task<IResult?> SubmitAsync(Track track, TrackSections? set, CancellationToken cancellationToken)
    {
        var stored = set ?? TrackSections.Create(track.Id, Now);
        try
        {
            await StartAsync(stored, track, cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            return PracticeFlowProblems.WorkerUnavailable();
        }

        await (set is null ? sections.AddAsync(stored, cancellationToken) : sections.SaveAsync(cancellationToken));
        return null;
    }

    /// <summary>Queues the job, or fails the set with the reason when the worker refuses it (4xx).</summary>
    private async Task StartAsync(TrackSections stored, Track track, CancellationToken cancellationToken)
    {
        try
        {
            stored.StartDetection((await worker.CreateSectionJobAsync(JobRequest(track), cancellationToken)).JobId, Now);
        }
        catch (WorkerRequestException rejected)
        {
            stored.Fail($"{SubmitRejected} {rejected.Detail}", Now);
        }
    }
}
