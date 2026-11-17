using StemStudio.Api.SongSections;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Setlists;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Setlists;

namespace StemStudio.Api.Setlists;

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
    /// <param name="setlist">The setlist (at least one entry).</param>
    /// <param name="library">Its tracks and mixes.</param>
    /// <param name="sections">Section set per track id (missing = none).</param>
    /// <param name="options">Print options.</param>
    /// <param name="generatedAt">Printed in the footer.</param>
    /// <returns>The request.</returns>
    public static WorkerSetlistDocumentRequest Build(Setlist setlist, SetlistLibrary library, IReadOnlyDictionary<Guid, TrackSections> sections, SetlistPdfOptions options, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(setlist);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(options);
        var songs = setlist.Entries.Where(entry => library.Tracks.ContainsKey(entry.TrackId))
            .Select((entry, index) => Song(entry, index + 1, library, sections.GetValueOrDefault(entry.TrackId))).ToList();
        return new WorkerSetlistDocumentRequest
        {
            Title = setlist.Name,
            Note = setlist.Note,
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

    private static WorkerSetlistSong Song(SetlistEntry entry, int position, SetlistLibrary library, TrackSections? sections)
    {
        var track = library.Tracks[entry.TrackId];
        var mix = entry.MixId is { } mixId ? library.Mixes.GetValueOrDefault(mixId) : null;
        return new WorkerSetlistSong
        {
            Position = position,
            Title = Cut(track.Title),
            Artist = track.Artist is { } artist ? Cut(artist) : null,
            Bpm = track.BeatGrid?.Bpm,
            BeatsPerBar = track.BeatGrid?.BeatsPerBar,
            DurationSeconds = track.DurationSeconds ?? 0,
            MixName = mix?.Name,
            Note = entry.Note.Length == 0 ? null : entry.Note,
            Sections = (sections?.Sections ?? []).Select(section => new WorkerSetlistSection(section.Label, SectionMapper.KindName(section.Kind), section.StartSeconds)).ToList(),
            Lyrics = Lyrics(track.Lyrics?.HasText == true ? track.Lyrics.Document!.PlainText : null),
        };
    }

    private static string Cut(string value) => value.Length <= MaxTitleLength ? value : value[..MaxTitleLength];
}
