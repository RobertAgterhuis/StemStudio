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
}

/// <summary>EF Core implementation of <see cref="IPracticeNoteRepository"/>.</summary>
/// <param name="context">Database context.</param>
public sealed class PracticeNoteRepository(StemStudioDbContext context) : IPracticeNoteRepository
{
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
}
