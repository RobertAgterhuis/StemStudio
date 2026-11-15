namespace StemStudio.Api.Worker;

/// <summary>Worker job state (contract <c>JobState</c>).</summary>
public enum WorkerJobState
{
    /// <summary>Waiting in the FIFO queue.</summary>
    Queued,

    /// <summary>Fetching or normalising the audio.</summary>
    Downloading,

    /// <summary>Running Demucs.</summary>
    Separating,

    /// <summary>Detecting the beat grid.</summary>
    Analysing,

    /// <summary>Terminal: result and stems available.</summary>
    Completed,

    /// <summary>Terminal: see <see cref="WorkerJob.Error"/>.</summary>
    Failed,
}

/// <summary>Source metadata reported by the worker (contract <c>SourceMetadata</c>).</summary>
/// <param name="Title">Title.</param>
/// <param name="Artist">Artist, when known.</param>
/// <param name="SourceUrl">YouTube URL the audio came from; null for uploads.</param>
public sealed record WorkerSourceMetadata(string Title, string? Artist, string? SourceUrl);

/// <summary>A worker job (contract <c>Job</c>).</summary>
public sealed record WorkerJob
{
    /// <summary>Job ID (32 lower-case hex characters).</summary>
    public required string JobId { get; init; }

    /// <summary>Source key.</summary>
    public required string SourceKey { get; init; }

    /// <summary>State.</summary>
    public required WorkerJobState Status { get; init; }

    /// <summary>Human-readable stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Progress 0..100.</summary>
    public int Progress { get; init; }

    /// <summary>Failure reason when failed.</summary>
    public string? Error { get; init; }

    /// <summary>Metadata once the source is resolved.</summary>
    public WorkerSourceMetadata? Metadata { get; init; }
}
