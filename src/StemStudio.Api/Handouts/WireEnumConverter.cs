using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using StemStudio.Contracts.Handouts;

namespace StemStudio.Api.Handouts;

/// <summary>
/// Reads a contract enum only by its wire name (<see cref="JsonStringEnumMemberNameAttribute"/>, case-sensitive). A number,
/// an unknown name or any other token reads as <see cref="Undefined"/>, which the request rules refuse with 422, so a client
/// can never send a member that the contract does not define. Writes the wire name.
/// </summary>
/// <typeparam name="T">The contract enum.</typeparam>
public sealed class WireEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    /// <summary>The value of a token that is not a wire name (never a defined member).</summary>
    public static readonly T Undefined = (T)Enum.ToObject(typeof(T), int.MinValue);

    private static readonly Dictionary<string, T> ByName = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToDictionary(field => field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name, field => (T)field.GetValue(null)!, StringComparer.Ordinal);

    private static readonly Dictionary<T, string> ByValue = ByName.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return ByName.TryGetValue(reader.GetString()!, out var value) ? value : Undefined;
        }

        // The serializer buffers a converter's whole value before Read, so TrySkip always completes (Skip would throw on a
        // request body that is read in parts).
        _ = reader.TrySkip();
        return Undefined;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(ByValue[value]);
    }
}

/// <summary>Registers the strict readers of the enums a client sends in <c>createHandout</c> (security review 020, finding 1).</summary>
public static class HandoutJson
{
    /// <summary>Adds the converters (they take precedence over the contract's type-level converters).</summary>
    /// <param name="options">The API's serializer options.</param>
    public static void AddTo(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new WireEnumConverter<HandoutFamilyDto>());
        options.Converters.Add(new WireEnumConverter<IngredientCodeDto>());
        options.Converters.Add(new WireEnumConverter<TranspositionDto>());
        options.Converters.Add(new WireEnumConverter<PaperDto>());
    }
}
