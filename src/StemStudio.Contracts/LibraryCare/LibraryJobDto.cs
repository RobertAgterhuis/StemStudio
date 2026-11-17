namespace StemStudio.Contracts.LibraryCare;

/// <summary>A backup, verify, restore or undo job (<c>LibraryJob</c>); poll about once per second (backend-api 1.12.0, feature 013).</summary>
public sealed record LibraryJobDto
{
    /// <summary>Job id.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Kind of job.</summary>
    public required LibraryJobKindDto Kind { get; init; }

    /// <summary>Status.</summary>
    public required LibraryJobStatusDto Status { get; init; }

    /// <summary>Current stage; null when queued or finished.</summary>
    public required LibraryJobStageDto? Stage { get; init; }

    /// <summary>Readable stage text (&lt;= 200 characters).</summary>
    public required string? StageText { get; init; }

    /// <summary>Progress 0..100.</summary>
    public required int Progress { get; init; }

    /// <summary>Bytes processed.</summary>
    public required long BytesDone { get; init; }

    /// <summary>Bytes to process.</summary>
    public required long BytesTotal { get; init; }

    /// <summary>e.g. "Song title - stems"; never a file path or personal content (&lt;= 300 characters).</summary>
    public required string? CurrentItem { get; init; }

    /// <summary>Time left; null in the first seconds and when finished.</summary>
    public required int? EtaSeconds { get; init; }

    /// <summary>File name of the archive.</summary>
    public required string? ArchiveFileName { get; init; }

    /// <summary>False once a restore closes the gate or the job is finished.</summary>
    public required bool Cancellable { get; init; }

    /// <summary>Report when completed.</summary>
    public required LibraryJobReportDto? Report { get; init; }

    /// <summary>Error when failed.</summary>
    public required LibraryJobErrorDto? Error { get; init; }

    /// <summary>When the job was accepted.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it started.</summary>
    public required DateTimeOffset? StartedAt { get; init; }

    /// <summary>When it finished.</summary>
    public required DateTimeOffset? FinishedAt { get; init; }
}
