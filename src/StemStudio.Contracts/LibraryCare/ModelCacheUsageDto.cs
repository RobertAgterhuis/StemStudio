namespace StemStudio.Contracts.LibraryCare;

/// <summary>Size of one model cache folder (<c>ModelCacheUsage</c>); never removable in 013 (backend-api 1.12.0, feature 013).</summary>
public sealed record ModelCacheUsageDto
{
    /// <summary>Folder under the model cache, e.g. torch, hf, whisper, acestep, soulx.</summary>
    public required string Id { get; init; }

    /// <summary>Bytes.</summary>
    public required long Bytes { get; init; }

    /// <summary>Known model ids found in it.</summary>
    public required IReadOnlyList<string> Models { get; init; }
}
