namespace StemStudio.Contracts.LibraryCare;

/// <summary>Whether the worker part of the storage overview is available (<c>StorageOverview.worker</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record WorkerStorageStateDto
{
    /// <summary>The worker answered within 2 s.</summary>
    public required bool Available { get; init; }

    /// <summary>Why not.</summary>
    public required string? Reason { get; init; }
}
