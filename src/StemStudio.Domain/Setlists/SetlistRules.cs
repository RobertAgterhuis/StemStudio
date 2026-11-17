using System.Globalization;

namespace StemStudio.Domain.Setlists;

/// <summary>One entry as the user sends it (<c>PUT entries</c>).</summary>
/// <param name="Id">Id of a kept entry, or null.</param>
/// <param name="TrackId">The song.</param>
/// <param name="MixId">Optional mix of that song.</param>
/// <param name="Note">Note (0..1,000 characters).</param>
public sealed record SetlistEntryInput(Guid? Id, Guid TrackId, Guid? MixId, string? Note);

/// <summary>What the entry rules need from the library.</summary>
/// <param name="Tracks">Ids of the existing tracks.</param>
/// <param name="MixTracks">Track id per existing mix.</param>
public sealed record SetlistEntryContext(IReadOnlySet<Guid> Tracks, IReadOnlyDictionary<Guid, Guid> MixTracks);

/// <summary>Limits and validation of setlists (014 FR-015, data-model 3.6).</summary>
public static class SetlistRules
{
    /// <summary>Longest name after trimming.</summary>
    public const int MaxNameLength = 80;

    /// <summary>Longest setlist note.</summary>
    public const int MaxNoteLength = 2000;

    /// <summary>Most entries.</summary>
    public const int MaxEntries = 100;

    /// <summary>Longest entry note.</summary>
    public const int MaxEntryNoteLength = 1000;

    /// <summary>Field error code: the track does not exist.</summary>
    public const string TrackNotFound = "track-not-found";

    /// <summary>Field error code: the mix belongs to another track.</summary>
    public const string MixTrackMismatch = "mix-track-mismatch";

    /// <summary>Checks the entry list.</summary>
    /// <param name="entries">The ordered entries.</param>
    /// <param name="context">The library.</param>
    /// <returns>The violations.</returns>
    public static IReadOnlyList<DomainError> ValidateEntries(IReadOnlyList<SetlistEntryInput> entries, SetlistEntryContext context)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(context);
        var errors = new ValidationErrors();
        errors.Check(entries.Count <= MaxEntries, "entries", $"A setlist can have at most {MaxEntries} songs.");
        for (var i = 0; i < entries.Count; i++)
        {
            ValidateEntry(errors.Nested(string.Create(CultureInfo.InvariantCulture, $"entries[{i}]")), entries[i], context);
        }

        return errors.Errors;
    }

    /// <summary>Checks a name and a note; null means unchanged.</summary>
    /// <param name="errors">Collector.</param>
    /// <param name="name">Trimmed name, or null.</param>
    /// <param name="note">Note, or null.</param>
    internal static void ValidateHeader(ValidationErrors errors, string? name, string? note)
    {
        errors.Check(name is null || name.Length is >= 1 and <= MaxNameLength, "name", $"The name needs 1 to {MaxNameLength} characters.");
        errors.Check(note is null || note.Length <= MaxNoteLength, "note", $"The note may have at most {MaxNoteLength} characters.");
    }

    /// <summary>A trimmed note (empty for null).</summary>
    /// <param name="note">Raw note.</param>
    /// <returns>The note.</returns>
    internal static string CleanNote(string? note) => note?.Trim() ?? string.Empty;

    private static void ValidateEntry(ValidationErrors errors, SetlistEntryInput entry, SetlistEntryContext context)
    {
        errors.Check(context.Tracks.Contains(entry.TrackId), "trackId", $"The song is not in the library ({TrackNotFound}).");
        if (entry.MixId is { } mixId)
        {
            var belongs = context.MixTracks.TryGetValue(mixId, out var owner) && owner == entry.TrackId;
            errors.Check(belongs, "mixId", $"The mix does not belong to this song ({MixTrackMismatch}).");
        }

        errors.Check(CleanNote(entry.Note).Length <= MaxEntryNoteLength, "note", $"The note may have at most {MaxEntryNoteLength} characters.");
    }
}
