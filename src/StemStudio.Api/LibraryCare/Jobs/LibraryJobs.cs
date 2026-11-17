using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Jobs;

/// <summary>The work of a library job: returns the report or throws (<see cref="LibraryJobException"/>, cancellation).</summary>
/// <param name="context">Progress reporting.</param>
/// <param name="cancellationToken">Cancelled by <see cref="LibraryJobRunner.CancelAsync"/> or shutdown.</param>
/// <returns>The report.</returns>
public delegate Task<LibraryJobReportDto> LibraryJobWork(LibraryJobContext context, CancellationToken cancellationToken);

/// <summary>A progress report of a running library job.</summary>
/// <param name="Stage">Stage.</param>
/// <param name="StageText">Readable stage text (no file paths).</param>
/// <param name="Progress">Overall progress 0..100.</param>
/// <param name="BytesDone">Bytes processed.</param>
/// <param name="BytesTotal">Bytes to process.</param>
/// <param name="CurrentItem">Readable current item, e.g. "Song - stems"; never a path.</param>
public sealed record LibraryJobProgress(LibraryJobStageDto Stage, string StageText, int Progress, long BytesDone, long BytesTotal, string? CurrentItem);

/// <summary>A library job failed with a contract error code (message without file paths).</summary>
public sealed class LibraryJobException : Exception
{
    /// <summary>Creates the exception (<c>io-error</c>).</summary>
    public LibraryJobException()
        : this(LibraryJobErrorCodeDto.IoError, "The library job failed.")
    {
    }

    /// <summary>Creates the exception (<c>io-error</c>).</summary>
    /// <param name="message">Readable message.</param>
    public LibraryJobException(string message)
        : this(LibraryJobErrorCodeDto.IoError, message)
    {
    }

    /// <summary>Creates the exception (<c>io-error</c>).</summary>
    /// <param name="message">Readable message.</param>
    /// <param name="innerException">Cause.</param>
    public LibraryJobException(string message, Exception innerException)
        : base(message, innerException) => Code = LibraryJobErrorCodeDto.IoError;

    /// <summary>Creates the exception.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Readable message.</param>
    public LibraryJobException(LibraryJobErrorCodeDto code, string message)
        : base(message) => Code = code;

    /// <summary>Creates the exception.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Readable message.</param>
    /// <param name="innerException">Cause.</param>
    public LibraryJobException(LibraryJobErrorCodeDto code, string message, Exception innerException)
        : base(message, innerException) => Code = code;

    /// <summary>The error code.</summary>
    public LibraryJobErrorCodeDto Code { get; }
}

/// <summary>Factories and transitions of <see cref="LibraryJobDto"/> records.</summary>
public static class LibraryJobs
{
    /// <summary>Text of a job interrupted by an API restart.</summary>
    public const string InterruptedText = "StemStudio was stopped while this job ran; nothing was changed. Start it again.";

    /// <summary>Text of an unexpected failure (never the exception text: no paths).</summary>
    public const string UnexpectedText = "The library job failed because of a file error; nothing was changed. Try again.";

    /// <summary>A new queued job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="archiveFileName">Archive name.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The job.</returns>
    public static LibraryJobDto Queued(Guid jobId, LibraryJobKindDto kind, string? archiveFileName, DateTimeOffset now) => new()
    {
        JobId = jobId,
        Kind = kind,
        Status = LibraryJobStatusDto.Queued,
        Stage = null,
        StageText = null,
        Progress = 0,
        BytesDone = 0,
        BytesTotal = 0,
        CurrentItem = null,
        EtaSeconds = null,
        ArchiveFileName = archiveFileName,
        Cancellable = true,
        Report = null,
        Error = null,
        CreatedAt = now,
        StartedAt = null,
        FinishedAt = null,
    };

    /// <summary>True for completed, failed and cancelled.</summary>
    /// <param name="job">The job.</param>
    /// <returns>True when terminal.</returns>
    public static bool IsTerminal(LibraryJobDto job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Status is LibraryJobStatusDto.Completed or LibraryJobStatusDto.Failed or LibraryJobStatusDto.Cancelled;
    }

    /// <summary>A terminal copy of the job.</summary>
    /// <param name="job">The job.</param>
    /// <param name="status">Terminal status.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The finished job.</returns>
    public static LibraryJobDto Finished(LibraryJobDto job, LibraryJobStatusDto status, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job with { Status = status, Stage = null, StageText = null, CurrentItem = null, EtaSeconds = null, Cancellable = false, FinishedAt = now };
    }

    /// <summary>A failed copy of the job.</summary>
    /// <param name="job">The job.</param>
    /// <param name="code">Error code.</param>
    /// <param name="message">Readable message.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The failed job; the stage is kept so the UI can say where it stopped.</returns>
    public static LibraryJobDto Failed(LibraryJobDto job, LibraryJobErrorCodeDto code, string message, DateTimeOffset now) =>
        Finished(job, LibraryJobStatusDto.Failed, now) with { Stage = job.Stage, Error = new LibraryJobErrorDto { Code = code, Message = message } };
}
