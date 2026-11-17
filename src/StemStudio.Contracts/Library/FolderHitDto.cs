namespace StemStudio.Contracts.Library;

/// <summary>A folder search hit (backend-api 1.22.0 <c>FolderHit</c>).</summary>
public sealed record FolderHitDto
{
    /// <summary>The folder.</summary>
    public required LibraryFolderDto Folder { get; init; }

    /// <summary>Parents (artist for an album; artist and album for a song).</summary>
    public IReadOnlyList<FolderRefDto> Path { get; init; } = [];

    /// <summary>Highlights on the folder name (at most 32).</summary>
    public IReadOnlyList<HighlightDto> Highlights { get; init; } = [];
}
