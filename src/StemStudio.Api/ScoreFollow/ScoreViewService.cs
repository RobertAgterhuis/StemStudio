using StemStudio.Api.Assets;
using StemStudio.Api.Scores;
using StemStudio.Api.SongSections;
using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Scores;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.ScoreFollow;

/// <summary>One score view submission at a time (check + submit + store under one lock).</summary>
public sealed class ScoreViewGate : IDisposable
{
    /// <summary>The lock.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => Lock.Dispose();
}

/// <summary>Score list per track and score views (014 FR-009, FR-014, data-model 3.5).</summary>
/// <param name="tracks">Tracks.</param>
/// <param name="views">Score views and score items.</param>
/// <param name="worker">The worker practice lane.</param>
/// <param name="gate">The submission lock.</param>
/// <param name="clock">Clock.</param>
public sealed class ScoreViewService(ITrackRepository tracks, IScoreViewRepository views, IWorkerPracticeClient worker, ScoreViewGate gate, TimeProvider clock)
{
    /// <summary>Failure text when the score has no tempo grid to place its bars.</summary>
    public const string NoGrid = "The score has no tempo grid, so its bars cannot follow the playback.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary><c>GET /api/tracks/{trackId}/scores</c>: ready score items of the track (directly or through an exported audio item).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 with the list (newest first) or 404.</returns>
    public async Task<IResult> ListAsync(Guid trackId, CancellationToken cancellationToken)
    {
        if (await tracks.FindAsync(trackId, cancellationToken) is not { } track)
        {
            return TrackService.NotFound(trackId);
        }

        var scores = await views.ReadyScoresAsync(cancellationToken);
        var audio = await views.AssetsAsync(scores.Select(AudioIdOf).OfType<Guid>().ToList(), cancellationToken);
        var own = scores.Select(score => new ScoreSources(score, track, AudioIdOf(score) is { } id ? audio.GetValueOrDefault(id) : null))
            .Where(sources => ScoreViewPlanner.TrackOf(sources) == trackId).ToList();
        var stored = await views.FindManyAsync(own.Select(sources => sources.Score.Id).ToList(), cancellationToken);
        return TypedResults.Ok(own.Select(sources => ToDto(sources, stored.GetValueOrDefault(sources.Score.Id))).ToList());
    }

    /// <summary><c>GET /api/assets/{assetId}/score-view</c>.</summary>
    /// <param name="assetId">Score item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 409 <c>not-a-score</c>.</returns>
    public async Task<IResult> GetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var (sources, problem) = await SourcesAsync(assetId, requireReady: false, cancellationToken);
        return problem ?? TypedResults.Ok(ScoreFollowMapper.ToDto(assetId, await views.FindAsync(assetId, cancellationToken), ScoreViewPlanner.Plan(sources!)));
    }

    /// <summary><c>POST /api/assets/{assetId}/score-view</c>: queues the view; idempotent while queued, rendering or ready.</summary>
    /// <param name="assetId">Score item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public async Task<IResult> CreateAsync(Guid assetId, CancellationToken cancellationToken)
    {
        await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            var (sources, problem) = await SourcesAsync(assetId, requireReady: true, cancellationToken);
            return problem ?? await CreateLockedAsync(sources!, cancellationToken);
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    /// <summary>Queues the view of a score item that has just become ready (the change is saved with the item).</summary>
    /// <param name="score">The ready score item (tracked).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task QueueNewAsync(Asset score, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(score);
        if (score.Kind != AssetKind.Score || await views.FindAsync(score.Id, cancellationToken) is not null)
        {
            return;
        }

        var sources = await LoadSourcesAsync(score, cancellationToken);
        if (await SubmitAsync(sources, null, cancellationToken) is { } view)
        {
            views.Add(view);
        }
    }

    /// <summary><c>GET /api/assets/{assetId}/score-view/pages/{page}</c>: the SVG for an <c>&lt;img&gt;</c>.</summary>
    /// <param name="assetId">Score item.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="files">Stored pages.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file, 404 or 409 <c>score-view-not-ready</c>.</returns>
    public async Task<IResult> PageAsync(Guid assetId, int page, ScoreViewFiles files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        var (_, problem) = await SourcesAsync(assetId, requireReady: false, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var view = await views.FindAsync(assetId, cancellationToken);
        if (view is not { Status: ScoreViewStatus.Ready })
        {
            return ScoreFollowProblems.ViewNotReady();
        }

        return page >= 1 && page <= view.PageCount && files.Find(assetId, page) is { } path
            ? TypedResults.PhysicalFile(path, "image/svg+xml")
            : ApiProblems.NotFound($"Page {page} of the score view does not exist.");
    }

    private async Task<IResult> CreateLockedAsync(ScoreSources sources, CancellationToken cancellationToken)
    {
        var stored = await views.FindAsync(sources.Score.Id, cancellationToken);
        if (stored is { Status: not ScoreViewStatus.Failed })
        {
            return TypedResults.Accepted((string?)null, ScoreFollowMapper.ToDto(sources.Score.Id, stored, null));
        }

        if (await SubmitAsync(sources, stored, cancellationToken) is not { } view)
        {
            return PracticeFlowProblems.WorkerUnavailable();
        }

        await (stored is null ? views.AddAsync(view, cancellationToken) : views.SaveAsync(cancellationToken));
        return TypedResults.Accepted((string?)null, ScoreFollowMapper.ToDto(view.AssetId, view, null));
    }

    /// <summary>Submits the job; a score without grid fails at once. Null when the worker cannot take it.</summary>
    private async Task<ScoreView?> SubmitAsync(ScoreSources sources, ScoreView? stored, CancellationToken cancellationToken)
    {
        var plan = ScoreViewPlanner.Plan(sources);
        var view = stored ?? ScoreView.Create(sources.Score.Id, plan?.TrackId ?? ScoreViewPlanner.TrackOf(sources), plan?.OffsetSeconds ?? 0, Now);
        if (plan is not { Grid: { } grid })
        {
            view.Fail(NoGrid, Now);
            return view;
        }

        try
        {
            var job = await worker.CreateScoreViewJobAsync(new WorkerScoreViewJobRequest { Musicxml = plan.MusicXmlPath, Grid = ScoreSubmitter.WorkerGrid(grid), DurationSeconds = plan.DurationSeconds }, cancellationToken);
            view.Submit(job.JobId, Now);
            return view;
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return null;
        }
    }

    private async Task<(ScoreSources? Sources, IResult? Problem)> SourcesAsync(Guid assetId, bool requireReady, CancellationToken cancellationToken)
    {
        var found = await views.AssetsAsync([assetId], cancellationToken);
        if (!found.TryGetValue(assetId, out var asset))
        {
            return (null, AssetProblems.NotFound(assetId));
        }

        if (asset.Kind != AssetKind.Score)
        {
            return (null, ScoreFollowProblems.NotAScore());
        }

        return requireReady && asset.Status != AssetStatus.Ready ? (null, ScoreFollowProblems.ScoreNotReady()) : (await LoadSourcesAsync(asset, cancellationToken), null);
    }

    private async Task<ScoreSources> LoadSourcesAsync(Asset score, CancellationToken cancellationToken)
    {
        var audio = AudioIdOf(score) is { } audioId ? (await views.AssetsAsync([audioId], cancellationToken)).GetValueOrDefault(audioId) : null;
        var trackId = ScoreViewPlanner.TrackOf(new ScoreSources(score, null, audio));
        var track = trackId is { } id ? await tracks.FindAsync(id, cancellationToken) : null;
        return new ScoreSources(score, track, audio);
    }

    private static Guid? AudioIdOf(Asset score) => score.Provenance.ScoreRecipe?.Source.AssetId;

    private static TrackScoreDto ToDto(ScoreSources sources, ScoreView? view)
    {
        var recipe = sources.Score.Provenance.ScoreRecipe;
        var plan = ScoreViewPlanner.Plan(sources);
        var partial = recipe?.Range is not null || sources.Audio?.Provenance.Recipe?.Range is not null;
        return new TrackScoreDto
        {
            AssetId = sources.Score.Id,
            Name = sources.Score.Name,
            Profile = ScoreFollowMapper.ProfileOf(recipe?.Profile ?? sources.Score.Score?.Profile ?? ScoreProfile.Vocals),
            Source = recipe?.Source.Channel ?? "asset",
            RangeStartSeconds = partial ? plan?.OffsetSeconds : null,
            RangeEndSeconds = partial ? plan?.EndSeconds : null,
            ManualTempo = recipe?.Tempo is not null,
            CreatedAt = new DateTimeOffset(sources.Score.CreatedAt, TimeSpan.Zero),
            ViewStatus = ScoreFollowMapper.StatusOf(view),
        };
    }
}

/// <summary>409 problems of the score view (backend 1.13.0 <c>ScoreViewConflict</c>).</summary>
public static class ScoreFollowProblems
{
    /// <summary>409 <c>not-a-score</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult NotAScore() =>
        PracticeFlowProblems.Conflict(PracticeFlowProblemTypes.NotAScore, "This item is not sheet music", "Only sheet-music items have a score view.");

    /// <summary>409 <c>score-not-ready</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult ScoreNotReady() =>
        PracticeFlowProblems.Conflict(PracticeFlowProblemTypes.ScoreNotReady, "The sheet music is not ready", "Wait until the sheet music has been made.");

    /// <summary>409 <c>score-view-not-ready</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult ViewNotReady() =>
        PracticeFlowProblems.Conflict(PracticeFlowProblemTypes.ScoreViewNotReady, "The score view is not ready yet", "Wait until the score view has been prepared.");
}
