using StemStudio.Domain.Practice;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Worker;

/// <summary>Status of a practice-lane job (worker 1.8.0 <c>JobStatus</c>).</summary>
public enum WorkerPracticeJobState
{
    /// <summary>Waiting in the practice lane.</summary>
    Queued,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Done; the result is available.</summary>
    Completed,

    /// <summary>Failed; <c>error</c> says why.</summary>
    Failed,

    /// <summary>Cancelled.</summary>
    Cancelled,
}

/// <summary>Fields every practice-lane job has (worker 1.8.0).</summary>
public abstract record WorkerPracticeJob
{
    /// <summary>Job id.</summary>
    public required string JobId { get; init; }

    /// <summary>Status.</summary>
    public required WorkerPracticeJobState Status { get; init; }

    /// <summary>Progress 0..1.</summary>
    public double Progress { get; init; }

    /// <summary>Failure text.</summary>
    public string? Error { get; init; }
}

/// <summary>A kept detail stem for section detection (worker 1.8.0 <c>DetailPath</c>).</summary>
/// <param name="Channel">Channel id <c>parent.stem</c>.</param>
/// <param name="Path">Library path of the FLAC.</param>
public sealed record WorkerDetailPath(string Channel, string Path);

/// <summary>Body of <c>POST /v1/section-jobs</c> (worker 1.8.0 <c>SectionJobRequest</c>).</summary>
public sealed record WorkerSectionJobRequest
{
    /// <summary>Stem name -> library path (vocals, drums, bass, other; guitar and piano for six stems).</summary>
    public required IReadOnlyDictionary<string, string> Stems { get; init; }

    /// <summary>Kept detail stems.</summary>
    public IReadOnlyList<WorkerDetailPath> Details { get; init; } = [];

    /// <summary>The track's beat grid, or null.</summary>
    public required BeatGrid? Grid { get; init; }

    /// <summary>Track length.</summary>
    public required double DurationSeconds { get; init; }
}

/// <summary>Analyser of a section result.</summary>
/// <param name="Name"><c>stems-novelty</c> or <c>fake</c>.</param>
/// <param name="Version">Version.</param>
public sealed record WorkerAnalyzer(string Name, string Version);

/// <summary>One detected section (worker 1.8.0 <c>DetectedSection</c>).</summary>
/// <param name="Kind">Kind (kebab-case on the wire).</param>
/// <param name="Label">Label.</param>
/// <param name="StartSeconds">Start.</param>
/// <param name="EndSeconds">End.</param>
/// <param name="Cluster">Repetition letter.</param>
/// <param name="Instrument">Leading instrument of a solo, or null.</param>
/// <param name="Confidence">Confidence 0..1.</param>
public sealed record WorkerDetectedSection(string Kind, string Label, double StartSeconds, double EndSeconds, string? Cluster, string? Instrument, double Confidence);

/// <summary>An analyser warning.</summary>
/// <param name="Code">Code.</param>
/// <param name="Message">Text.</param>
public sealed record WorkerSectionWarning(string Code, string Message);

/// <summary>Result of a section job (worker 1.8.0 <c>SectionResult</c>).</summary>
/// <param name="Analyzer">The analyser.</param>
/// <param name="Snap"><c>downbeat</c>, <c>beat</c> or <c>time</c>.</param>
/// <param name="Warnings">Warnings.</param>
/// <param name="Sections">Sections in order.</param>
public sealed record WorkerSectionResult(WorkerAnalyzer Analyzer, string Snap, IReadOnlyList<WorkerSectionWarning> Warnings, IReadOnlyList<WorkerDetectedSection> Sections);

/// <summary>A section job (worker 1.8.0 <c>SectionJob</c>).</summary>
public sealed record WorkerSectionJob : WorkerPracticeJob
{
    /// <summary>Result when completed.</summary>
    public WorkerSectionResult? Result { get; init; }
}

/// <summary>Body of <c>POST /v1/score-view-jobs</c> (worker 1.8.0 <c>ScoreViewJobRequest</c>).</summary>
public sealed record WorkerScoreViewJobRequest
{
    /// <summary>Library path <c>assets/&lt;id&gt;/score.musicxml</c>.</summary>
    public required string Musicxml { get; init; }

    /// <summary>A <see cref="WorkerBeatGridTempo"/> or a <see cref="WorkerManualTempo"/> (serialised by its runtime type).</summary>
    public required object Grid { get; init; }

    /// <summary>Length of the score's input (1..900).</summary>
    public required double DurationSeconds { get; init; }
}

/// <summary>One rendered page.</summary>
/// <param name="Page">1-based page.</param>
/// <param name="WidthPx">Width.</param>
/// <param name="HeightPx">Height.</param>
public sealed record WorkerScoreViewPage(int Page, int WidthPx, int HeightPx);

/// <summary>Result of a score view job (worker 1.8.0 <c>ScoreViewResult</c>).</summary>
/// <param name="Pages">Pages.</param>
/// <param name="Bars">Bars (worker 1.8.0 <c>BarBox</c>, times relative to the input start).</param>
/// <param name="Engine">Engraving engine.</param>
public sealed record WorkerScoreViewResult(IReadOnlyList<WorkerScoreViewPage> Pages, IReadOnlyList<ScoreBar> Bars, WorkerAnalyzer Engine);

/// <summary>A score view job (worker 1.8.0 <c>ScoreViewJob</c>).</summary>
public sealed record WorkerScoreViewJob : WorkerPracticeJob
{
    /// <summary>Result when completed.</summary>
    public WorkerScoreViewResult? Result { get; init; }
}

/// <summary>Print options of a setlist document.</summary>
/// <param name="IncludeLyrics">Lyrics per song.</param>
/// <param name="IncludeSections">Sections per song.</param>
/// <param name="IncludeNotes">Notes.</param>
/// <param name="PageBreakPerSong">New page per song.</param>
public sealed record WorkerSetlistOptions(bool IncludeLyrics, bool IncludeSections, bool IncludeNotes, bool PageBreakPerSong);

/// <summary>A section line of a song.</summary>
/// <param name="Label">Label.</param>
/// <param name="Kind">Kind (kebab-case).</param>
/// <param name="StartSeconds">Start.</param>
public sealed record WorkerSetlistSection(string Label, string Kind, double StartSeconds);

/// <summary>Plain lyrics lines of a song.</summary>
/// <param name="Lines">Lines (empty line = stanza break).</param>
/// <param name="Shortened">True when cut at 10,000 characters.</param>
public sealed record WorkerSetlistLyrics(IReadOnlyList<string> Lines, bool Shortened);

/// <summary>One song of a setlist document (worker 1.8.0 <c>SetlistSongContent</c>).</summary>
public sealed record WorkerSetlistSong
{
    /// <summary>1-based position.</summary>
    public required int Position { get; init; }

    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Artist.</summary>
    public string? Artist { get; init; }

    /// <summary>Tempo.</summary>
    public double? Bpm { get; init; }

    /// <summary>Beats per bar.</summary>
    public int? BeatsPerBar { get; init; }

    /// <summary>Key text.</summary>
    public string? Key { get; init; }

    /// <summary>Length.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Mix name.</summary>
    public string? MixName { get; init; }

    /// <summary>Note.</summary>
    public string? Note { get; init; }

    /// <summary>Sections.</summary>
    public IReadOnlyList<WorkerSetlistSection> Sections { get; init; } = [];

    /// <summary>Lyrics, or null.</summary>
    public WorkerSetlistLyrics? Lyrics { get; init; }
}

/// <summary>Body of <c>POST /v1/setlist-document-jobs</c> (worker 1.8.0 <c>SetlistDocumentRequest</c>).</summary>
public sealed record WorkerSetlistDocumentRequest
{
    /// <summary>Setlist name.</summary>
    public required string Title { get; init; }

    /// <summary>Setlist note.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>Printed in the footer.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Options.</summary>
    public required WorkerSetlistOptions Options { get; init; }

    /// <summary>Songs (1..100).</summary>
    public required IReadOnlyList<WorkerSetlistSong> Songs { get; init; }
}

/// <summary>A setlist document job (worker 1.8.0 <c>SetlistDocumentJob</c>).</summary>
public sealed record WorkerSetlistDocumentJob : WorkerPracticeJob
{
    /// <summary>Page count when completed.</summary>
    public int? Pages { get; init; }
}

/// <summary>Queue state of one worker lane (worker 1.8.0 <c>LaneHealth</c>).</summary>
/// <param name="Queued">Waiting jobs.</param>
/// <param name="Running">True while a job runs.</param>
public sealed record WorkerLaneHealth(int Queued, bool Running);

/// <summary>Lanes beside the separation queue (worker 1.8.0 <c>LanesHealth</c>).</summary>
/// <param name="Render">Render lane.</param>
/// <param name="Transcribe">Transcription lane.</param>
/// <param name="Practice">Practice lane (sections, score views, setlist documents).</param>
public sealed record WorkerLanesHealth(WorkerLaneHealth? Render, WorkerLaneHealth? Transcribe, WorkerLaneHealth? Practice);

/// <summary>The configured section analyser (worker 1.8.0 <c>StructureHealth</c>).</summary>
/// <param name="Analyzer"><c>stems-novelty</c> or <c>fake</c>.</param>
/// <param name="Version">Version.</param>
public sealed record WorkerStructureHealth(string Analyzer, string Version);
