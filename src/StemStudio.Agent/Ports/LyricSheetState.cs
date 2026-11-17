namespace StemStudio.Agent.Ports;

/// <summary>A lyrics sheet as the lyric tools see it: never the text.</summary>
public sealed record LyricSheetState
{
    /// <summary>Sheet id.</summary>
    public required Guid LyricsId { get; init; }

    /// <summary>Current revision (the expected revision of the next request).</summary>
    public required int Revision { get; init; }

    /// <summary>Language code of the sheet.</summary>
    public required string Language { get; init; }

    /// <summary>Title; untrusted text (taints the run, 011 rule).</summary>
    public required string Title { get; init; }
}
