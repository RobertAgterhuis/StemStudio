namespace StemStudio.Contracts.Library;

/// <summary>Folder art (backend-api 1.22.0 <c>FolderArt</c>): song = track picture, album = best picture (005 precedence), artist = up to 4 album covers.</summary>
public sealed record FolderArtDto
{
    /// <summary>Cover, mosaic or placeholder.</summary>
    public required FolderArtKindDto Kind { get; init; }

    /// <summary>1 image for a cover, 2..4 for a mosaic, none for a placeholder.</summary>
    public IReadOnlyList<ArtImageDto> Images { get; init; } = [];

    /// <summary>The placeholder (also the fallback when an image fails to load).</summary>
    public required ArtPlaceholderDto Placeholder { get; init; }
}
