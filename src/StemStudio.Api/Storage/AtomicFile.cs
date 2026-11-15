namespace StemStudio.Api.Storage;

/// <summary>Writes a file under a temporary name and renames it into place, so readers never see partial files.</summary>
internal static class AtomicFile
{
    /// <summary>Writes through <paramref name="tempPath"/>, then moves it to the target; the temp file never survives.</summary>
    /// <typeparam name="T">Result of the write.</typeparam>
    /// <param name="tempPath">Temporary path in the target directory (generated, unique).</param>
    /// <param name="write">Writes the content.</param>
    /// <param name="target">The final path, which may depend on the result (e.g. a content hash).</param>
    /// <returns>The write result.</returns>
    public static async Task<T> WriteAsync<T>(string tempPath, Func<FileStream, Task<T>> write, Func<T, string> target)
    {
        try
        {
            var result = await WriteTempAsync(tempPath, write);
            File.Move(tempPath, target(result), overwrite: true);
            return result;
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>Creates the directory, writes the content through a generated temp file and returns the file size.</summary>
    /// <param name="path">Final path (built from validated names only).</param>
    /// <param name="write">Writes the content into the given stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file size in bytes.</returns>
    public static async Task<long> SaveAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return await WriteAsync($"{path}.{Guid.NewGuid():N}.tmp", WriteThenMeasure, _ => path);

        async Task<long> WriteThenMeasure(FileStream stream)
        {
            await write(stream, cancellationToken);
            return stream.Length;
        }
    }

    private static async Task<T> WriteTempAsync<T>(string tempPath, Func<FileStream, Task<T>> write)
    {
        await using var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        return await write(stream);
    }
}
