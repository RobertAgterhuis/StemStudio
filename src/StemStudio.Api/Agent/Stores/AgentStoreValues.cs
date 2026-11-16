using System.Text.Json;

namespace StemStudio.Api.Agent.Stores;

/// <summary>Value conversions between the agent records and the persistence rows (enum names, UTC times, JSON columns).</summary>
internal static class AgentStoreValues
{
    /// <summary>JSON options of stored lists and payloads (camelCase, contract enum names).</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>Parses a stored enum name.</summary>
    /// <typeparam name="T">Enum type.</typeparam>
    /// <param name="value">Stored name.</param>
    /// <returns>The value.</returns>
    public static T Parse<T>(string value)
        where T : struct, Enum => Enum.Parse<T>(value);

    /// <summary>Parses an optional stored enum name.</summary>
    /// <typeparam name="T">Enum type.</typeparam>
    /// <param name="value">Stored name or null.</param>
    /// <returns>The value or null.</returns>
    public static T? ParseOptional<T>(string? value)
        where T : struct, Enum => value is null ? null : Enum.Parse<T>(value);

    /// <summary>UTC <see cref="DateTime"/> for storage.</summary>
    /// <param name="value">Time.</param>
    /// <returns>UTC time.</returns>
    public static DateTime Utc(DateTimeOffset value) => value.UtcDateTime;

    /// <summary>Optional UTC time for storage.</summary>
    /// <param name="value">Time or null.</param>
    /// <returns>UTC time or null.</returns>
    public static DateTime? Utc(DateTimeOffset? value) => value?.UtcDateTime;

    /// <summary>Stored UTC time as an offset time.</summary>
    /// <param name="value">Stored time.</param>
    /// <returns>Offset time (UTC).</returns>
    public static DateTimeOffset Offset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>Optional stored UTC time as an offset time.</summary>
    /// <param name="value">Stored time or null.</param>
    /// <returns>Offset time or null.</returns>
    public static DateTimeOffset? Offset(DateTime? value) => value is null ? null : Offset(value.Value);

    /// <summary>Serialises a value to JSON text.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>JSON text.</returns>
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);

    /// <summary>Reads JSON text.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="json">JSON text.</param>
    /// <returns>The value.</returns>
    public static T FromJson<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Json) ?? throw new JsonException($"The stored {typeof(T).Name} is null.");
}
