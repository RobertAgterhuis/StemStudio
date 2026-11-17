using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>The previous library (<c>library-care/previous/&lt;restoreId&gt;/previous.json</c>, data-model 5.6); at most one.</summary>
/// <param name="paths">Library-care folders.</param>
public sealed class PreviousLibraryStore(LibraryCarePaths paths) : IPreviousLibraryReader
{
    private const string RecordName = "previous.json";

    /// <inheritdoc />
    public async Task<PreviousLibraryDto?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.PreviousDirectory))
        {
            return null;
        }

        foreach (var folder in Directory.EnumerateDirectories(paths.PreviousDirectory))
        {
            if (await JsonFiles.ReadAsync<PreviousLibraryDto>(Path.Combine(folder, RecordName), cancellationToken) is { } previous)
            {
                return previous;
            }
        }

        return null;
    }

    /// <summary>Writes the record of a previous library.</summary>
    /// <param name="previous">The record.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(PreviousLibraryDto previous, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var folder = paths.Previous(previous.RestoreId);
        Directory.CreateDirectory(folder);
        await JsonFiles.WriteAsync(Path.Combine(folder, RecordName), previous, cancellationToken);
    }

    /// <summary>Deletes a previous library permanently.</summary>
    /// <param name="restoreId">Its id.</param>
    public void Delete(Guid restoreId)
    {
        var folder = paths.Previous(restoreId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Bytes of a previous library's files.</summary>
    /// <param name="restoreId">Its id.</param>
    /// <returns>The size.</returns>
    public long SizeOf(Guid restoreId)
    {
        var folder = new DirectoryInfo(paths.Previous(restoreId));
        return folder.Exists ? folder.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).Sum(file => file.Length) : 0;
    }
}
