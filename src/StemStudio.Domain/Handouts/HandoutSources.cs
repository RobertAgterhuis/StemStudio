using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Handouts;

/// <summary>A song section as the handout reads it (014 <c>SectionDto</c>, DELTA P0-4 A-014-1).</summary>
/// <param name="Id">Section id (the practice log's key).</param>
/// <param name="Label">Label (<c>Chorus 2</c>).</param>
/// <param name="StartSeconds">Start.</param>
/// <param name="EndSeconds">End.</param>
/// <param name="StartBar">014's start bar from the beat grid (1-based, pickup 0); null without a reliable grid.</param>
public sealed record SongSection(Guid Id, string Label, double StartSeconds, double EndSeconds, int? StartBar);

/// <summary>A setlist entry as the handout reads it (014 <c>SetlistEntryDto</c>, A-014-2).</summary>
/// <param name="Position">1-based running order.</param>
/// <param name="TrackId">The song (014 removes entries of deleted tracks).</param>
/// <param name="Notes">Entry note; null when 014 has <c>""</c>.</param>
public sealed record SetlistSong(int Position, Guid TrackId, string? Notes);

/// <summary>A setlist as the handout reads it (014 <c>SetlistDto</c>, A-014-2).</summary>
/// <param name="Id">Setlist id.</param>
/// <param name="Name">Name.</param>
/// <param name="Notes">Setlist note; null when 014 has <c>""</c>.</param>
/// <param name="Entries">Entries in running order.</param>
public sealed record SetlistSnapshot(Guid Id, string Name, string? Notes, IReadOnlyList<SetlistSong> Entries);

/// <summary>A ready notation item of a song (a 008/016 <c>score</c> library item, A-016-2).</summary>
public sealed record NotationItem
{
    /// <summary>The score item.</summary>
    public required Guid AssetId { get; init; }

    /// <summary>Item name.</summary>
    public required string Title { get; init; }

    /// <summary>The song.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Profile.</summary>
    public required ScoreProfile Profile { get; init; }

    /// <summary>Score, tab, drums or transposed part.</summary>
    public required NotationKind Kind { get; init; }

    /// <summary>Written transposition of a part (<c>c</c> for a concert part); null for other items.</summary>
    public Transposition? Transposition { get; init; }

    /// <summary>The stem it was made from (<c>bass</c>, <c>other.flute</c>); null for a library source.</summary>
    public string? Channel { get; init; }

    /// <summary>Library path of the PDF (<c>assets/&lt;id&gt;/score.pdf</c>).</summary>
    public required string PdfPath { get; init; }

    /// <summary>The score's key, or null.</summary>
    public KeyInfo? Key { get; init; }

    /// <summary>Model attribution (008 piano CC BY 4.0), or null.</summary>
    public string? Attribution { get; init; }

    /// <summary>When the item was made (newest wins).</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>Classifies a score item for handouts by its 016 facts (DELTA P0-4 A-016-2).</summary>
public static class NotationClassifier
{
    /// <summary>Drum profile = drums; tablature = tab; a non-concert part = transposed; else score.</summary>
    /// <param name="facts">The score facts.</param>
    /// <returns>The kind and, for a part, its written transposition.</returns>
    public static (NotationKind Kind, Transposition? Transposition) Classify(ScoreFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Profile == ScoreProfile.Drums)
        {
            return (NotationKind.Drums, null);
        }

        if (facts.Tablature is not null)
        {
            return (NotationKind.Tab, null);
        }

        if (facts.Part is not { } part)
        {
            return (NotationKind.Score, null);
        }

        var transposition = Written(part.Instrument);
        return (transposition == Handouts.Transposition.C ? NotationKind.Score : NotationKind.Transposed, transposition);
    }

    /// <summary>The written transposition of a part instrument: B-flat (<c>*-bb</c>), E-flat (<c>*-eb</c>), F (<c>*-f</c>), else C.</summary>
    /// <param name="instrument">The instrument.</param>
    /// <returns>The transposition.</returns>
    public static Transposition Written(WrittenInstrument instrument)
    {
        var name = instrument.ToString();
        return name.EndsWith("Bb", StringComparison.Ordinal) ? Handouts.Transposition.BFlat
            : name.EndsWith("Eb", StringComparison.Ordinal) ? Handouts.Transposition.EFlat
            : name.EndsWith('F') ? Handouts.Transposition.F
            : Handouts.Transposition.C;
    }
}
