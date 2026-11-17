using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Mixes;

/// <summary>A-B loop region in seconds (spec FR-013). Requires 0 &lt;= Start &lt; End &lt;= track duration.</summary>
/// <param name="Start">Loop start (A) in seconds.</param>
/// <param name="End">Loop end (B) in seconds.</param>
/// <param name="Enabled">Whether looping is active.</param>
/// <param name="SectionId">
/// Backend-api 1.13.0 (feature 014): the section the loop was set from (one-click loop), cleared by the UI when A or B is moved by
/// hand; null otherwise. An id that no longer exists is read back as null (the loop stays). Not written when null, so 1.12.0
/// clients see the same JSON as before.
/// </param>
public sealed record LoopRegionDto(
    double Start,
    double End,
    bool Enabled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? SectionId = null);
