using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>The normalised export request, stored with the item and used by retry (backend contract 1.5.0).</summary>
public sealed record ExportRecipeDto
{
    /// <summary>The exported track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Raw stems or the mix.</summary>
    public required ExportModeDto Mode { get; init; }

    /// <summary>Explicit channel ids, or null = every audible channel.</summary>
    public IReadOnlyList<string>? Channels { get; init; }

    /// <summary>Snapshot of the mix used (mode mix).</summary>
    public ExportMixDto? Mix { get; init; }

    /// <summary>Exported range, or null for the whole track.</summary>
    public RangeDto? Range { get; init; }

    /// <summary>Output format with defaults filled in.</summary>
    public required ExportFormatDto Format { get; init; }

    /// <summary>The take the export used (1.14.0, amended in round 014+015); absent on the wire when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TakeId { get; init; }
}
