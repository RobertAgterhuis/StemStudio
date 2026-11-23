namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One chord of a chart (<c>ChordSegment</c>) (backend-api 1.15.0, feature 016, data-model 2.2).</summary>
public sealed record ChordSegmentDto
{
    /// <summary>Start in ms.</summary>
    public required int StartMs { get; init; }

    /// <summary>End in ms (&gt; start).</summary>
    public required int EndMs { get; init; }

    /// <summary>Harte label (<c>N</c> = no chord, <c>X</c> = unknown), at most 32 characters.</summary>
    public required string Label { get; init; }

    /// <summary>Root pitch class; null for <c>N</c>/<c>X</c>.</summary>
    public required int? Root { get; init; }

    /// <summary>Quality; null for <c>N</c>/<c>X</c>.</summary>
    public required ChordQualityDto? Quality { get; init; }

    /// <summary>Bass pitch class when it is not the root, else null.</summary>
    public required int? Bass { get; init; }

    /// <summary>Confidence 0..1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Changed by the user (FR-015).</summary>
    public required bool Edited { get; init; }
}
