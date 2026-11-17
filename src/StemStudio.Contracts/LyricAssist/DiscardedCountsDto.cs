namespace StemStudio.Contracts.LyricAssist;

/// <summary>Model items that were not shown, per reason (schema <c>DiscardedCounts</c>, Expert log).</summary>
public sealed record DiscardedCountsDto
{
    /// <summary>Nothing discarded.</summary>
    public static DiscardedCountsDto None { get; } = new();

    /// <summary>The anchor did not match the text.</summary>
    public int InvalidAnchor { get; init; }

    /// <summary>The item did not match the output schema.</summary>
    public int InvalidSchema { get; init; }

    /// <summary>A rhyme or syllable claim failed verification.</summary>
    public int FailedVerification { get; init; }

    /// <summary>Removed by sensitivity, intentional marks or personal words.</summary>
    public int Filtered { get; init; }

    /// <summary>Withheld because too close to library lyrics (shown to every level).</summary>
    public int Verbatim { get; init; }
}
