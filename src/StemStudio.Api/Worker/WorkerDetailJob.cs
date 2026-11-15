namespace StemStudio.Api.Worker;

/// <summary>Worker detail job state (contract 1.1.0 <c>DetailJobState</c>).</summary>
public enum WorkerDetailJobState
{
    /// <summary>Waiting behind every standard job.</summary>
    Queued,

    /// <summary>A parent stem is being split.</summary>
    Running,

    /// <summary>Terminal: result and detail stems available.</summary>
    Completed,

    /// <summary>Terminal: see <see cref="WorkerDetailJob.Error"/>.</summary>
    Failed,

    /// <summary>Terminal: cancelled.</summary>
    Cancelled,
}

/// <summary>A worker detail job (contract 1.1.0 <c>DetailJob</c>).</summary>
public sealed record WorkerDetailJob
{
    /// <summary>Detail job ID (32 lower-case hex characters).</summary>
    public required string DetailJobId { get; init; }

    /// <summary>The completed standard job whose stems are split.</summary>
    public required string StandardJobId { get; init; }

    /// <summary>State.</summary>
    public required WorkerDetailJobState Status { get; init; }

    /// <summary>Human-readable stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Parent stem being split while running.</summary>
    public string? CurrentParent { get; init; }

    /// <summary>Progress 0..100.</summary>
    public int Progress { get; init; }

    /// <summary>Failure reason when failed.</summary>
    public string? Error { get; init; }

    /// <summary>Where the detail steps ran (1.2.0); null before the first parent step, for cached results and for 1.1.0 workers.</summary>
    public WorkerJobDevice? Device { get; init; }
}
