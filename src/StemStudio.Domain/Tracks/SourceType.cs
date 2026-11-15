namespace StemStudio.Domain.Tracks;

/// <summary>Origin of the audio of a track.</summary>
public enum SourceType
{
    /// <summary>YouTube video (<c>youtube:&lt;videoId&gt;</c>).</summary>
    Youtube,

    /// <summary>Spotify track matched on YouTube (<c>spotify:&lt;trackId&gt;</c>).</summary>
    Spotify,

    /// <summary>Uploaded file (<c>upload:&lt;sha256&gt;</c>).</summary>
    Upload,
}
