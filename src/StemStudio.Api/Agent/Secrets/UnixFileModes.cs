using System.Runtime.Versioning;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>Reads and sets Unix file modes (seam for the key ring permission check, research R-04).</summary>
public interface IUnixFileModes
{
    /// <summary>False on Windows, where Unix modes do not exist (the per-user ACL of <c>%LOCALAPPDATA%</c> applies).</summary>
    bool Supported { get; }

    /// <summary>The mode of a directory.</summary>
    /// <param name="path">Directory path.</param>
    /// <returns>The mode.</returns>
    UnixFileMode GetMode(string path);

    /// <summary>Sets the mode of a directory; throws <see cref="UnauthorizedAccessException"/> when the process does not own it.</summary>
    /// <param name="path">Directory path.</param>
    /// <param name="mode">New mode.</param>
    void SetMode(string path, UnixFileMode mode);
}

/// <summary>The platform's <see cref="IUnixFileModes"/>.</summary>
public static class UnixFileModes
{
    /// <summary>The real modes on Linux/macOS; an unsupported stub on Windows.</summary>
    /// <returns>The implementation for this process.</returns>
    public static IUnixFileModes ForCurrentPlatform() => OperatingSystem.IsWindows() ? new NoUnixFileModes() : new FileSystemUnixFileModes();

    private sealed class NoUnixFileModes : IUnixFileModes
    {
        public bool Supported => false;

        public UnixFileMode GetMode(string path) => throw new PlatformNotSupportedException();

        public void SetMode(string path, UnixFileMode mode) => throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform("windows")]
    private sealed class FileSystemUnixFileModes : IUnixFileModes
    {
        public bool Supported => true;

        public UnixFileMode GetMode(string path) => File.GetUnixFileMode(path);

        public void SetMode(string path, UnixFileMode mode) => File.SetUnixFileMode(path, mode);
    }
}
