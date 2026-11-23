#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Handouts;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Handouts;

/// <summary>Reads and sets the practice notes of a track (FR-015, data-model 5.2): one note per family, empty text deletes it.</summary>
/// <param name="tracks">Tracks.</param>
/// <param name="notes">Practice notes.</param>
/// <param name="clock">Clock.</param>
public sealed class PracticeNoteService(ITrackRepository tracks, IPracticeNoteRepository notes, TimeProvider clock)
{
    private static readonly Dictionary<string, HandoutFamily> FamilyNames = Enum.GetValues<HandoutFamily>().ToDictionary(value => IngredientPlanner.Wire(value), StringComparer.Ordinal);

    /// <summary><c>GET /api/tracks/{trackId}/practice-notes</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public Task<IResult> ListAsync(Guid trackId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary><c>PUT /api/tracks/{trackId}/practice-notes/{family}</c>: control characters (except line breaks) are removed; an empty text deletes.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="family">Family wire value.</param>
    /// <param name="request">The text.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 204, 404 or 422.</returns>
    public Task<IResult> SetAsync(Guid trackId, string family, SetPracticeNoteRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task<IResult> StoreAsync(Guid trackId, HandoutFamily family, string clean, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await notes.FindAsync(trackId, family, cancellationToken);
        if (clean.Length == 0)
        {
            if (existing is not null)
            {
                notes.Remove(existing);
                await notes.SaveAsync(cancellationToken);
            }

            return TypedResults.NoContent();
        }

        var note = existing ?? PracticeNote.Create(trackId, family, clean, now);
        if (existing is null)
        {
            notes.Add(note);
        }
        else
        {
            note.Update(clean, now);
        }

        await notes.SaveAsync(cancellationToken);
        return TypedResults.Ok(ToDto(note));
    }

    private static PracticeNoteDto ToDto(PracticeNote note) =>
        new() { Family = HandoutMapper.Convert<HandoutFamilyDto>(note.Family), Text = note.Text, UpdatedAt = TrackMapper.Utc(note.UpdatedAt) };
}
