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
    public async Task<IResult> ListAsync(Guid trackId, CancellationToken cancellationToken)
    {
        if (await tracks.FindAsync(trackId, cancellationToken) is null)
        {
            return TrackService.NotFound(trackId);
        }

        var stored = await notes.ListAsync(trackId, cancellationToken);
        return TypedResults.Ok(new PracticeNotesDto { Notes = stored.Select(ToDto).ToList() });
    }

    /// <summary><c>PUT /api/tracks/{trackId}/practice-notes/{family}</c>: control characters (except line breaks) are removed; an empty text deletes.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="family">Family wire value.</param>
    /// <param name="request">The text.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 204, 404 or 422.</returns>
    public async Task<IResult> SetAsync(Guid trackId, string family, SetPracticeNoteRequest request, CancellationToken cancellationToken)
    {
        if (!FamilyNames.TryGetValue(family ?? string.Empty, out var parsed))
        {
            return HandoutProblems.Field("family", "Must be one of overview, vocals, guitar, bass, keys, drums, melodic.");
        }

        if (request?.Text is not { Length: <= PracticeNoteRules.MaxLength } text)
        {
            return HandoutProblems.Field("text", $"Enter at most {ContractLimits.MaxPracticeNoteLength} characters.");
        }

        if (await tracks.FindAsync(trackId, cancellationToken) is null)
        {
            return TrackService.NotFound(trackId);
        }

        return await StoreAsync(trackId, parsed, PracticeNoteRules.Clean(text), cancellationToken);
    }

    private async Task<IResult> StoreAsync(Guid trackId, HandoutFamily family, string clean, CancellationToken cancellationToken)
    {
        if (clean.Length == 0)
        {
            await notes.DeleteAsync(trackId, family, cancellationToken);
            return TypedResults.NoContent();
        }

        var note = await notes.SetAsync(PracticeNote.Create(trackId, family, clean, clock.GetUtcNow().UtcDateTime), cancellationToken);
        return TypedResults.Ok(ToDto(note));
    }

    private static PracticeNoteDto ToDto(PracticeNote note) =>
        new() { Family = HandoutMapper.Convert<HandoutFamilyDto>(note.Family), Text = note.Text, UpdatedAt = TrackMapper.Utc(note.UpdatedAt) };
}
