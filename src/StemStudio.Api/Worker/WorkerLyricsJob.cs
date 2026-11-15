using System.Text.Json.Serialization;
using StemStudio.Domain.Lyrics;

namespace StemStudio.Api.Worker;

/// <summary>Body of <c>POST /v1/lyrics-jobs</c> with <c>mode: lookup</c> (worker 1.5.0 <c>LookupLyricsRequest</c>).</summary>
public sealed record WorkerLyricsLookupRequest
{
    /// <summary>Always <c>lookup</c>.</summary>
    public string Mode => "lookup";

    /// <summary>Title (the 005 cleaned lookup title).</summary>
    public required string Title { get; init; }

    /// <summary>Artist, when known.</summary>
    public string? Artist { get; init; }

    /// <summary>Album, when known.</summary>
    public string? Album { get; init; }

    /// <summary>Duration of the normalised audio (0 &lt; d &lt;= 900).</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>The standard job, for correlation only.</summary>
    public string? StandardJobId { get; init; }
}

/// <summary>Body of <c>POST /v1/lyrics-jobs</c> with <c>mode: align</c> (worker 1.5.0 <c>AlignLyricsRequest</c>).</summary>
public sealed record WorkerLyricsAlignRequest
{
    /// <summary>Always <c>align</c>.</summary>
    public string Mode => "align";

    /// <summary>Completed standard job whose vocal stem is aligned.</summary>
    public required string StandardJobId { get; init; }

    /// <summary>Plain lyrics, one line per line break.</summary>
    public required string Text { get; init; }

    /// <summary>ISO 639 language hint; null = detect.</summary>
    public string? Language { get; init; }

    /// <summary>A line-synced document whose line times are kept; null aligns the whole text.</summary>
    public LyricsDocument? Refine { get; init; }

    /// <summary>Prints everything but the text: lyrics are copyrighted and never logged (FR-010).</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"Mode = {Mode}, StandardJobId = {StandardJobId}, Language = {Language}, Refine = {Refine}");
        return true;
    }
}

/// <summary>Mode of a lyrics job (contract <c>LyricsJobMode</c>).</summary>
public enum WorkerLyricsJobMode
{
    /// <summary>LRCLIB lookup.</summary>
    Lookup,

    /// <summary>Alignment to the vocal stem.</summary>
    Align,
}

/// <summary>State of a lyrics job (contract <c>LyricsJobState</c>).</summary>
public enum WorkerLyricsJobState
{
    /// <summary>Waiting.</summary>
    Queued,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Terminal: result available.</summary>
    Completed,

    /// <summary>Terminal: see <see cref="WorkerLyricsJob.Error"/>.</summary>
    Failed,

    /// <summary>Terminal: cancelled.</summary>
    Cancelled,
}

/// <summary>A lyrics job (contract <c>LyricsJob</c>).</summary>
public sealed record WorkerLyricsJob
{
    /// <summary>Job ID (32 lower-case hex characters).</summary>
    public required string LyricsJobId { get; init; }

    /// <summary>Lookup or align.</summary>
    public WorkerLyricsJobMode Mode { get; init; }

    /// <summary>State.</summary>
    public required WorkerLyricsJobState Status { get; init; }

    /// <summary>Readable stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Progress 0..1.</summary>
    public double Progress { get; init; }

    /// <summary>Where the alignment runs (align only).</summary>
    public WorkerJobDevice? Device { get; init; }

    /// <summary>Expected wall time of the alignment (align only).</summary>
    public double? EstimatedSeconds { get; init; }

    /// <summary>Fixed failure text when failed.</summary>
    public string? Error { get; init; }
}

/// <summary>Outcome of a lyrics job (contract <c>LyricsOutcome</c>).</summary>
public enum WorkerLyricsOutcome
{
    /// <summary>Lyrics found (word, line or plain).</summary>
    Found,

    /// <summary>The song is instrumental.</summary>
    Instrumental,

    /// <summary>Nothing found.</summary>
    [JsonStringEnumMemberName("not-found")]
    NotFound,

    /// <summary>Online lookups are off.</summary>
    Disabled,
}

/// <summary>
/// Result of a completed lyrics job (contract <c>LyricsResult</c>). The <c>TimedLyrics</c> wire shape equals
/// <see cref="LyricsDocument"/> (camelCase); its rules are checked again by the domain. Copyrighted: never logged.
/// </summary>
public sealed record WorkerLyricsResult
{
    /// <summary>Outcome.</summary>
    public required WorkerLyricsOutcome Outcome { get; init; }

    /// <summary>The document for found and instrumental; null otherwise.</summary>
    public LyricsDocument? Lyrics { get; init; }
}

/// <summary>Lyrics alignment readiness of the worker (contract 1.5.0 <c>LyricsAlignmentHealth</c>).</summary>
public sealed record WorkerLyricsAlignmentHealth
{
    /// <summary>False when the alignment stack cannot be loaded.</summary>
    public bool Available { get; init; }

    /// <summary>Whisper model, e.g. <c>large-v3-turbo</c>.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Whether the model weights are downloaded.</summary>
    public bool ModelCached { get; init; }

    /// <summary>The automatic alignment setting.</summary>
    public AlignAuto Auto { get; init; }

    /// <summary>Why the alignment is unavailable.</summary>
    public string? Reason { get; init; }
}
