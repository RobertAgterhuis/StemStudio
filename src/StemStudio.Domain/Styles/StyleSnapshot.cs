using System.Text.Json.Serialization;

namespace StemStudio.Domain.Styles;

/// <summary>A style as it was when a generation was created (kept when the style changes or is deleted).</summary>
/// <param name="StyleId">The style's identifier.</param>
/// <param name="Kind">Preset or user.</param>
/// <param name="PresetKey">The preset key, if any.</param>
/// <param name="Fields">The fields at creation.</param>
public sealed record StyleSnapshot(Guid StyleId, StyleKind Kind, string? PresetKey, StyleFields Fields)
{
    /// <summary>The style name.</summary>
    [JsonIgnore]
    public string Name => Fields.Name;
}
