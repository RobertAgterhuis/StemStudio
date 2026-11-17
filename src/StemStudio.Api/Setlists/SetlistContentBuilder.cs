using StemStudio.Api.SongSections;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Setlists;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Setlists;

namespace StemStudio.Api.Setlists;

/// <summary>A setlist with what its PDF prints: library facts and the section set per track.</summary>
/// <param name="Setlist">The setlist (at least one entry).</param>
/// <param name="Library">Its tracks and mixes.</param>
/// <param name="Sections">Section set per track id (missing = none).</param>
public sealed record SetlistSource(Setlist Setlist, SetlistLibrary Library, IReadOnlyDictionary<Guid, TrackSections> Sections);

/// <summary>
/// Collects the content of a setlist PDF (014 FR-018, data-model 2.6): per song the facts, the mix name, the note, the
/// section starts and the lyrics as plain lines (at most 10,000 characters per song, then <c>shortened</c>).
/// </summary>
public static class SetlistContentBuilder
{
    /// <summary>Most lyrics characters per song.</summary>
    public const int MaxLyricsCharacters = 10_000;

    private const int MaxTitleLength = 200;

    /// <summary>The worker request of a setlist.</summary>
    /// <param name="source">The setlist, its library facts and section sets.</param>
    /// <param name="options">Print options.</param>
    /// <param name="generatedAt">Printed in the footer.</param>
    /// <returns>The request.</returns>
    public static WorkerSetlistDocumentRequest Build(SetlistSource source, SetlistPdfOptions options, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        var songs = source.Setlist.Entries.Where(entry => source.Library.Tracks.ContainsKey(entry.TrackId))
            .Select((entry, index) => Song(entry, index + 1, source)).ToList();
        return new WorkerSetlistDocumentRequest
        {
            Title = source.Setlist.Name,
            Note = source.Setlist.Note,
            GeneratedAt = generatedAt,
            Options = new WorkerSetlistOptions(options.Lyrics, options.Sections, options.Notes, options.PageBreakPerSong),
            Songs = songs,
        };
    }

    /// <summary>Plain lyrics lines, cut after <see cref="MaxLyricsCharacters"/> characters (the line breaks are not counted).</summary>
    /// <param name="plainText">The lyrics text (empty lines are stanza breaks).</param>
    /// <returns>The lines, or null when there is no text.</returns>
    public static WorkerSetlistLyrics? Lyrics(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return null;
        }

        var lines = new List<string>();
        var left = MaxLyricsCharacters;
        foreach (var line in plainText.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.Length > left)
            {
                lines.Add(line[..left]);
                return new WorkerSetlistLyrics(lines, true);
            }

            lines.Add(line);
            left -= line.Length;
        }

        return new WorkerSetlistLyrics(lines, false);
    }

    private static WorkerSetlistSong Song(SetlistEntry entry, int position, SetlistSource source)
    {
        var track = source.Library.Tracks[entry.TrackId];
        var grid = track.BeatGrid;
        return new WorkerSetlistSong
        {
            Position = position,
            Title = Cut(track.Title),
            Artist = CutOrNull(track.Artist),
            Bpm = grid?.Bpm,
            BeatsPerBar = grid?.BeatsPerBar,
            DurationSeconds = track.DurationSeconds ?? 0,
            MixName = MixName(entry, source.Library),
            Note = entry.Note.Length == 0 ? null : entry.Note,
            Sections = SectionsOf(source.Sections.GetValueOrDefault(entry.TrackId)),
            Lyrics = Lyrics(LyricsText(track)),
        };
    }

    private static string? MixName(SetlistEntry entry, SetlistLibrary library) =>
        entry.MixId is { } mixId && library.Mixes.TryGetValue(mixId, out var mix) ? mix.Name : null;

    private static List<WorkerSetlistSection> SectionsOf(TrackSections? sections) =>
        (sections?.Sections ?? []).Select(section => new WorkerSetlistSection(section.Label, SectionMapper.KindName(section.Kind), section.StartSeconds)).ToList();

    private static string? LyricsText(Domain.Tracks.Track track) => track.Lyrics is { HasText: true, Document: { } document } ? document.PlainText : null;

    private static string? CutOrNull(string? value) => value is null ? null : Cut(value);

    private static string Cut(string value) => value.Length <= MaxTitleLength ? value : value[..MaxTitleLength];
}
