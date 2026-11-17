using System.Text.Json;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Jobs;

/// <summary>
/// Library job records as JSON files in <c>library-care/jobs/</c> (data-model 5.1): outside the database, so they survive a
/// restore that replaces it. The last <see cref="LibraryCareLimits.KeptLibraryJobs"/> records are kept.
/// </summary>
/// <param name="paths">Library-care folders.</param>
public sealed class LibraryJobStore(LibraryCarePaths paths) : IDisposable
{
    /// <summary>JSON options of every library-care record (web defaults: camelCase).</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();

    /// <summary>Writes a record (temp file + rename) and prunes older records.</summary>
    /// <param name="job">The job.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(LibraryJobDto job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(paths.JobsDirectory);
            await JsonFiles.WriteAsync(PathOf(job.JobId), job, cancellationToken);
            foreach (var stale in (await ListAsync(cancellationToken)).Skip(LibraryCareLimits.KeptLibraryJobs))
            {
                File.Delete(PathOf(stale.JobId));
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Reads one record.</summary>
    /// <param name="jobId">Job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The job, or null when unknown or unreadable.</returns>
    public Task<LibraryJobDto?> FindAsync(Guid jobId, CancellationToken cancellationToken) =>
        JsonFiles.ReadAsync<LibraryJobDto>(PathOf(jobId), cancellationToken);

    /// <summary>Every readable record, newest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The jobs.</returns>
    public async Task<IReadOnlyList<LibraryJobDto>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.JobsDirectory))
        {
            return [];
        }

        var jobs = new List<LibraryJobDto>();
        foreach (var file in Directory.EnumerateFiles(paths.JobsDirectory, "*.json"))
        {
            if (await JsonFiles.ReadAsync<LibraryJobDto>(file, cancellationToken) is { } job)
            {
                jobs.Add(job);
            }
        }

        return [.. jobs.OrderByDescending(job => job.CreatedAt)];
    }

    private string PathOf(Guid jobId) => Path.Combine(paths.JobsDirectory, LibraryCarePaths.Id(jobId) + ".json");
}

/// <summary>Atomic JSON files of library care (temp file + rename, write-through).</summary>
public static class JsonFiles
{
    /// <summary>Writes a value atomically.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="path">Target path.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, value, LibraryJobStore.Json, cancellationToken);
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Reads a value; a missing or unreadable file is null.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="path">The path.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The value, or null.</returns>
    public static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return await JsonSerializer.DeserializeAsync<T>(stream, LibraryJobStore.Json, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            return null;
        }
    }
}
