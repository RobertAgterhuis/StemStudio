using System.Text.Json.Serialization;
namespace StemStudio.Contracts.Sections;

/// <summary>One section of a replace request (<c>SectionInput</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SectionInputDto
{
    /// <summary>Existing id to keep; null for a new section.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; init; }

    /// <summary>Functional kind.</summary>
    public required SectionKindDto Kind { get; init; }

    /// <summary>Label, 1..40 characters after trimming.</summary>
    public required string Label { get; init; }

    /// <summary>Start in seconds.</summary>
    public required double StartSeconds { get; init; }

    /// <summary>End in seconds.</summary>
    public required double EndSeconds { get; init; }

    /// <summary>Channel id of the solo instrument; only for <c>solo</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instrument { get; init; }
}
