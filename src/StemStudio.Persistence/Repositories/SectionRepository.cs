using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Sections;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of section sets (feature 014).</summary>
public interface ISectionRepository
{
    /// <summary>The section set of a track with its sections (tracked).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The set, or null when never created.</returns>
    Task<TrackSections?> FindAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>The sets with a queued or running detection, with their sections (tracked).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sets.</returns>
    Task<IReadOnlyList<TrackSections>> ActiveAsync(CancellationToken cancellationToken);

    /// <summary>Section counts per track.</summary>
    /// <param name="trackIds">Tracks.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Count per track (tracks without sections are missing).</returns>
    Task<IReadOnlyDictionary<Guid, int>> CountsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    /// <summary>Inserts a set and saves.</summary>
    /// <param name="sections">The set.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(TrackSections sections, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ISectionRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class SectionRepository(StemStudioDbContext context) : ISectionRepository
{
    private static readonly SectionSetStatus[] Active = [SectionSetStatus.Queued, SectionSetStatus.Detecting];

    /// <inheritdoc />
    public Task<TrackSections?> FindAsync(Guid trackId, CancellationToken cancellationToken) =>
        context.TrackSections.Include(set => set.Sections).AsSplitQuery().FirstOrDefaultAsync(set => set.TrackId == trackId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackSections>> ActiveAsync(CancellationToken cancellationToken) =>
        await context.TrackSections.Include(set => set.Sections).AsSplitQuery().Where(set => Active.Contains(set.Status)).OrderBy(set => set.UpdatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken) =>
        await context.Sections.Where(section => trackIds.Contains(section.TrackId)).GroupBy(section => section.TrackId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(TrackSections sections, CancellationToken cancellationToken)
    {
        context.TrackSections.Add(sections);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
