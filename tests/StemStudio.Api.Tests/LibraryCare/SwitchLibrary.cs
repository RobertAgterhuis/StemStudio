using Microsoft.Data.Sqlite;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Storage;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>A temporary data directory with a live library and a staged restore of <paramref name="jobId"/> (switch tests).</summary>
/// <param name="jobId">The restore job.</param>
internal sealed class SwitchLibrary(Guid jobId) : IDisposable
{
    private readonly DirectoryInfo _root = CreateRoot(jobId);

    public LibraryCarePaths Paths => new(new DataDirectory(System.IO.Path.Combine(_root.FullName, "data")), System.IO.Path.Combine(_root.FullName, "backups"));

    public string Path(string relative) => System.IO.Path.Combine(_root.FullName, "data", relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public string Read(string relative) => File.ReadAllText(Path(relative));

    public void Write(string relative, string text) => Write(_root, relative, text);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _root.Delete(recursive: true);
    }

    private static DirectoryInfo CreateRoot(Guid jobId)
    {
        var root = Directory.CreateTempSubdirectory("stemstudio-switch-");
        Write(root, "stemstudio.db", "live db");
        Write(root, "tracks/a/stems/vocals.flac", "live stem");
        Write(root, "assets/x/audio.flac", "live asset");
        Write(root, $"library-care/staging/{jobId}/stemstudio.db", "restored db");
        Write(root, $"library-care/staging/{jobId}/files/tracks/b/stems/drums.flac", "restored stem");
        return root;
    }

    private static void Write(DirectoryInfo root, string relative, string text)
    {
        var path = System.IO.Path.Combine(root.FullName, "data", relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
