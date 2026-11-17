using System.Text.Json.Serialization;
namespace StemStudio.Contracts.Sections;

/// <summary>One section of a track; times in track seconds (backend-api 1.13.0, feature 014).</summary>
public sealed record SectionDto
{
    /// <summary>Section id, kept across edits.</summary>
    public required Guid Id { get; init; }

    /// <summary>Functional kind.</summary>
    public required SectionKindDto Kind { get; init; }

    /// <summary>Display label, 1..40 characters ("Verse 2", "Guitar solo").</summary>
    public required string Label { get; init; }

    /// <summary>Start in seconds (>= 0).</summary>
    public required double StartSeconds { get; init; }

    /// <summary>End in seconds (> start, at least 1 s later).</summary>
    public required double EndSeconds { get; init; }

    /// <summary>1-based bar of the start when the track has a reliable grid; else null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartBar { get; init; }

    /// <summary>Channel id of the solo instrument (<c>solo</c> only), else null.</summary>
    public required string? Instrument { get; init; }

    /// <summary>0..1 for detected sections; null for user sections.</summary>
    public required double? Confidence { get; init; }

    /// <summary>Detected or user.</summary>
    public required SectionOriginDto Origin { get; init; }
}
