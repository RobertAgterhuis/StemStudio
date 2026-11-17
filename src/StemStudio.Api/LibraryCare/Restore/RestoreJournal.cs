using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Jobs;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>A move of the switch.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JournalOp>))]
public enum JournalOp
{
    /// <summary>A live item moved into the previous library.</summary>
    MoveAside,

    /// <summary>A staged (or previous) item moved into the live library.</summary>
    MoveIn,
}

/// <summary>State of a journal.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JournalState>))]
public enum JournalState
{
    /// <summary>Planned, nothing moved.</summary>
    Staged,

    /// <summary>Moves in progress.</summary>
    Switching,

    /// <summary>Every move done.</summary>
    Committed,

    /// <summary>Moves undone after a failure or a crash.</summary>
    RolledBack,
}

/// <summary>One move; paths are relative to the data directory.</summary>
/// <param name="Op">The move kind.</param>
/// <param name="From">Source.</param>
/// <param name="To">Destination.</param>
/// <param name="Done">Recorded after the move.</param>
public sealed record JournalStep(JournalOp Op, string From, string To, bool Done);

/// <summary>The journal of a restore or undo (<c>library-care/journal/&lt;restoreId&gt;.json</c>, data-model 5.5).</summary>
public sealed record RestoreJournalRecord
{
    /// <summary>Id of the restore (also the previous library's folder).</summary>
    public required Guid RestoreId { get; init; }

    /// <summary><c>restore</c> or <c>undo</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The moves.</summary>
    public required IReadOnlyList<JournalStep> Steps { get; init; }

    /// <summary>State.</summary>
    public required JournalState State { get; init; }

    /// <summary>False for an empty library: the moved-aside files are deleted after the commit.</summary>
    public required bool KeepPrevious { get; init; }

    /// <summary>Folder (relative) removed after the commit, e.g. the emptied previous library of an undo.</summary>
    public string? RemoveAfterCommit { get; init; }
}

/// <summary>Builds and validates switch plans.</summary>
public static partial class JournalPlans
{
    /// <summary>Top-level items of a library.</summary>
    public static IReadOnlyList<string> Items { get; } = ["stemstudio.db", "stemstudio.db-wal", "stemstudio.db-shm", "tracks", "assets", "generations", "voices", "uploads"];

    /// <summary>The plan of a restore: live items aside into <c>previous/&lt;restoreId&gt;</c>, staged items in.</summary>
    /// <param name="restoreId">Restore id.</param>
    /// <param name="jobId">The job whose staging folder holds the library.</param>
    /// <param name="paths">Library-care folders.</param>
    /// <param name="keepPrevious">False for an empty library.</param>
    /// <returns>The journal.</returns>
    public static RestoreJournalRecord Restore(Guid restoreId, Guid jobId, LibraryCarePaths paths, bool keepPrevious)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var staging = $"library-care/staging/{LibraryCarePaths.Id(jobId)}";
        var aside = Aside(restoreId, paths);
        var staged = Items.Select(item => (From: item == "stemstudio.db" ? $"{staging}/stemstudio.db" : $"{staging}/files/{item}", To: item))
            .Where(move => Exists(paths, move.From))
            .Select(move => new JournalStep(JournalOp.MoveIn, move.From, move.To, false));
        return new RestoreJournalRecord { RestoreId = restoreId, Kind = "restore", Steps = [.. aside, .. staged], State = JournalState.Staged, KeepPrevious = keepPrevious };
    }

    /// <summary>The plan of an undo: the restored library aside into <c>previous/&lt;undoId&gt;</c>, the previous library in.</summary>
    /// <param name="previousId">The previous library.</param>
    /// <param name="undoId">Id of the undo (the new previous library).</param>
    /// <param name="paths">Library-care folders.</param>
    /// <returns>The journal.</returns>
    public static RestoreJournalRecord Undo(Guid previousId, Guid undoId, LibraryCarePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var previous = $"library-care/previous/{LibraryCarePaths.Id(previousId)}";
        var back = Items.Where(item => Exists(paths, $"{previous}/{item}")).Select(item => new JournalStep(JournalOp.MoveIn, $"{previous}/{item}", item, false));
        return new RestoreJournalRecord
        {
            RestoreId = undoId, Kind = "undo", Steps = [.. Aside(undoId, paths), .. back], State = JournalState.Staged, KeepPrevious = true, RemoveAfterCommit = previous,
        };
    }

    /// <summary>True for a top-level item or a path below <c>library-care/(staging|previous)/GUID</c>.</summary>
    /// <param name="path">A journal path.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsAllowedPath(string path) => Items.Contains(path, StringComparer.Ordinal) || CarePath().IsMatch(path);

    /// <summary>Refuses a journal with a path outside the allowed items.</summary>
    /// <param name="journal">The journal.</param>
    /// <returns>The journal.</returns>
    public static RestoreJournalRecord Validate(RestoreJournalRecord journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        var paths = journal.Steps.SelectMany(step => new[] { step.From, step.To }).Concat(journal.RemoveAfterCommit is { } extra ? [extra] : []);
        return paths.All(IsAllowedPath) ? journal : throw ArchiveReader.Unsafe("The restore journal names a path outside the library; nothing was moved.");
    }

    private static IEnumerable<JournalStep> Aside(Guid id, LibraryCarePaths paths) => Items.Where(item => Exists(paths, item))
        .Select(item => new JournalStep(JournalOp.MoveAside, item, $"library-care/previous/{LibraryCarePaths.Id(id)}/{item}", false));

    private static bool Exists(LibraryCarePaths paths, string relative) => Path.Exists(Full(paths, relative));

    /// <summary>The absolute path of a journal path.</summary>
    /// <param name="paths">Library-care folders.</param>
    /// <param name="relative">The journal path.</param>
    /// <returns>The absolute path.</returns>
    public static string Full(LibraryCarePaths paths, string relative)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.Data.Root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    [GeneratedRegex(@"\Alibrary-care/(staging|previous)/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(/(stemstudio\.db(-wal|-shm)?|files(/(tracks|assets|generations|voices|uploads))?|tracks|assets|generations|voices|uploads))?\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CarePath();
}

/// <summary>Journal files and their start-up recovery (research R-07): before <c>committed</c> every move is undone in reverse.</summary>
/// <param name="paths">Library-care folders.</param>
public sealed class RestoreJournal(LibraryCarePaths paths)
{
    /// <summary>Writes a journal (write-through + rename).</summary>
    /// <param name="journal">The journal.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(RestoreJournalRecord journal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(journal);
        Directory.CreateDirectory(paths.JournalDirectory);
        await JsonFiles.WriteAsync(paths.Journal(journal.RestoreId), journal, cancellationToken);
    }

    /// <summary>The absolute path of a journal path.</summary>
    /// <param name="relative">The journal path.</param>
    /// <returns>The absolute path.</returns>
    public string Full(string relative) => JournalPlans.Full(paths, relative);

    /// <summary>Reads a journal.</summary>
    /// <param name="restoreId">Restore id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The journal, or null.</returns>
    public Task<RestoreJournalRecord?> FindAsync(Guid restoreId, CancellationToken cancellationToken) =>
        JsonFiles.ReadAsync<RestoreJournalRecord>(paths.Journal(restoreId), cancellationToken);

    /// <summary>Rolls back every journal that did not commit.</summary>
    /// <param name="files">File seam.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The number of rolled-back journals.</returns>
    public async Task<int> RecoverAsync(ILibraryFiles files, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.JournalDirectory))
        {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(paths.JournalDirectory, "*.json"))
        {
            if (await JsonFiles.ReadAsync<RestoreJournalRecord>(file, cancellationToken) is { State: JournalState.Staged or JournalState.Switching } journal)
            {
                await RollBackAsync(JournalPlans.Validate(journal), files, cancellationToken);
                count++;
            }
        }

        return count;
    }

    /// <summary>Undoes every move whose destination exists and source does not, in reverse (idempotent).</summary>
    /// <param name="journal">The journal.</param>
    /// <param name="files">File seam.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task RollBackAsync(RestoreJournalRecord journal, ILibraryFiles files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(files);
        foreach (var step in journal.Steps.Reverse())
        {
            var (from, to) = (JournalPlans.Full(paths, step.From), JournalPlans.Full(paths, step.To));
            if (!Path.Exists(from) && Path.Exists(to))
            {
                files.Move(to, from);
            }
        }

        await SaveAsync(journal with { State = JournalState.RolledBack }, cancellationToken);
    }
}
