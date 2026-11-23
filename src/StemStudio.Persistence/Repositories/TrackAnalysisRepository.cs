using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Persistence.Repositories;

/// <summary>Status of the three analyses of a track, without their documents.</summary>
/// <param name="TrackId">The track.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Status">The status.</param>
public sealed record AnalysisStatusRow(Guid TrackId, AnalysisKind Kind, AnalysisStatus Status);

/// <summary>Persistence of the track analyses (feature 016).</summary>
public interface ITrackAnalysisRepository
{
    /// <summary>One analysis of a track (tracked).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The analysis, or null when never created.</returns>
    Task<TrackAnalysis?> FindAsync(Guid trackId, AnalysisKind kind, CancellationToken cancellationToken);

    /// <summary>The analyses that are queued or running (tracked), oldest change first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The analyses.</returns>
    Task<IReadOnlyList<TrackAnalysis>> ActiveAsync(CancellationToken cancellationToken);

    /// <summary>The stored worker job of an analysis, read from the database (not from tracked entities).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job id, or null.</returns>
    Task<string?> CurrentJobIdAsync(Guid trackId, AnalysisKind kind, CancellationToken cancellationToken);

    /// <summary>The statuses of the given tracks' analyses (no documents loaded).</summary>
    /// <param name="trackIds">Tracks.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>One row per stored analysis.</returns>
    Task<IReadOnlyList<AnalysisStatusRow>> StatusesAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    /// <summary>The ready chord analyses of the given tracks (for the key summary), untracked.</summary>
    /// <param name="trackIds">Tracks.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Analysis per track (tracks without ready chords are missing).</returns>
    Task<IReadOnlyDictionary<Guid, TrackAnalysis>> ReadyChordsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    /// <summary>Inserts an analysis and saves.</summary>
    /// <param name="analysis">The analysis.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(TrackAnalysis analysis, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ITrackAnalysisRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class TrackAnalysisRepository(StemStudioDbContext context) : ITrackAnalysisRepository
{
    private static readonly AnalysisStatus[] Active = [AnalysisStatus.Queued, AnalysisStatus.Running];

    /// <inheritdoc />
    public Task<TrackAnalysis?> FindAsync(Guid trackId, AnalysisKind kind, CancellationToken cancellationToken) =>
        context.TrackAnalyses.FirstOrDefaultAsync(analysis => analysis.TrackId == trackId && analysis.Kind == kind, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackAnalysis>> ActiveAsync(CancellationToken cancellationToken) =>
        await context.TrackAnalyses.Where(analysis => Active.Contains(analysis.Status)).OrderBy(analysis => analysis.UpdatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<string?> CurrentJobIdAsync(Guid trackId, AnalysisKind kind, CancellationToken cancellationToken) =>
        context.TrackAnalyses.AsNoTracking().Where(analysis => analysis.TrackId == trackId && analysis.Kind == kind)
            .Select(analysis => analysis.JobId).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnalysisStatusRow>> StatusesAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken) =>
        await context.TrackAnalyses.Where(analysis => trackIds.Contains(analysis.TrackId))
            .Select(analysis => new AnalysisStatusRow(analysis.TrackId, analysis.Kind, analysis.Status)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, TrackAnalysis>> ReadyChordsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken) =>
        await context.TrackAnalyses.AsNoTracking()
            .Where(analysis => trackIds.Contains(analysis.TrackId) && analysis.Kind == AnalysisKind.Chords && analysis.Status == AnalysisStatus.Ready)
            .ToDictionaryAsync(analysis => analysis.TrackId, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(TrackAnalysis analysis, CancellationToken cancellationToken)
    {
        context.TrackAnalyses.Add(analysis);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
