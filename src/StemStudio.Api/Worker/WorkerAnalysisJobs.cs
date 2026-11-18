namespace StemStudio.Api.Worker;

/// <summary>Body of <c>POST /v1/instrument-analysis-jobs</c> (worker 1.9.0 <c>InstrumentAnalysisJobRequest</c>, serialised by its runtime type).</summary>
public abstract record WorkerAnalysisRequest
{
    /// <summary><c>chords</c>, <c>drum-hits</c> or <c>vocal-pitch</c>.</summary>
    public abstract string Kind { get; }
}

/// <summary>A chords job (worker 1.9.0 <c>ChordsJobRequest</c>).</summary>
public sealed record WorkerChordsRequest : WorkerAnalysisRequest
{
    /// <inheritdoc />
    public override string Kind => "chords";

    /// <summary>The harmonic stems (1..4 library paths).</summary>
    public required IReadOnlyList<string> Inputs { get; init; }

    /// <summary>The bass stem for slash chords, or null.</summary>
    public string? BassInput { get; init; }

    /// <summary>The reliable beat grid, or null.</summary>
    public WorkerBeatGridTempo? Grid { get; init; }

    /// <summary><c>simple</c> or <c>full</c>.</summary>
    public required string Vocabulary { get; init; }

    /// <summary><c>btc</c>, <c>chroma-hmm</c>, or null for the worker default.</summary>
    public string? Engine { get; init; }
}

/// <summary>A drum hits job (worker 1.9.0 <c>DrumHitsJobRequest</c>).</summary>
public sealed record WorkerDrumHitsRequest : WorkerAnalysisRequest
{
    /// <inheritdoc />
    public override string Kind => "drum-hits";

    /// <summary>The drum detail stems.</summary>
    public required WorkerDrumInputs DrumInputs { get; init; }
}

/// <summary>A reference melody job (worker 1.9.0 <c>VocalPitchJobRequest</c>).</summary>
public sealed record WorkerVocalPitchRequest : WorkerAnalysisRequest
{
    /// <inheritdoc />
    public override string Kind => "vocal-pitch";

    /// <summary>The vocal stem.</summary>
    public required string Input { get; init; }

    /// <summary><c>vocals.lead</c> or <c>vocals</c>.</summary>
    public required string Source { get; init; }
}

/// <summary>A key on the wire (worker 1.9.0 <c>AnalysisKey</c>).</summary>
/// <param name="Tonic">Tonic 0..11.</param>
/// <param name="Mode"><c>major</c> or <c>minor</c>.</param>
/// <param name="Confidence">Confidence 0..1 (absent in alternatives).</param>
/// <param name="Alternative">Relative key, or null.</param>
public sealed record WorkerKey(int Tonic, string Mode, double Confidence, WorkerKey? Alternative);

/// <summary>A key region on the wire.</summary>
/// <param name="StartMs">Start.</param>
/// <param name="EndMs">End.</param>
/// <param name="Tonic">Tonic.</param>
/// <param name="Mode">Mode.</param>
/// <param name="Confidence">Confidence.</param>
public sealed record WorkerKeyRegion(int StartMs, int EndMs, int Tonic, string Mode, double Confidence);

/// <summary>A chord segment on the wire.</summary>
/// <param name="StartMs">Start.</param>
/// <param name="EndMs">End.</param>
/// <param name="Label">Harte label.</param>
/// <param name="Root">Root or null.</param>
/// <param name="Quality">Harte quality or null.</param>
/// <param name="Bass">Bass or null.</param>
/// <param name="Confidence">Confidence.</param>
public sealed record WorkerChordSegment(int StartMs, int EndMs, string Label, int? Root, string? Quality, int? Bass, double Confidence);

/// <summary>A warning on the wire.</summary>
/// <param name="Code">Kebab-case code.</param>
/// <param name="Text">Text.</param>
public sealed record WorkerAnalysisWarning(string Code, string Text);

/// <summary>A drum hit on the wire.</summary>
/// <param name="TimeMs">Time.</param>
/// <param name="Piece">Piece.</param>
/// <param name="Strength">Strength.</param>
public sealed record WorkerDrumHit(int TimeMs, string Piece, double Strength);

/// <summary>A pitch range on the wire.</summary>
/// <param name="LowCents">Low.</param>
/// <param name="HighCents">High.</param>
public sealed record WorkerPitchRange(int LowCents, int HighCents);

/// <summary>
/// The result of an analysis job: the union of worker 1.9.0 <c>ChordChart</c>, <c>DrumHits</c> and <c>VocalPitch</c>
/// (the fields of the other kinds are null).
/// </summary>
public sealed record WorkerAnalysisResult
{
    /// <summary><c>chords</c>, <c>drum-hits</c> or <c>vocal-pitch</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Engine that ran.</summary>
    public required string Engine { get; init; }

    /// <summary>Chords: vocabulary.</summary>
    public string? Vocabulary { get; init; }

    /// <summary>Chords: global key.</summary>
    public WorkerKey? Key { get; init; }

    /// <summary>Chords: key regions.</summary>
    public IReadOnlyList<WorkerKeyRegion>? KeyRegions { get; init; }

    /// <summary>Chords: spelling hint.</summary>
    public bool PreferFlats { get; init; }

    /// <summary>Chords: snapped to beats.</summary>
    public bool BeatSnapped { get; init; }

    /// <summary>Chords: every segment is N.</summary>
    public bool NoChords { get; init; }

    /// <summary>Chords: segments.</summary>
    public IReadOnlyList<WorkerChordSegment>? Segments { get; init; }

    /// <summary>Chords and vocal pitch: warnings.</summary>
    public IReadOnlyList<WorkerAnalysisWarning>? Warnings { get; init; }

    /// <summary>Drum hits: pieces with an input.</summary>
    public IReadOnlyList<string>? Pieces { get; init; }

    /// <summary>Drum hits: hits.</summary>
    public IReadOnlyList<WorkerDrumHit>? Hits { get; init; }

    /// <summary>Vocal pitch: hop.</summary>
    public int? HopMs { get; init; }

    /// <summary>Vocal pitch: frames.</summary>
    public IReadOnlyList<int>? Frames { get; init; }

    /// <summary>Vocal pitch: source.</summary>
    public string? Source { get; init; }

    /// <summary>Vocal pitch: voiced share.</summary>
    public double? VoicedRatio { get; init; }

    /// <summary>Vocal pitch: range.</summary>
    public WorkerPitchRange? Range { get; init; }
}

/// <summary>An analysis job (worker 1.9.0 <c>InstrumentAnalysisJob</c>).</summary>
public sealed record WorkerAnalysisJob
{
    /// <summary>Job id.</summary>
    public required string AnalysisJobId { get; init; }

    /// <summary>Kind.</summary>
    public required string Kind { get; init; }

    /// <summary>Status.</summary>
    public required WorkerPracticeJobState Status { get; init; }

    /// <summary>Stage, or null.</summary>
    public string? Stage { get; init; }

    /// <summary>Progress 0..1.</summary>
    public double Progress { get; init; }

    /// <summary>Device, or null.</summary>
    public WorkerJobDevice? Device { get; init; }

    /// <summary>Failure text.</summary>
    public string? Error { get; init; }

    /// <summary>Result when completed.</summary>
    public WorkerAnalysisResult? Result { get; init; }
}

/// <summary>The instrument-analysis lane in the worker health (worker 1.9.0 <c>InstrumentAnalysisHealth</c>).</summary>
/// <param name="Models">Readiness per BTC model (<c>ready</c> or <c>not-downloaded</c>).</param>
/// <param name="ChordEngine">Configured chord engine.</param>
/// <param name="PitchEngine">Configured pitch engine.</param>
public sealed record WorkerInstrumentAnalysisHealth(IReadOnlyDictionary<string, string> Models, string ChordEngine, string PitchEngine);
