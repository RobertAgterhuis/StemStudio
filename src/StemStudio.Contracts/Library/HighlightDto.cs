using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Library;

/// <summary>A highlighted range (backend-api 1.22.0 <c>Highlight</c>): UTF-16 offsets in the displayed text of a field.</summary>
public sealed record HighlightDto
{
    /// <summary>The field.</summary>
    public required SearchFieldDto Field { get; init; }

    /// <summary>UTF-16 offset in the displayed text (folder name, item name or instrument label).</summary>
    public required int Start { get; init; }

    /// <summary>Length in UTF-16 code units (at least 1).</summary>
    public required int Length { get; init; }

    /// <summary>For <see cref="SearchFieldDto.Instrument"/>: the label the offsets refer to; absent otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }
}
