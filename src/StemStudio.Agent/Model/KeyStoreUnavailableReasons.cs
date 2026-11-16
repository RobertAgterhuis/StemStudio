namespace StemStudio.Agent.Model;

/// <summary>Codes of an unusable key ring directory (data-model 5); rendered in words, never with a stack trace.</summary>
public static class KeyStoreUnavailableReasons
{
    /// <summary>The directory is missing and cannot be created, or is not writable.</summary>
    public const string NotWritable = "not-writable";

    /// <summary>The directory is inside <c>StemStudio:DataDir</c> (the library).</summary>
    public const string InsideLibrary = "inside-library";

    /// <summary>The directory is inside <c>StemStudio:BackupDir</c>.</summary>
    public const string InsideBackups = "inside-backups";

    /// <summary>The directory is inside the content root of the API.</summary>
    public const string InsideApp = "inside-app";

    /// <summary>The directory is inside a git work tree.</summary>
    public const string InsideRepository = "inside-repository";
}
