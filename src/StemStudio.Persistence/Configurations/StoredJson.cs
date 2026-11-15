using System.Text.Json;
using System.Text.Json.Serialization;

namespace StemStudio.Persistence.Configurations;

/// <summary>Serializer settings of hand-written JSON columns.</summary>
internal static class StoredJson
{
    /// <summary>Default settings with enums stored by name.</summary>
    public static JsonSerializerOptions EnumsByName { get; } = new() { Converters = { new JsonStringEnumConverter() } };
}
