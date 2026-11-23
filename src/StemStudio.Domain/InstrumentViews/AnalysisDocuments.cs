using StemStudio.Domain.Scores;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>The three per-track analyses of feature 016 (data-model 7.1).</summary>
public enum AnalysisKind
{
    /// <summary>Chord chart with key (US1, US7, US8).</summary>
    Chords,

    /// <summary>Drum hits per piece (US3).</summary>
    DrumHits,

    /// <summary>Reference melody of the lead vocal (US4).</summary>
    VocalPitch,
}

/// <summary>Status of an analysis (data-model 7.2).</summary>
public enum AnalysisStatus
{
    /// <summary>Never run, or deleted.</summary>
    None,

    /// <summary>Submitted to the worker.</summary>
    Queued,

    /// <summary>The worker runs it.</summary>
    Running,

    /// <summary>The document is stored.</summary>
    Ready,

    /// <summary>The worker failed, or lost the job.</summary>
    Failed,

    /// <summary>A prerequisite is missing (drums not split, no vocals, no harmonic stem).</summary>
    Unavailable,

    /// <summary>Ready, but the input stems changed since.</summary>
    Stale,
}

/// <summary>Chord vocabulary (data-model 2.1).</summary>
public enum ChordVocabulary
{
    /// <summary>Major and minor (25 classes).</summary>
    Simple,

    /// <summary>14 qualities and inversions.</summary>
    Full,
}

/// <summary>Warning codes of the analysis documents (data-model 2.4, 3.1).</summary>
public enum AnalysisWarningCode
{
    /// <summary>The fast method knows only major and minor.</summary>
    VocabularyReduced,

    /// <summary>Changes are not aligned to beats.</summary>
    GridUnreliable,

    /// <summary>The chord model was not available.</summary>
    FallbackEngine,

    /// <summary>Very few chords.</summary>
    FewChords,

    /// <summary>The key is uncertain.</summary>
    KeyUncertain,

    /// <summary>Few voiced frames.</summary>
    FewVoiced,

    /// <summary>Other instruments bleed into the detail stem.</summary>
    DetailBleed,
}

/// <summary>Drum pieces of the drum trainer (data-model 5.1).</summary>
public enum DrumPiece
{
    /// <summary>Kick.</summary>
    Kick,

    /// <summary>Snare.</summary>
    Snare,

    /// <summary>Toms.</summary>
    Toms,

    /// <summary>Hi-hat.</summary>
    Hihat,

    /// <summary>Cymbals.</summary>
    Cymbals,
}

/// <summary>Source of the reference melody (data-model 7.4).</summary>
public enum VocalSource
{
    /// <summary>The kept lead stem of the vocals split.</summary>
    VocalsLead,

    /// <summary>The standard vocals stem.</summary>
    Vocals,
}

/// <summary>A warning of an analysis document.</summary>
/// <param name="Code">Code.</param>
/// <param name="Text">Readable text.</param>
public sealed record AnalysisWarning(AnalysisWarningCode Code, string Text);

/// <summary>A stored analysis result (one of the three documents).</summary>
public abstract record AnalysisDocument
{
    /// <summary>The engine that produced it (<c>btc</c>, <c>chroma-hmm</c>, <c>drum-onsets</c>, <c>pyin</c>, <c>yin</c>, <c>fake</c>).</summary>
    public required string Engine { get; init; }
}

/// <summary>The global key of a chord chart (data-model 2.8).</summary>
/// <param name="Tonic">Tonic 0..11.</param>
/// <param name="Mode">Mode.</param>
/// <param name="Confidence">Confidence 0..1.</param>
/// <param name="Alternative">The relative key when it fits almost as well, or null.</param>
public sealed record SongKey(int Tonic, KeyMode Mode, double Confidence, MusicalKey? Alternative)
{
    /// <summary>Tonic and mode.</summary>
    public MusicalKey Key => new(Tonic, Mode);
}

/// <summary>A key region (data-model 2.8).</summary>
/// <param name="StartMs">Start.</param>
/// <param name="EndMs">End.</param>
/// <param name="Tonic">Tonic 0..11.</param>
/// <param name="Mode">Mode.</param>
/// <param name="Confidence">Confidence 0..1.</param>
public sealed record KeyRegion(int StartMs, int EndMs, int Tonic, KeyMode Mode, double Confidence)
{
    /// <summary>Tonic and mode.</summary>
    public MusicalKey Key => new(Tonic, Mode);
}

/// <summary>One chord segment (data-model 2.2).</summary>
public sealed record ChordSegment
{
    /// <summary>Start (ms).</summary>
    public required int StartMs { get; init; }

    /// <summary>End (ms).</summary>
    public required int EndMs { get; init; }

    /// <summary>Harte label.</summary>
    public required string Label { get; init; }

    /// <summary>Root 0..11, null for <c>N</c>/<c>X</c>.</summary>
    public int? Root { get; init; }

    /// <summary>Quality, null for <c>N</c>/<c>X</c>.</summary>
    public ChordQuality? Quality { get; init; }

    /// <summary>Bass pitch class when not the root.</summary>
    public int? Bass { get; init; }

    /// <summary>Confidence 0..1.</summary>
    public double Confidence { get; init; }

    /// <summary>Corrected by the user (FR-015).</summary>
    public bool Edited { get; init; }

    /// <summary>The chord by pitch classes.</summary>
    public ChordName Chord => new(Root, Quality, Bass);

    /// <summary>True for a segment with a chord (not <c>N</c>/<c>X</c>).</summary>
    public bool HasChord => Root is not null && Quality is not null;

    /// <summary>Length in milliseconds.</summary>
    public int DurationMs => EndMs - StartMs;
}

/// <summary>A chord chart (data-model 2.4) with the user's edits.</summary>
public sealed record ChordChart : AnalysisDocument
{
    /// <summary>Vocabulary that ran.</summary>
    public required ChordVocabulary Vocabulary { get; init; }

    /// <summary>Global key, or null when no chords were found.</summary>
    public SongKey? Key { get; init; }

    /// <summary>Key regions in time order.</summary>
    public IReadOnlyList<KeyRegion> KeyRegions { get; init; } = [];

    /// <summary>Spelling hint of the global key.</summary>
    public bool PreferFlats { get; init; }

    /// <summary>Changes were snapped to beats.</summary>
    public bool BeatSnapped { get; init; }

    /// <summary>Every segment is <c>N</c>.</summary>
    public bool NoChords { get; init; }

    /// <summary>Segments in time order.</summary>
    public required IReadOnlyList<ChordSegment> Segments { get; init; }

    /// <summary>Warnings.</summary>
    public IReadOnlyList<AnalysisWarning> Warnings { get; init; } = [];

    /// <summary>At least one segment was edited.</summary>
    public bool HasEdits => Segments.Any(segment => segment.Edited);

    /// <summary>The spelling key of a chord starting at <paramref name="startMs"/>: its region's key, else the global key.</summary>
    /// <param name="startMs">The chord start.</param>
    /// <returns>The key, or null (sharps).</returns>
    public MusicalKey? SpellingKeyAt(int startMs) =>
        KeyRegions.FirstOrDefault(region => region.StartMs <= startMs && startMs < region.EndMs)?.Key ?? Key?.Key;

    /// <summary>A copy with one segment set to a new label and marked edited (FR-015).</summary>
    /// <param name="index">0-based segment index.</param>
    /// <param name="label">A Harte label of the edit pattern.</param>
    /// <returns>The edited chart.</returns>
    /// <exception cref="DomainValidationException">The index or the label is invalid.</exception>
    public ChordChart WithEdit(int index, string label)
    {
        var errors = new ValidationErrors();
        errors.Check(index >= 0 && index < Segments.Count, "segmentIndex", $"Must be between 0 and {Segments.Count - 1}.");
        var valid = HarteLabel.TryParse(label, out var chord);
        errors.Check(valid, "label", "Must be a chord label like A:min7, C:maj/3 or N.");
        errors.ThrowIfAny();
        var segments = Segments.ToArray();
        segments[index] = segments[index] with { Label = label, Root = chord.Root, Quality = chord.Quality, Bass = chord.Bass, Edited = true };
        return this with { Segments = segments };
    }
}

/// <summary>One drum hit (data-model 5.1).</summary>
/// <param name="TimeMs">Time.</param>
/// <param name="Piece">Piece.</param>
/// <param name="Strength">Strength 0..1.</param>
public sealed record DrumHit(int TimeMs, DrumPiece Piece, double Strength);

/// <summary>Drum hits per piece (data-model 5.1).</summary>
public sealed record DrumHitsDocument : AnalysisDocument
{
    /// <summary>Pieces that had an input.</summary>
    public required IReadOnlyList<DrumPiece> Pieces { get; init; }

    /// <summary>Hits in time order.</summary>
    public required IReadOnlyList<DrumHit> Hits { get; init; }
}

/// <summary>Range of a reference melody in centi-semitones.</summary>
/// <param name="LowCents">Lowest voiced frame.</param>
/// <param name="HighCents">Highest voiced frame.</param>
public sealed record PitchRange(int LowCents, int HighCents);

/// <summary>Reference melody (data-model 3.1): one value per 10 ms in centi-semitones, 0 = unvoiced.</summary>
public sealed record VocalPitchDocument : AnalysisDocument
{
    /// <summary>Frame hop (10 ms).</summary>
    public int HopMs { get; init; } = 10;

    /// <summary>Frames (MIDI x 100; 0 = unvoiced).</summary>
    public required IReadOnlyList<int> Frames { get; init; }

    /// <summary>The analysed stem.</summary>
    public required VocalSource Source { get; init; }

    /// <summary>Share of voiced frames 0..1.</summary>
    public double VoicedRatio { get; init; }

    /// <summary>Voiced range, or null.</summary>
    public PitchRange? Range { get; init; }

    /// <summary>Warnings.</summary>
    public IReadOnlyList<AnalysisWarning> Warnings { get; init; } = [];
}
