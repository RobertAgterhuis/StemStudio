using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Generations;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of generations (features 009 and 010).</summary>
public interface IGenerationRepository
{
    /// <summary>One generation (tracked).</summary>
    /// <param name="id">Generation ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The generation or null.</returns>
    Task<Generation?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Generations, newest first, optionally filtered (tracked).</summary>
    /// <param name="kind">Kind filter.</param>
    /// <param name="sourceTrackId">Source track filter.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The generations.</returns>
    Task<IReadOnlyList<Generation>> ListAsync(GenerationKind? kind, Guid? sourceTrackId, CancellationToken cancellationToken);

    /// <summary>Generations that are not terminal (for the sync service).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The active generations.</returns>
    Task<IReadOnlyList<Generation>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>Generations made from a track (tracked), for track deletion.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The generations.</returns>
    Task<IReadOnlyList<Generation>> ListBySourceTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Inserts a generation.</summary>
    /// <param name="generation">The generation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(Generation generation, CancellationToken cancellationToken);

    /// <summary>Deletes a generation (with its takes).</summary>
    /// <param name="generation">The generation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Generation generation, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IGenerationRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class GenerationRepository(StemStudioDbContext context) : IGenerationRepository
{
    private static readonly GenerationState[] Terminal = [GenerationState.Completed, GenerationState.Failed, GenerationState.Cancelled];

    /// <inheritdoc />
    public Task<Generation?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Generations.FirstOrDefaultAsync(generation => generation.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Generation>> ListAsync(GenerationKind? kind, Guid? sourceTrackId, CancellationToken cancellationToken) =>
        await context.Generations
            .Where(generation => (kind == null || generation.Kind == kind) && (sourceTrackId == null || generation.SourceTrackId == sourceTrackId))
            .OrderByDescending(generation => generation.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Generation>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.Generations.Where(generation => !Terminal.Contains(generation.Status)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Generation>> ListBySourceTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        await context.Generations.Where(generation => generation.SourceTrackId == trackId).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Generation generation, CancellationToken cancellationToken)
    {
        context.Generations.Add(generation);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Generation generation, CancellationToken cancellationToken)
    {
        context.Generations.Remove(generation);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
