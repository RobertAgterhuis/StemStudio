using StemStudio.Domain.Scores;

namespace StemStudio.Api.Worker;

/// <summary>Body of <c>POST /v1/transcription-jobs</c> (worker 1.6.0 <c>TranscriptionJobRequest</c>).</summary>
public sealed record WorkerTranscriptionJobRequest
{
    /// <summary>The profile.</summary>
    public required ScoreProfile Profile { get; init; }

    /// <summary>Piano quality (the worker ignores it for other profiles).</summary>
    public ScoreQuality? Quality { get; init; }

    /// <summary>The input of a pitched profile.</summary>
    public WorkerTranscriptionInput? Input { get; init; }

    /// <summary>The five drum detail stems of the drums profile.</summary>
    public WorkerDrumInputs? DrumInputs { get; init; }

    /// <summary>Part of the input, or null.</summary>
    public WorkerRenderRange? Range { get; init; }

    /// <summary>A <see cref="WorkerBeatGridTempo"/> or a <see cref="WorkerManualTempo"/> (serialised by its runtime type).</summary>
    public required object Grid { get; init; }

    /// <summary>The resolution, or null for the profile default.</summary>
    public ScoreResolution? Resolution { get; init; }

    /// <summary>A key name, or null.</summary>
    public string? KeyOverride { get; init; }

    /// <summary>Titles printed on the score.</summary>
    public required WorkerScoreTitles Titles { get; init; }
}

/// <summary>An input file relative to the library mount.</summary>
/// <param name="Path">Path, e.g. <c>tracks/&lt;id&gt;/stems/bass.flac</c>.</param>
public sealed record WorkerTranscriptionInput(string Path);

/// <summary>The drum detail stems (a missing piece is null).</summary>
/// <param name="Kick">Kick path.</param>
/// <param name="Snare">Snare path.</param>
/// <param name="Toms">Toms path.</param>
/// <param name="Hihat">Hi-hat path.</param>
/// <param name="Cymbals">Cymbals path.</param>
public sealed record WorkerDrumInputs(string? Kick, string? Snare, string? Toms, string? Hihat, string? Cymbals);

/// <summary>The track's beat grid (contract <c>BeatGridTempo</c>).</summary>
/// <param name="Bpm">Tempo.</param>
/// <param name="BeatsPerBar">Beats per bar.</param>
/// <param name="Beats">Beat times relative to the input start.</param>
/// <param name="Downbeats">Downbeat times relative to the input start.</param>
public sealed record WorkerBeatGridTempo(double Bpm, int BeatsPerBar, IReadOnlyList<double> Beats, IReadOnlyList<double> Downbeats)
{
    /// <summary>Always <c>beat-grid</c>.</summary>
    public string Kind => "beat-grid";
}

/// <summary>A constant tempo (contract <c>ManualTempo</c>).</summary>
/// <param name="Bpm">Tempo.</param>
/// <param name="BeatsPerBar">Beats per bar.</param>
/// <param name="FirstDownbeatSeconds">First downbeat relative to the input start.</param>
public sealed record WorkerManualTempo(double Bpm, int BeatsPerBar, double FirstDownbeatSeconds)
{
    /// <summary>Always <c>manual</c>.</summary>
    public string Kind => "manual";
}

/// <summary>Titles of a score (contract <c>ScoreTitles</c>, each at most 200 characters).</summary>
/// <param name="Title">Title (the item name).</param>
/// <param name="Subtitle">"&lt;track title&gt; - &lt;artist&gt;".</param>
/// <param name="InstrumentName">Part name.</param>
public sealed record WorkerScoreTitles(string Title, string? Subtitle, string? InstrumentName);

/// <summary>A transcription job (contract <c>TranscriptionJob</c>; status as render jobs).</summary>
public sealed record WorkerTranscriptionJob
{
    /// <summary>Job ID (32 lower-case hex characters).</summary>
    public required string TranscriptionJobId { get; init; }

    /// <summary>State.</summary>
    public required WorkerRenderJobState Status { get; init; }

    /// <summary><c>loading-model</c>, <c>transcribing</c>, <c>notating</c>, <c>engraving</c>; null while queued and after the end.</summary>
    public string? Stage { get; init; }

    /// <summary>Progress 0..1.</summary>
    public double Progress { get; init; }

    /// <summary>Where the model ran.</summary>
    public WorkerJobDevice? Device { get; init; }

    /// <summary>Failure text.</summary>
    public string? Error { get; init; }

    /// <summary>Files and summary once completed.</summary>
    public WorkerTranscriptionResult? Result { get; init; }
}

/// <summary>Result of a completed transcription job.</summary>
/// <param name="Files">The pdf, musicxml and midi files.</param>
/// <param name="Summary">The score facts.</param>
public sealed record WorkerTranscriptionResult(IReadOnlyList<WorkerTranscriptionFile> Files, WorkerScoreSummary Summary);

/// <summary>One file of a transcription job.</summary>
/// <param name="Role"><c>pdf</c>, <c>musicxml</c> or <c>midi</c>.</param>
/// <param name="FileName">Worker file name.</param>
/// <param name="ContentType">Media type.</param>
/// <param name="SizeBytes">Size in bytes.</param>
/// <param name="Sha256">SHA-256 (64 lower-case hex characters).</param>
public sealed record WorkerTranscriptionFile(string Role, string FileName, string ContentType, long SizeBytes, string Sha256);

/// <summary>The worker's score summary (contract <c>ScoreSummary</c>); engines and warning codes are kebab-case on the wire.</summary>
public sealed record WorkerScoreSummary
{
    /// <summary>The profile.</summary>
    public required ScoreProfile Profile { get; init; }

    /// <summary>The engine.</summary>
    public required ScoreEngine Engine { get; init; }

    /// <summary>Piano only.</summary>
    public ScoreQuality? Quality { get; init; }

    /// <summary>The key; null for drums.</summary>
    public KeyInfo? Key { get; init; }

    /// <summary>Tempo mark.</summary>
    public int Bpm { get; init; }

    /// <summary>Time signature.</summary>
    public string TimeSignature { get; init; } = string.Empty;

    /// <summary>Bars.</summary>
    public int Bars { get; init; }

    /// <summary>Notes.</summary>
    public int Notes { get; init; }

    /// <summary>Pages.</summary>
    public int Pages { get; init; }

    /// <summary>The resolution.</summary>
    public ScoreResolution Resolution { get; init; }

    /// <summary>Warnings.</summary>
    public IReadOnlyList<ScoreWarning> Warnings { get; init; } = [];

    /// <summary>Model credit, or null.</summary>
    public string? Attribution { get; init; }
}
