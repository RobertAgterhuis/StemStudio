using System.Text.Json.Serialization;
namespace StemStudio.Contracts.Practice;

/// <summary>Time in one section of a session (<c>PracticeSectionInput</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeSectionInputDto
{
    /// <summary>Section id at recording time, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? SectionId { get; init; }

    /// <summary>Kind snapshot (<c>none</c> outside sections).</summary>
    public required PracticeSectionKindDto Kind { get; init; }

    /// <summary>Label snapshot (0..40 characters).</summary>
    public required string Label { get; init; }

    /// <summary>Played seconds in this section.</summary>
    public required double PlayedSeconds { get; init; }

    /// <summary>Completed loop passes of this section.</summary>
    public int LoopPasses { get; init; }

    /// <summary>Highest speed of a full pass with the trainer on (50..120), or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TrainerReachedPercent { get; init; }
}
