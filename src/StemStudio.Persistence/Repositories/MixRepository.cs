using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Mixes;

namespace StemStudio.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IMixRepository"/>.</summary>
/// <param name="context">The scoped context.</param>
public sealed class MixRepository(StemStudioDbContext context) : IMixRepository
{
    /// <inheritdoc />
    public async Task<Mix?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Mixes.FirstOrDefaultAsync(mix => mix.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MixSummary>> ListAsync(Guid? trackId, CancellationToken cancellationToken)
    {
        var mixes = context.Mixes.AsNoTracking().Where(mix => trackId == null || mix.TrackId == trackId);
        return await mixes
            .Join(context.Tracks, mix => mix.TrackId, track => track.Id, (mix, track) => new { mix, track.Title })
            .OrderByDescending(row => row.mix.UpdatedAt)
            .Select(row => new MixSummary(row.mix.Id, row.mix.TrackId, row.Title, row.mix.Name, row.mix.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> NamesForTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        await context.Mixes.Where(mix => mix.TrackId == trackId).Select(mix => mix.Name).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsNameTakenAsync(Guid trackId, string normalizedName, Guid? excludeMixId, CancellationToken cancellationToken) =>
        context.Mixes.AnyAsync(
            mix => mix.TrackId == trackId && mix.NormalizedName == normalizedName && mix.Id != excludeMixId,
            cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Mix mix, CancellationToken cancellationToken)
    {
        context.Mixes.Add(mix);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Mix mix, CancellationToken cancellationToken)
    {
        context.Mixes.Remove(mix);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
