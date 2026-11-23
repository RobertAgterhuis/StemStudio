using StemStudio.Api.Handouts.Sources;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Handouts;

/// <summary>Reads everything a song brings into a handout through the 014/016 source ports (data-model 6.3 step 1).</summary>
/// <param name="sections">014 sections.</param>
/// <param name="chords">016 chord chart.</param>
/// <param name="practice">014 practice log.</param>
/// <param name="notation">008/016 notation items.</param>
/// <param name="notes">Practice notes.</param>
public sealed class HandoutMaterialLoader(ISectionSource sections, IChordSource chords, IPracticeLogSource practice, INotationCatalog notation, IPracticeNoteRepository notes)
{
    /// <summary>The material of one song.</summary>
    /// <param name="track">The song (with its lyrics row).</param>
    /// <param name="family">The family (its practice note).</param>
    /// <param name="entryNotes">Setlist entry notes, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The material.</returns>
    public async Task<HandoutMaterial> LoadAsync(Track track, HandoutFamily family, string? entryNotes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        var lyrics = track.Lyrics is { Status: LyricsStatus.Found, Document: { } document } found ? (document, found.OffsetMs) : (null, 0);
        return new HandoutMaterial
        {
            TrackId = track.Id,
            Title = track.Title,
            Artist = track.Artist,
            DurationSeconds = track.DurationSeconds ?? 0,
            Grid = track.BeatGrid,
            Artwork = track.Artwork,
            ArtworkPath = ArtworkPath(track),
            Sections = await sections.SectionsAsync(track, cancellationToken),
            Chart = await chords.ChartAsync(track.Id, cancellationToken),
            Lyrics = lyrics.document,
            LyricsOffsetMs = lyrics.Item2,
            BestTempo = await practice.BestTempoAsync(track.Id, cancellationToken),
            Notation = await notation.ItemsAsync(track.Id, cancellationToken),
            PracticeNote = (await notes.FindAsync(track.Id, family, cancellationToken))?.Text,
            EntryNotes = entryNotes,
        };
    }

    /// <summary>The library path of the shown cover (<c>tracks/&lt;id&gt;/artwork/(auto|user)/cover.jpg</c>), or null.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The path or null.</returns>
    public static string? ArtworkPath(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.Artwork is null ? null : $"tracks/{track.Id:D}/artwork/{(track.HasUserArtwork ? "user" : "auto")}/cover.jpg";
    }
}
