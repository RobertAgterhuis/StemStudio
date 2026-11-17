using System.Text.Json;
using System.Text.Json.Nodes;

namespace StemStudio.Web.Practice;

/// <summary>JSON form of override chords (014 data-model 5.1): <c>{key, mod?, shift?, layer?}</c>, optional fields omitted.</summary>
public static class ShortcutJson
{
    /// <summary>Reads one chord; null when the shape is invalid (key 1..20 lower-case characters, optional booleans, layer keys/pedal).</summary>
    /// <param name="value">The JSON value.</param>
    /// <returns>The chord, or null.</returns>
    public static ShortcutChord? ReadChord(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || Key(value) is not { } key)
        {
            return null;
        }

        var (mod, shift, layer) = (OptionalBool(value, "mod"), OptionalBool(value, "shift"), Layer(value));
        return mod.Valid && shift.Valid && layer is { } valid ? new ShortcutChord(key, mod.Value == true, shift.Value, valid) : null;
    }

    /// <summary>Writes one chord.</summary>
    /// <param name="chord">The chord.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject ChordNode(ShortcutChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var node = new JsonObject { ["key"] = chord.Key };
        if (chord.Mod)
        {
            node["mod"] = true;
        }

        if (chord.Shift is { } shift)
        {
            node["shift"] = shift;
        }

        if (chord.Layer == KeyLayer.Pedal)
        {
            node["layer"] = "pedal";
        }

        return node;
    }

    /// <summary>Writes the overrides object.</summary>
    /// <param name="overrides">Command id to chords.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject OverridesNode(IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var node = new JsonObject();
        foreach (var (command, chords) in overrides)
        {
            node[command] = new JsonArray([.. chords.Select(c => (JsonNode)ChordNode(c))]);
        }

        return node;
    }

    /// <summary>Writes the overrides as an element.</summary>
    /// <param name="overrides">Command id to chords.</param>
    /// <returns>The JSON element.</returns>
    public static JsonElement OverridesElement(IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> overrides) =>
        JsonSerializer.SerializeToElement(OverridesNode(overrides));

    private static string? Key(JsonElement value) =>
        value.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
        && key.GetString() is { Length: >= 1 and <= KeyChords.MaxKeyLength } text && !text.Any(char.IsUpper)
            ? text
            : null;

    private static (bool Valid, bool? Value) OptionalBool(JsonElement value, string name) =>
        !value.TryGetProperty(name, out var field) ? (true, null) : field.ValueKind switch
        {
            JsonValueKind.Null => (true, null),
            JsonValueKind.True => (true, true),
            JsonValueKind.False => (true, false),
            _ => (false, null),
        };

    private static KeyLayer? Layer(JsonElement value) =>
        !value.TryGetProperty("layer", out var layer) ? KeyLayer.Keys : layer.ValueKind switch
        {
            JsonValueKind.Null => KeyLayer.Keys,
            JsonValueKind.String when layer.GetString() == "keys" => KeyLayer.Keys,
            JsonValueKind.String when layer.GetString() == "pedal" => KeyLayer.Pedal,
            _ => null,
        };
}
