using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// JSON text form of the immutable <see cref="DetailPhase"/> snapshot (column <c>Tracks.DetailPhase</c>, null = never
/// requested). Enums are stored by name, timestamps as UTC ISO 8601.
/// </summary>
public static class DetailPhaseJson
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Converter between the snapshot and its JSON text (EF Core never passes null to a converter).</summary>
    public static ValueConverter<DetailPhase?, string> Converter { get; } = new(phase => Serialize(phase!), json => Deserialize(json));

    /// <summary>Compares snapshots by their JSON form (list members compare by content, not by reference).</summary>
    public static ValueComparer<DetailPhase?> Comparer { get; } = new(
        (left, right) => SerializeOrNull(left) == SerializeOrNull(right),
        phase => (SerializeOrNull(phase) ?? string.Empty).GetHashCode(StringComparison.Ordinal),
        phase => phase);

    /// <summary>Serializes a snapshot.</summary>
    /// <param name="phase">The snapshot.</param>
    /// <returns>JSON text.</returns>
    public static string Serialize(DetailPhase phase) => JsonSerializer.Serialize(phase, Options);

    /// <summary>Reads a snapshot.</summary>
    /// <param name="json">JSON text written by <see cref="Serialize"/>.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="JsonException">The text is not a detail phase.</exception>
    public static DetailPhase Deserialize(string json) =>
        JsonSerializer.Deserialize<DetailPhase>(json, Options) ?? throw new JsonException("The stored detail phase is null.");

    private static string? SerializeOrNull(DetailPhase? phase) => phase is null ? null : Serialize(phase);
}
