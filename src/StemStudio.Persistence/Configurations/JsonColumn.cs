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
}
