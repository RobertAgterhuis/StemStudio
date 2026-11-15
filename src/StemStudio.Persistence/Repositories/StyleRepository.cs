using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Styles;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of the style library (feature 009).</summary>
public interface IStyleRepository
{
    /// <summary>One style (tracked).</summary>
    /// <param name="id">Style ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The style or null.</returns>
    Task<Style?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Every style (not tracked); ordering is the caller's.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The styles.</returns>
    Task<IReadOnlyList<Style>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Every style name.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The names.</returns>
    Task<IReadOnlyList<string>> NamesAsync(CancellationToken cancellationToken);

    /// <summary>Whether another style has this normalised name.</summary>
    /// <param name="normalizedName">Upper-case trimmed name.</param>
    /// <param name="excludeStyleId">The style being edited, if any.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when taken.</returns>
    Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludeStyleId, CancellationToken cancellationToken);

    /// <summary>Inserts a style.</summary>
    /// <param name="style">The style.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">The name is taken.</exception>
    Task AddAsync(Style style, CancellationToken cancellationToken);

    /// <summary>Deletes a style.</summary>
    /// <param name="style">The style.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Style style, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">The name is taken.</exception>
    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Upserts the presets by id and deletes presets that are no longer in the resource (generations keep their
    /// snapshots); a user style that holds a preset name is renamed "&lt;name&gt; (copy)".
    /// </summary>
    /// <param name="presets">The presets of the resource.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The number of inserted, changed or deleted presets.</returns>
    Task<int> SeedPresetsAsync(IReadOnlyList<PresetDefinition> presets, DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IStyleRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class StyleRepository(StemStudioDbContext context) : IStyleRepository
{
    /// <inheritdoc />
    public Task<Style?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Styles.FirstOrDefaultAsync(style => style.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Style>> ListAsync(CancellationToken cancellationToken) =>
        await context.Styles.AsNoTracking().ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> NamesAsync(CancellationToken cancellationToken) =>
        await context.Styles.Select(style => style.Name).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludeStyleId, CancellationToken cancellationToken) =>
        context.Styles.AnyAsync(style => style.NormalizedName == normalizedName && style.Id != excludeStyleId, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Style style, CancellationToken cancellationToken)
    {
        context.Styles.Add(style);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Style style, CancellationToken cancellationToken)
    {
        context.Styles.Remove(style);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);

    /// <inheritdoc />
    public async Task<int> SeedPresetsAsync(IReadOnlyList<PresetDefinition> presets, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presets);
        var existing = await context.Styles.ToListAsync(cancellationToken);
        var changes = RemoveRetired(existing, presets) + presets.Sum(preset => Upsert(existing, preset, nowUtc));
        await UnitOfWork.SaveAsync(context, cancellationToken);
        return changes;
    }

    private int RemoveRetired(List<Style> existing, IReadOnlyList<PresetDefinition> presets)
    {
        var retired = existing.Where(style => style.IsPreset && presets.All(preset => preset.Id != style.Id)).ToList();
        context.Styles.RemoveRange(retired);
        existing.RemoveAll(retired.Contains);
        return retired.Count;
    }

    private int Upsert(List<Style> existing, PresetDefinition preset, DateTime nowUtc)
    {
        RenameClashingUserStyle(existing, preset, nowUtc);
        var row = existing.Find(style => style.Id == preset.Id);
        if (row is not null)
        {
            return row.ApplyPreset(preset, nowUtc) ? 1 : 0;
        }

        var style = Style.FromPreset(preset, nowUtc);
        context.Styles.Add(style);
        existing.Add(style);
        return 1;
    }

    private static void RenameClashingUserStyle(List<Style> existing, PresetDefinition preset, DateTime nowUtc)
    {
        var normalized = StyleNaming.Normalize(preset.Fields.Name);
        var clash = existing.Find(style => !style.IsPreset && style.NormalizedName == normalized);
        if (clash is not null)
        {
            clash.Update(clash.Fields with { Name = StyleNaming.CopyName(clash.Name, existing.Select(style => style.Name).Append(preset.Fields.Name)) }, nowUtc);
        }
    }
}
