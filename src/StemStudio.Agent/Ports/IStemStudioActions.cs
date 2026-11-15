namespace StemStudio.Agent.Ports;

/// <summary>
/// Every StemStudio action a server tool may take, grouped per feature. Implemented by the backend (LoopbackActions, T035)
/// with a typed HttpClient against <c>StemStudio__Agent__LoopbackBaseUrl</c>; the agent never touches the database, files or
/// the worker. 009/010 groups are added when those features are delivered.
/// </summary>
public interface IStemStudioActions
{
    /// <summary>Tracks, imports, detail separation and device (001-003).</summary>
    ITrackActions Tracks { get; }

    /// <summary>Mixes (001).</summary>
    IMixActions Mixes { get; }

    /// <summary>Artwork and band info (005).</summary>
    IArtistActions Artists { get; }

    /// <summary>Lyrics state without text (006).</summary>
    ILyricsActions Lyrics { get; }

    /// <summary>Exports and scores in the asset library (007, 008).</summary>
    IAssetActions Assets { get; }
}
