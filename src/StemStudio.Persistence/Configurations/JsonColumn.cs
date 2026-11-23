using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// A value stored as one JSON text column (enums by name). Values compare by their JSON form, so changes inside lists
/// and nested records are detected.
/// </summary>
/// <typeparam name="T">The stored value type.</typeparam>
internal static class JsonColumn<T>
    where T : class
{
    /// <summary>Converter between the value and its JSON text.</summary>
    public static ValueConverter<T, string> Converter { get; } = new(value => Serialize(value), json => Deserialize(json));

    /// <summary>
    /// Converter that reads a stored text that is not a value of the type as null (no value) instead of failing the whole
    /// query, and counts it in <see cref="StoredJsonFaults"/> (for derived data that can be made again, e.g. a quality report).
    /// </summary>
    public static ValueConverter<T, string> TolerantConverter { get; } = new(value => Serialize(value), json => ReadOrNull(json)!);

    /// <summary>Compares values by their JSON text.</summary>
    public static ValueComparer<T> Comparer { get; } = new(
        (left, right) => Serialize(left!) == Serialize(right!),
        value => Serialize(value).GetHashCode(StringComparison.Ordinal),
        value => Deserialize(Serialize(value)));

    /// <summary>Serializes a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>JSON text.</returns>
    public static string Serialize(T value) => JsonSerializer.Serialize(value, StoredJson.EnumsByName);

    /// <summary>Reads a value.</summary>
    /// <param name="json">JSON text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="JsonException">The text is not a value of the type.</exception>
    public static T Deserialize(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson.EnumsByName) ?? throw new JsonException($"The stored {typeof(T).Name} is null.");

    /// <summary>Reads a value, or null (counted as a fault) when the text is not a value of the type.</summary>
    /// <param name="json">JSON text.</param>
    /// <returns>The value or null.</returns>
    public static T? ReadOrNull(string json)
    {
        try
        {
            return Deserialize(json);
        }
        catch (JsonException)
        {
            StoredJsonFaults.Record(typeof(T).Name);
            return null;
        }
    }
}

/// <summary>
/// Stored JSON values that could not be read and were treated as missing (security review 020, finding 7). Process-wide: the
/// application logs the first one once (<see cref="TryClaimReport"/>); the row keeps its text until it is written again.
/// </summary>
public static class StoredJsonFaults
{
    private static int _count;
    private static int _reported;
    private static string? _first;

    /// <summary>How many unreadable values were read since start.</summary>
    public static int Count => Volatile.Read(ref _count);

    /// <summary>
    /// True exactly once per process, after the first unreadable value; <paramref name="column"/> names its type (e.g.
    /// <c>QualityReport</c>).
    /// </summary>
    /// <param name="column">The type of the first unreadable value.</param>
    /// <returns>True when the caller should log it.</returns>
    public static bool TryClaimReport(out string column)
    {
        column = Volatile.Read(ref _first) ?? string.Empty;
        return Count > 0 && Interlocked.CompareExchange(ref _reported, 1, 0) == 0;
    }

    /// <summary>Counts an unreadable value.</summary>
    /// <param name="column">Its type.</param>
    internal static void Record(string column)
    {
        Interlocked.CompareExchange(ref _first, column, null);
        Interlocked.Increment(ref _count);
    }
}
