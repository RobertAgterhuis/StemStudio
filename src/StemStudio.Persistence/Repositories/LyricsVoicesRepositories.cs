using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Voices;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of own lyrics sheets (feature 010).</summary>
public interface ILyricsSheetRepository
{
    /// <summary>One sheet (tracked).</summary>
    /// <param name="id">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sheet or null.</returns>
    Task<LyricsSheet?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The stored text of a sheet, read past the tracked entity (a running assist sees saves of other requests).</summary>
    /// <param name="id">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The text, or null when the sheet is gone.</returns>
    Task<string?> CurrentTextAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Sheets, newest change first; <paramref name="query"/> matches title and text ignoring case.</summary>
    /// <param name="query">Search text.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sheets.</returns>
    Task<IReadOnlyList<LyricsSheet>> ListAsync(string? query, CancellationToken cancellationToken);

    /// <summary>Inserts a sheet.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(LyricsSheet sheet, CancellationToken cancellationToken);

    /// <summary>Deletes a sheet.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(LyricsSheet sheet, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ILyricsSheetRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class LyricsSheetRepository(StemStudioDbContext context) : ILyricsSheetRepository
{
    /// <inheritdoc />
    public Task<LyricsSheet?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.LyricsSheets.FirstOrDefaultAsync(sheet => sheet.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<string?> CurrentTextAsync(Guid id, CancellationToken cancellationToken) =>
        context.LyricsSheets.AsNoTracking().Where(sheet => sheet.Id == id).Select(sheet => (string?)sheet.Text).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LyricsSheet>> ListAsync(string? query, CancellationToken cancellationToken)
    {
        var all = await context.LyricsSheets.OrderByDescending(sheet => sheet.UpdatedAt).ToListAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(query)
            ? all
            : all.Where(sheet => sheet.Title.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) || sheet.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(LyricsSheet sheet, CancellationToken cancellationToken)
    {
        context.LyricsSheets.Add(sheet);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(LyricsSheet sheet, CancellationToken cancellationToken)
    {
        context.LyricsSheets.Remove(sheet);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}

/// <summary>Persistence of voice profiles (feature 010).</summary>
public interface IVoiceProfileRepository
{
    /// <summary>One profile (tracked).</summary>
    /// <param name="id">Profile id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The profile or null.</returns>
    Task<VoiceProfile?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Every profile by display name.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The profiles.</returns>
    Task<IReadOnlyList<VoiceProfile>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Whether a profile has this normalised name.</summary>
    /// <param name="normalizedName">Upper-case trimmed name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when taken.</returns>
    Task<bool> IsNameTakenAsync(string normalizedName, CancellationToken cancellationToken);

    /// <summary>Inserts a profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">The name is taken.</exception>
    Task AddAsync(VoiceProfile profile, CancellationToken cancellationToken);

    /// <summary>Deletes a profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(VoiceProfile profile, CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IVoiceProfileRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class VoiceProfileRepository(StemStudioDbContext context) : IVoiceProfileRepository
{
    /// <inheritdoc />
    public Task<VoiceProfile?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.VoiceProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceProfile>> ListAsync(CancellationToken cancellationToken) =>
        await context.VoiceProfiles.OrderBy(profile => profile.NormalizedName).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsNameTakenAsync(string normalizedName, CancellationToken cancellationToken) =>
        context.VoiceProfiles.AnyAsync(profile => profile.NormalizedName == normalizedName, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(VoiceProfile profile, CancellationToken cancellationToken)
    {
        context.VoiceProfiles.Add(profile);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(VoiceProfile profile, CancellationToken cancellationToken)
    {
        context.VoiceProfiles.Remove(profile);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }
}

/// <summary>The index of file-byte hashes of stored audio (010 exact-copy guard).</summary>
public interface IAudioHashRepository
{
    /// <summary>Adds a hash; an existing hash is kept.</summary>
    /// <param name="hash">The hash.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(AudioHash hash, CancellationToken cancellationToken);

    /// <summary>Adds hashes; existing ones are kept.</summary>
    /// <param name="hashes">The hashes.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddRangeAsync(IEnumerable<AudioHash> hashes, CancellationToken cancellationToken);

    /// <summary>Whether a hash is stored.</summary>
    /// <param name="sha256">The hash.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when known.</returns>
    Task<bool> ContainsAsync(string sha256, CancellationToken cancellationToken);

    /// <summary>Removes the hashes of an owner.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IAudioHashRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class AudioHashRepository(StemStudioDbContext context) : IAudioHashRepository
{
    /// <inheritdoc />
    public Task AddAsync(AudioHash hash, CancellationToken cancellationToken) => AddRangeAsync([hash], cancellationToken);

    /// <inheritdoc />
    public async Task AddRangeAsync(IEnumerable<AudioHash> hashes, CancellationToken cancellationToken)
    {
        foreach (var hash in hashes.DistinctBy(hash => hash.Sha256))
        {
            if (!await context.AudioHashes.AnyAsync(stored => stored.Sha256 == hash.Sha256, cancellationToken))
            {
                context.AudioHashes.Add(hash);
            }
        }

        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ContainsAsync(string sha256, CancellationToken cancellationToken) =>
        context.AudioHashes.AnyAsync(hash => hash.Sha256 == sha256, cancellationToken);

    /// <inheritdoc />
    public Task RemoveOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        context.AudioHashes.Where(hash => hash.OwnerId == ownerId).ExecuteDeleteAsync(cancellationToken);
}
