using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Practice;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of the practice log (feature 014): the settings row and the sessions.</summary>
public interface IPracticeRepository
{
    /// <summary>The settings row (tracked; recreated with the defaults when missing).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The settings.</returns>
    Task<PracticeLogSettings> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>The number of stored sessions.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The count.</returns>
    Task<int> CountSessionsAsync(CancellationToken cancellationToken);

    /// <summary>One session with its section rows (tracked).</summary>
    /// <param name="id">Session id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The session, or null.</returns>
    Task<PracticeSession?> FindSessionAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Sessions newest first (not tracked).</summary>
    /// <param name="trackId">Only this track, or all.</param>
    /// <param name="before">Only sessions that started before this time, or all.</param>
    /// <param name="limit">Most sessions.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sessions.</returns>
    Task<IReadOnlyList<PracticeSession>> ListSessionsAsync(Guid? trackId, DateTime? before, int limit, CancellationToken cancellationToken);

    /// <summary>All sessions of a track (not tracked).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sessions.</returns>
    Task<IReadOnlyList<PracticeSession>> SessionsOfTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>All sessions (not tracked).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sessions.</returns>
    Task<IReadOnlyList<PracticeSession>> AllSessionsAsync(CancellationToken cancellationToken);

    /// <summary>Inserts a session and saves.</summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddSessionAsync(PracticeSession session, CancellationToken cancellationToken);

    /// <summary>Deletes one session and saves.</summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveSessionAsync(PracticeSession session, CancellationToken cancellationToken);

    /// <summary>Deletes the sessions of a track, or all.</summary>
    /// <param name="trackId">Track id, or null for all.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Deleted sessions.</returns>
    Task<int> DeleteSessionsAsync(Guid? trackId, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IPracticeRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class PracticeRepository(StemStudioDbContext context) : IPracticeRepository
{
    /// <inheritdoc />
    public async Task<PracticeLogSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await context.PracticeLogSettings.FirstOrDefaultAsync(row => row.Id == PracticeLogSettings.SingletonId, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = PracticeLogSettings.CreateDefault();
        context.PracticeLogSettings.Add(settings);
        await UnitOfWork.SaveAsync(context, cancellationToken);
        return settings;
    }

    /// <inheritdoc />
    public Task<int> CountSessionsAsync(CancellationToken cancellationToken) => context.PracticeSessions.CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<PracticeSession?> FindSessionAsync(Guid id, CancellationToken cancellationToken) =>
        context.PracticeSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PracticeSession>> ListSessionsAsync(Guid? trackId, DateTime? before, int limit, CancellationToken cancellationToken)
    {
        var query = context.PracticeSessions.AsNoTracking();
        query = trackId is { } track ? query.Where(session => session.TrackId == track) : query;
        query = before is { } time ? query.Where(session => session.StartedAt < time) : query;
        return await query.OrderByDescending(session => session.StartedAt).ThenBy(session => session.Id).Take(limit).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PracticeSession>> SessionsOfTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        await context.PracticeSessions.AsNoTracking().Where(session => session.TrackId == trackId).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PracticeSession>> AllSessionsAsync(CancellationToken cancellationToken) =>
        await context.PracticeSessions.AsNoTracking().ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddSessionAsync(PracticeSession session, CancellationToken cancellationToken)
    {
        context.PracticeSessions.Add(session);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveSessionAsync(PracticeSession session, CancellationToken cancellationToken)
    {
        context.PracticeSessions.Remove(session);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> DeleteSessionsAsync(Guid? trackId, CancellationToken cancellationToken) =>
        trackId is { } track
            ? context.PracticeSessions.Where(session => session.TrackId == track).ExecuteDeleteAsync(cancellationToken)
            : context.PracticeSessions.ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
