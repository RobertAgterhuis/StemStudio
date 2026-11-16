namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// Default key ring directory of the non-Docker path (data-model 1): <c>%LOCALAPPDATA%\StemStudio\keys</c> on Windows,
/// <c>$XDG_DATA_HOME/stemstudio/keys</c> or <c>~/.local/share/stemstudio/keys</c> elsewhere. Docker sets <c>/keys</c>.
/// </summary>
public static class KeyRingDefaults
{
    /// <summary>The default for this process.</summary>
    /// <returns>The directory.</returns>
    public static string ForCurrentProcess() => Directory(OperatingSystem.IsWindows(), System.Environment.GetEnvironmentVariable);

    /// <summary>The default for a platform and an environment.</summary>
    /// <param name="windows">True on Windows.</param>
    /// <param name="variable">Reads an environment variable (null or blank = unset).</param>
    /// <returns>The directory.</returns>
    public static string Directory(bool windows, Func<string, string?> variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        if (windows)
        {
            return Path.Combine(Set(variable("LOCALAPPDATA")) ?? Folder(System.Environment.SpecialFolder.LocalApplicationData), "StemStudio", "keys");
        }

        return Set(variable("XDG_DATA_HOME")) is { } dataHome
            ? Path.Combine(dataHome, "stemstudio", "keys")
            : Path.Combine(Set(variable("HOME")) ?? Folder(System.Environment.SpecialFolder.UserProfile), ".local", "share", "stemstudio", "keys");
    }

    private static string? Set(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Folder(System.Environment.SpecialFolder folder) => System.Environment.GetFolderPath(folder);
}
