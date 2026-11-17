namespace StemStudio.Domain.Library;

/// <summary>Level of a library folder (feature 022, data-model 2.1).</summary>
public enum FolderLevel
{
    /// <summary>An artist folder.</summary>
    Artist,

    /// <summary>An album folder inside an artist.</summary>
    Album,

    /// <summary>A song folder inside an album; it holds the items.</summary>
    Song,
}
