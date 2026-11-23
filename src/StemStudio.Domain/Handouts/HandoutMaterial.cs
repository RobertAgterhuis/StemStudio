using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Handouts;

/// <summary>Everything one song brings into a handout, read once from the sources (data-model 6.3 step 1).</summary>
public sealed record HandoutMaterial
{
    /// <summary>The song.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Artist, or null.</summary>
    public string? Artist { get; init; }

    /// <summary>Length in seconds.</summary>
    public double DurationSeconds { get; init; }

    /// <summary>Beat grid, or null.</summary>
    public BeatGrid? Grid { get; init; }

    /// <summary>The shown artwork, or null.</summary>
    public TrackArtwork? Artwork { get; init; }

    /// <summary>Library path of the shown artwork, or null.</summary>
    public string? ArtworkPath { get; init; }

    /// <summary>Sections in order.</summary>
    public IReadOnlyList<SongSection> Sections { get; init; } = [];

    /// <summary>The 016 chord chart, or null.</summary>
    public ChordChart? Chart { get; init; }

    /// <summary>The found lyrics, or null.</summary>
    public LyricsDocument? Lyrics { get; init; }

    /// <summary>The lyrics offset in milliseconds.</summary>
    public int LyricsOffsetMs { get; init; }

    /// <summary>Best trainer tempo per section id.</summary>
    public IReadOnlyDictionary<Guid, int> BestTempo { get; init; } = new Dictionary<Guid, int>();

    /// <summary>Ready notation items, newest first.</summary>
    public IReadOnlyList<NotationItem> Notation { get; init; } = [];

    /// <summary>The family's practice note, or null.</summary>
    public string? PracticeNote { get; init; }

    /// <summary>Setlist entry notes, or null.</summary>
    public string? EntryNotes { get; init; }

    /// <summary>True when the chart has at least one chord.</summary>
    public bool HasChords => Chart?.Segments.Any(segment => segment.HasChord) == true;

    /// <summary>True when the lyrics have lines to print.</summary>
    public bool HasLyrics => Lyrics is { Instrumental: false, Lines.Count: > 0 };

    /// <summary>True when the beat grid can be used for bars.</summary>
    public bool HasReliableGrid => Grid is { Reliable: true, Downbeats.Count: > 0 };
}

/// <summary>Which ingredients a song can fill (data-model 6.2 "when available").</summary>
public static class HandoutAvailability
{
    /// <summary>Availability of every song ingredient of a song for a family.</summary>
    /// <param name="material">The song.</param>
    /// <param name="notation">The notation chosen for the family.</param>
    /// <returns>Code -> availability.</returns>
    public static IReadOnlyDictionary<IngredientCode, IngredientAvailability> Of(HandoutMaterial material, IReadOnlyList<NotationItem> notation)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(notation);
        return new Dictionary<IngredientCode, IngredientAvailability>
        {
            [IngredientCode.Facts] = IngredientAvailability.Available,
            [IngredientCode.FormChart] = When(material.Sections.Count > 0),
            [IngredientCode.ChordsOverLyrics] = ChordsOverLyrics(material),
            [IngredientCode.Lyrics] = When(material.HasLyrics),
            [IngredientCode.ChordChart] = ChordChart(material),
            [IngredientCode.Notation] = When(notation.Count > 0),
            [IngredientCode.PracticeNotes] = When(material.PracticeNote is not null),
            [IngredientCode.PracticeProgress] = When(material.Sections.Any(section => material.BestTempo.ContainsKey(section.Id))),
            [IngredientCode.SetlistNotes] = When(material.EntryNotes is not null),
        };
    }

    /// <summary>The best availability over several songs (an ingredient is in a setlist handout when one song has it).</summary>
    /// <param name="songs">Per song availability.</param>
    /// <param name="setlistNotes">True when the setlist itself has notes.</param>
    /// <returns>Code -> availability.</returns>
    public static IReadOnlyDictionary<IngredientCode, IngredientAvailability> Combine(IReadOnlyList<IReadOnlyDictionary<IngredientCode, IngredientAvailability>> songs, bool setlistNotes)
    {
        ArgumentNullException.ThrowIfNull(songs);
        var combined = Enum.GetValues<IngredientCode>().ToDictionary(code => code, code => songs.Select(song => song.GetValueOrDefault(code, IngredientAvailability.Missing)).DefaultIfEmpty(IngredientAvailability.Missing).Min());
        if (setlistNotes)
        {
            combined[IngredientCode.SetlistNotes] = IngredientAvailability.Available;
        }

        return combined;
    }

    private static IngredientAvailability ChordsOverLyrics(HandoutMaterial material) => (material.HasChords && material.HasLyrics) switch
    {
        false => IngredientAvailability.Missing,
        true when material.Lyrics!.SyncLevel == SyncLevel.Plain => IngredientAvailability.NoTiming,
        _ => IngredientAvailability.Available,
    };

    private static IngredientAvailability ChordChart(HandoutMaterial material) => (material.HasChords, material.Sections.Count > 0, material.HasReliableGrid) switch
    {
        (true, true, true) => IngredientAvailability.Available,
        (true, true, false) => IngredientAvailability.NoTiming,
        _ => IngredientAvailability.Missing,
    };

    private static IngredientAvailability When(bool available) => available ? IngredientAvailability.Available : IngredientAvailability.Missing;
}
