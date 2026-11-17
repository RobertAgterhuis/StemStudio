using System.Globalization;
using StemStudio.Api.Storage;

namespace StemStudio.Api.LibraryCare.Jobs;

/// <summary>
/// Folders of library care (data-model 2): <c>&lt;data&gt;/library-care/{jobs,journal,staging,previous,cleanup}</c> and the
/// backups folder. Every path is built from generated ids and validated archive names only.
/// </summary>
/// <param name="Data">The API data directory.</param>
/// <param name="BackupsDirectory">Absolute backups folder (<c>StemStudio:BackupDir</c>).</param>
public sealed record LibraryCarePaths(DataDirectory Data, string BackupsDirectory)
{
    /// <summary>Name of the library-care folder below the data directory.</summary>
    public const string FolderName = "library-care";

    /// <summary><c>library-care/</c>.</summary>
    public string Root => Data.Sub(FolderName);

    /// <summary><c>library-care/jobs/</c>.</summary>
    public string JobsDirectory => Path.Combine(Root, "jobs");

    /// <summary><c>library-care/journal/</c>.</summary>
    public string JournalDirectory => Path.Combine(Root, "journal");

    /// <summary><c>library-care/staging/</c>.</summary>
    public string StagingDirectory => Path.Combine(Root, "staging");

    /// <summary><c>library-care/previous/</c>.</summary>
    public string PreviousDirectory => Path.Combine(Root, "previous");

    /// <summary><c>library-care/cleanup/</c>.</summary>
    public string CleanupDirectory => Path.Combine(Root, "cleanup");

    /// <summary>Staging folder of a job.</summary>
    /// <param name="jobId">Job id.</param>
    /// <returns>The absolute path.</returns>
    public string Staging(Guid jobId) => Path.Combine(StagingDirectory, Id(jobId));

    /// <summary>Folder of a previous library.</summary>
    /// <param name="restoreId">Restore id.</param>
    /// <returns>The absolute path.</returns>
    public string Previous(Guid restoreId) => Path.Combine(PreviousDirectory, Id(restoreId));

    /// <summary>Journal file of a restore.</summary>
    /// <param name="restoreId">Restore id.</param>
    /// <returns>The absolute path.</returns>
    public string Journal(Guid restoreId) => Path.Combine(JournalDirectory, Id(restoreId) + ".json");

    /// <summary>Archive in the backups folder.</summary>
    /// <param name="fileName">A validated archive file name.</param>
    /// <returns>The absolute path.</returns>
    public string Archive(string fileName) => Path.Combine(BackupsDirectory, fileName);

    /// <summary>The lower-case D form of an id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The text.</returns>
    public static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);
}
