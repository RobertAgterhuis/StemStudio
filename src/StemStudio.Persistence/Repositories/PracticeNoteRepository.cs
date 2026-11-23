using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Handouts;

namespace StemStudio.Persistence.Repositories;

/// <summary>Storage of <see cref="PracticeNote"/> rows (feature 020; one unit of work per scope).</summary>
public interface IPracticeNoteRepository
{
    /// <summary>The notes of a track in family order.</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The notes.</returns>
    Task<IReadOnlyList<PracticeNote>> ListAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>One note (tracked).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="family">Family.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The note or null.</returns>
    Task<PracticeNote?> FindAsync(Guid trackId, HandoutFamily family, CancellationToken cancellationToken);

    /// <summary>Adds a note (saved by <see cref="SaveAsync"/>).</summary>
    /// <param name="note">The note.</param>
    void Add(PracticeNote note);

    /// <summary>Removes a note (saved by <see cref="SaveAsync"/>).</summary>
    /// <param name="note">The note.</param>
    void Remove(PracticeNote note);

    /// <summary>Saves the changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores a note's text and time (upsert): inserts it, or updates the stored note of its track and family, and saves. A
    /// concurrent first write or delete of the same note is retried, so two requests never fail on the key.
    /// </summary>
    /// <param name="note">The wanted note (not tracked).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stored note.</returns>
    Task<PracticeNote> SetAsync(PracticeNote note, CancellationToken cancellationToken);

    /// <summary>Deletes the note of a track and family if there is one (idempotent, one statement).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="family">Family.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid trackId, HandoutFamily family, CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IPracticeNoteRepository"/>.</summary>
/// <param name="context">Database context.</param>
public sealed class PracticeNoteRepository(StemStudioDbContext context) : IPracticeNoteRepository
{
    /// <summary>Attempts of <see cref="SetAsync"/> before a key or concurrency conflict is passed on.</summary>
    public const int MaxSetAttempts = 3;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PracticeNote>> ListAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var notes = await context.PracticeNotes.AsNoTracking().Where(note => note.TrackId == trackId).ToListAsync(cancellationToken);
        return notes.OrderBy(note => note.Family).ToList();
    }

    /// <inheritdoc />
    public Task<PracticeNote?> FindAsync(Guid trackId, HandoutFamily family, CancellationToken cancellationToken) =>
        context.PracticeNotes.FirstOrDefaultAsync(note => note.TrackId == trackId && note.Family == family, cancellationToken);

    /// <inheritdoc />
    public void Add(PracticeNote note) => context.PracticeNotes.Add(note);

    /// <inheritdoc />
    public void Remove(PracticeNote note) => context.PracticeNotes.Remove(note);

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PracticeNote> SetAsync(PracticeNote note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(note);
        for (var attempt = 1; ; attempt++)
        {
            var stored = await FindAsync(note.TrackId, note.Family, cancellationToken);
            if (stored is null)
            {
                context.PracticeNotes.Add(note);
            }
            else
            {
                stored.Update(note.Text, note.UpdatedAt);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return stored ?? note;
            }
            catch (DbUpdateException conflict) when (attempt < MaxSetAttempts)
            {
                // Another request inserted or deleted the note between the read and the write: read it again.
                foreach (var entry in conflict.Entries)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid trackId, HandoutFamily family, CancellationToken cancellationToken) =>
        context.PracticeNotes.Where(note => note.TrackId == trackId && note.Family == family).ExecuteDeleteAsync(cancellationToken);
}
