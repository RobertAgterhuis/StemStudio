namespace StemStudio.Domain.Tracks;

/// <summary>Where a track picture came from (feature 005, data-model 2.2). The order is the precedence rank (FR-005).</summary>
public enum ArtworkSource
{
    /// <summary>A picture chosen by the user (always shown when set).</summary>
    User = 0,

    /// <summary>The Spotify album image.</summary>
    Spotify = 1,

    /// <summary>The cover embedded in an uploaded file.</summary>
    Embedded = 2,

    /// <summary>The release group front from the Cover Art Archive.</summary>
    CoverArtArchive = 3,

    /// <summary>The YouTube video thumbnail.</summary>
    Youtube = 4,
}
