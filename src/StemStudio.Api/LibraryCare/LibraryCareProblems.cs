using System.Globalization;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare;

/// <summary>Problem responses of library care (backend-api 1.12.0 problem types), each with its code.</summary>
public static class LibraryCareProblems
{
    /// <summary>409 <c>library-job-running</c>.</summary>
    /// <param name="active">The active job, if known.</param>
    /// <returns>Code and result.</returns>
    public static (string Code, IResult Result) JobRunning(LibraryJobDto? active) => Problem(
        StatusCodes.Status409Conflict, LibraryCareProblemTypes.LibraryJobRunning, "A library job is running", $"{KindText(active?.Kind)} is running; try again when it has finished.");

    /// <summary>409 <c>backups-folder-unavailable</c>.</summary>
    /// <param name="reason">Why, with the fix.</param>
    /// <returns>Code and result.</returns>
    public static (string Code, IResult Result) FolderUnavailable(string? reason) => Problem(
        StatusCodes.Status409Conflict, LibraryCareProblemTypes.BackupsFolderUnavailable, "The backups folder is not available", reason ?? "The backups folder is not available.");

    /// <summary>409 <c>insufficient-disk-space</c> with <c>requiredBytes</c> and <c>freeBytes</c>.</summary>
    /// <param name="requiredBytes">Bytes needed.</param>
    /// <param name="freeBytes">Bytes free.</param>
    /// <returns>Code and result.</returns>
    public static (string Code, IResult Result) InsufficientSpace(long requiredBytes, long freeBytes)
    {
        var detail = string.Create(CultureInfo.InvariantCulture, $"This needs about {Gb(requiredBytes)} GB; {Gb(freeBytes)} GB is free.");
        var text = new ProblemText(StatusCodes.Status409Conflict, LibraryCareProblemTypes.InsufficientDiskSpace, "Not enough free space", detail);
        return (text.Code, GateProblems.Typed(text, new Dictionary<string, object?> { ["requiredBytes"] = requiredBytes, ["freeBytes"] = freeBytes }));
    }

    /// <summary>409 <c>archive-in-use</c>.</summary>
    /// <returns>Code and result.</returns>
    public static (string Code, IResult Result) ArchiveInUse() => Problem(
        StatusCodes.Status409Conflict, LibraryCareProblemTypes.ArchiveInUse, "The archive is in use", "A library job reads or writes this archive; try again when it has finished.");

    /// <summary>409 <c>not-cancellable</c>.</summary>
    /// <returns>Code and result.</returns>
    public static (string Code, IResult Result) NotCancellable() => Problem(
        StatusCodes.Status409Conflict, LibraryCareProblemTypes.NotCancellable, "The job cannot be cancelled", "The job has finished or is switching the library.");

    /// <summary>422 <c>archive-invalid</c>.</summary>
    /// <param name="detail">Why.</param>
    /// <returns>Code and result.</returns>
    public static (string Code, IResult Result) ArchiveInvalid(string detail) => Problem(
        StatusCodes.Status422UnprocessableEntity, LibraryCareProblemTypes.ArchiveInvalid, "Not a StemStudio backup", detail);

    /// <summary>A typed problem with its code.</summary>
    /// <param name="status">HTTP status.</param>
    /// <param name="code">Problem code.</param>
    /// <param name="title">Title.</param>
    /// <param name="detail">Detail.</param>
    /// <returns>Code and result.</returns>
    public static (string Code, IResult Result) Problem(int status, string code, string title, string detail) => (code, GateProblems.Typed(status, code, title, detail));

    private static string KindText(LibraryJobKindDto? kind) => kind switch
    {
        LibraryJobKindDto.Verify => "A check of an archive",
        LibraryJobKindDto.Restore => "A restore",
        LibraryJobKindDto.UndoRestore => "An undo of a restore",
        _ => "A backup",
    };

    private static string Gb(long bytes) => (bytes / 1_000_000_000d).ToString("0.0", CultureInfo.InvariantCulture);
}
