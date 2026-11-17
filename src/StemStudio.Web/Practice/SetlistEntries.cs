using StemStudio.Contracts;
using StemStudio.Contracts.Setlists;
using StemStudio.Web.Services;

namespace StemStudio.Web.Practice;

/// <summary>Entry list edits of the setlist editor (014 US5, FR-015/FR-016): every edit gives the full ordered input list for <c>replaceSetlistEntries</c>.</summary>
public static class SetlistEntries
{
    /// <summary>Most entries of a setlist.</summary>
    public const int MaxEntries = ContractLimits.MaxSetlistEntries;

    /// <summary>Longest entry note.</summary>
    public const int MaxNote = ContractLimits.MaxEntryNote;

    /// <summary>Longest setlist note.</summary>
    public const int MaxSetlistNote = ContractLimits.MaxSetlistNote;

    /// <summary>The inputs of the current entries.</summary>
    /// <param name="entries">The entries in order.</param>
    /// <returns>The inputs.</returns>
    public static List<SetlistEntryInputDto> Inputs(IReadOnlyList<SetlistEntryDto> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return [.. entries.Select(e => new SetlistEntryInputDto { Id = e.Id, TrackId = e.TrackId, MixId = e.MixId, Note = e.Note })];
    }

    /// <summary>Moves an entry (drag and drop, up/down).</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="from">Index to move.</param>
    /// <param name="to">Target index (clamped).</param>
    /// <returns>The new inputs, or null when nothing moves.</returns>
    public static List<SetlistEntryInputDto>? Move(IReadOnlyList<SetlistEntryDto> entries, int from, int to)
    {
        var list = Inputs(entries);
        to = Math.Clamp(to, 0, list.Count - 1);
        if (from < 0 || from >= list.Count || from == to)
        {
            return null;
        }

        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        return list;
    }

    /// <summary>Removes an entry.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="entryId">The entry.</param>
    /// <returns>The new inputs.</returns>
    public static List<SetlistEntryInputDto> Remove(IReadOnlyList<SetlistEntryDto> entries, Guid entryId) =>
        [.. Inputs(entries).Where(e => e.Id != entryId)];

    /// <summary>Appends a track with an optional mix.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="trackId">Track.</param>
    /// <param name="mixId">Mix of that track, or null.</param>
    /// <returns>The new inputs, or null at the 100-entry limit.</returns>
    public static List<SetlistEntryInputDto>? Add(IReadOnlyList<SetlistEntryDto> entries, Guid trackId, Guid? mixId)
    {
        var list = Inputs(entries);
        if (list.Count >= MaxEntries)
        {
            return null;
        }

        list.Add(new SetlistEntryInputDto { TrackId = trackId, MixId = mixId });
        return list;
    }

    /// <summary>Changes the note or the mix of an entry.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="entryId">The entry.</param>
    /// <param name="change">The change.</param>
    /// <returns>The new inputs.</returns>
    public static List<SetlistEntryInputDto> Change(IReadOnlyList<SetlistEntryDto> entries, Guid entryId, Func<SetlistEntryInputDto, SetlistEntryInputDto> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return [.. Inputs(entries).Select(e => e.Id == entryId ? change(e) : e)];
    }

    /// <summary>The total length text ("7:46", or "-" for unknown).</summary>
    /// <param name="seconds">Total seconds.</param>
    /// <returns>The text.</returns>
    public static string Total(double seconds) => TimeText.Duration(seconds);

    /// <summary>The route of setlist play mode at a 1-based position.</summary>
    /// <param name="setlistId">Setlist.</param>
    /// <param name="position">1-based position.</param>
    /// <returns>The route.</returns>
    public static string PlayRoute(Guid setlistId, int position) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"setlists/{setlistId}/play/{position}");
}
