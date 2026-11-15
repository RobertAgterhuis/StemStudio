using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// JSON text form of the immutable <see cref="JobDevice"/> record (column <c>Tracks.Device</c>, null = unknown; feature
/// 003). The kind is stored by name. The record compares by value, so EF Core's default comparer detects changes.
/// </summary>
public static class JobDeviceJson
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Converter between the record and its JSON text (EF Core never passes null to a converter).</summary>
    public static ValueConverter<JobDevice?, string> Converter { get; } = new(device => Serialize(device!), json => Deserialize(json));

    /// <summary>Serializes a record.</summary>
    /// <param name="device">The record.</param>
    /// <returns>JSON text.</returns>
    public static string Serialize(JobDevice device) => JsonSerializer.Serialize(device, Options);

    /// <summary>Reads a record; the domain rules are checked again by the constructor.</summary>
    /// <param name="json">JSON text written by <see cref="Serialize"/>.</param>
    /// <returns>The record.</returns>
    /// <exception cref="JsonException">The text is not a device record.</exception>
    public static JobDevice Deserialize(string json) =>
        JsonSerializer.Deserialize<JobDevice>(json, Options) ?? throw new JsonException("The stored device is null.");
}
