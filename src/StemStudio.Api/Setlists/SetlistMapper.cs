using StemStudio.Contracts.Setlists;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Setlists;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Setlists;

/// <summary>What the setlist DTOs show of the library: the entries' tracks, mixes and section counts.</summary>
/// <param name="Tracks">Track per id.</param>
/// <param name="Mixes">Mix per id.</param>
/// <param name="SectionCounts">Sections per track id.</param>
public sealed record SetlistLibrary(IReadOnlyDictionary<Guid, Track> Tracks, IReadOnlyDictionary<Guid, Mix> Mixes, IReadOnlyDictionary<Guid, int> SectionCounts);

/// <summary>Maps setlists to the backend 1.13.0 contract.</summary>
public static class SetlistMapper
{
    /// <summary>The full setlist.</summary>
    /// <param name="setlist">The setlist.</param>
    /// <param name="library">Library facts of its entries.</param>
    /// <returns>The DTO.</returns>
    public static SetlistDto ToDto(Setlist setlist, SetlistLibrary library)
    {
        ArgumentNullException.ThrowIfNull(setlist);
        ArgumentNullException.ThrowIfNull(library);
        var entries = setlist.Entries.Where(entry => library.Tracks.ContainsKey(entry.TrackId)).Select(entry => ToDto(entry, library)).ToList();
        return new SetlistDto
        {
            Id = setlist.Id,
            Name = setlist.Name,
            Note = setlist.Note,
            Entries = entries,
            TotalSeconds = entries.Sum(entry => entry.DurationSeconds ?? 0),
            RemovedEntries = setlist.RemovedEntries,
            CreatedAt = Utc(setlist.CreatedAt),
            UpdatedAt = Utc(setlist.UpdatedAt),
        };
    }

    /// <summary>The list row of a setlist.</summary>
    /// <param name="setlist">The setlist.</param>
    /// <param name="durations">Duration per track id.</param>
    /// <returns>The DTO.</returns>
    public static SetlistSummaryDto ToSummary(Setlist setlist, IReadOnlyDictionary<Guid, double?> durations)
    {
        ArgumentNullException.ThrowIfNull(setlist);
        ArgumentNullException.ThrowIfNull(durations);
        return new SetlistSummaryDto
        {
            Id = setlist.Id,
            Name = setlist.Name,
            Songs = setlist.Entries.Count,
            TotalSeconds = setlist.Entries.Sum(entry => durations.GetValueOrDefault(entry.TrackId) ?? 0),
            UpdatedAt = Utc(setlist.UpdatedAt),
        };
    }

    private static SetlistEntryDto ToDto(SetlistEntry entry, SetlistLibrary library)
    {
        var track = library.Tracks[entry.TrackId];
        var mix = entry.MixId is { } mixId ? library.Mixes.GetValueOrDefault(mixId) : null;
        return new SetlistEntryDto
        {
            Id = entry.Id,
            Position = entry.Position,
            TrackId = entry.TrackId,
            TrackTitle = track.Title,
            Artist = track.Artist,
            MixId = mix?.Id,
            MixName = mix?.Name,
            Note = entry.Note,
            DurationSeconds = track.DurationSeconds,
            Bpm = track.BeatGrid?.Bpm,
            SectionCount = library.SectionCounts.GetValueOrDefault(entry.TrackId),
            HasLyrics = track.Lyrics?.HasText == true,
        };
    }

    private static DateTimeOffset Utc(DateTime value) => new(value, TimeSpan.Zero);
}
