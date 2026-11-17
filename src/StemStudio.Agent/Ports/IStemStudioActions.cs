namespace StemStudio.Agent.Ports;

/// <summary>
/// Every StemStudio action a server tool may take, grouped per feature. Implemented by the backend (LoopbackActions, T035)
/// with a typed HttpClient against <c>StemStudio__Agent__LoopbackBaseUrl</c>; the agent never touches the database, files or
/// the worker. One group per feature area of the tool catalogue 1.0.0.
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

    /// <summary>Style library (009).</summary>
    IStyleActions Styles { get; }

    /// <summary>Generation status, restyles and generations (009, 010).</summary>
    IGenerationActions Generations { get; }

    /// <summary>Own lyrics sheets (without text) and songs (010).</summary>
    ISongActions Songs { get; }

    /// <summary>Own-voice profiles (read only) and voice conversions (010).</summary>
    IVoiceActions Voices { get; }

    /// <summary>Library folders, live search and library info (022, tool catalogue 1.1.0).</summary>
    ILibraryActions Library { get; }
}
