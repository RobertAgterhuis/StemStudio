namespace StemStudio.Contracts.InstrumentViews;

/// <summary>The stored chord chart (<c>ChordChartDocument</c>) (backend-api 1.15.0, feature 016, data-model 2.4).</summary>
public sealed record ChordChartDocumentDto
{
    /// <summary>Vocabulary.</summary>
    public required ChordVocabularyDto Vocabulary { get; init; }

    /// <summary>Engine that ran.</summary>
    public required ChordEngineDto Engine { get; init; }

    /// <summary>Global key; null without chords.</summary>
    public required SongKeyDto? Key { get; init; }

    /// <summary>Key regions (0..50).</summary>
    public required IReadOnlyList<KeyRegionDto> KeyRegions { get; init; }

    /// <summary>Spelling hint of the global key.</summary>
    public required bool PreferFlats { get; init; }

    /// <summary>Chord changes were snapped to beats.</summary>
    public required bool BeatSnapped { get; init; }

    /// <summary>Every segment is <c>N</c>.</summary>
    public required bool NoChords { get; init; }

    /// <summary>At least one segment was edited.</summary>
    public required bool HasEdits { get; init; }

    /// <summary>Segments in time order (at most 4000).</summary>
    public required IReadOnlyList<ChordSegmentDto> Segments { get; init; }

    /// <summary>Warnings.</summary>
    public required IReadOnlyList<AnalysisWarningDto> Warnings { get; init; }
}
