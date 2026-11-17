namespace StemStudio.Contracts.LibraryCare;

/// <summary>State of the backups folder (<c>FolderState</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record FolderStateDto
{
    /// <summary>The folder exists.</summary>
    public required bool Available { get; init; }

    /// <summary>The API can write into it.</summary>
    public required bool Writable { get; init; }

    /// <summary>Display-only host path (<c>StemStudio:BackupHostDirHint</c>, e.g. <c>./backups</c>; &lt;= 260 characters).</summary>
    public required string? HostPathHint { get; init; }

    /// <summary>Free space of the folder's disk; null when unknown.</summary>
    public required long? FreeBytes { get; init; }

    /// <summary>Size of the folder's disk; null when unknown.</summary>
    public required long? TotalBytes { get; init; }

    /// <summary>Why the folder is not available or not writable, with the fix (&lt;= 300 characters).</summary>
    public required string? Reason { get; init; }
}
