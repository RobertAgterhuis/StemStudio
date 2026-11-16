using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StemStudio.Agent.Tools;

/// <summary>
/// Canonical JSON (keys sorted ordinally at every level, no whitespace, minimal escaping) and its SHA-256 hash: the
/// binding between an approval card and the call that is executed (FR-015, data-model 4.2).
/// </summary>
public static class CanonicalJson
{
    /// <summary>Encoder shared by the agent's JSON output (readable non-ASCII text, still valid JSON).</summary>
    public static JavaScriptEncoder Encoder { get; } = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = Encoder, Indented = false };

    /// <summary>Canonical form of a JSON text.</summary>
    /// <param name="json">Valid JSON.</param>
    /// <returns>The canonical text.</returns>
    public static string Canonicalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Canonicalize(document.RootElement);
    }

    /// <summary>Canonical form of a JSON element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The canonical text.</returns>
    public static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            Write(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>SHA-256 of the UTF-8 bytes, lower-case hex (64 characters).</summary>
    /// <param name="canonical">Canonical JSON.</param>
    /// <returns>The hash.</returns>
    public static string Hash(string canonical) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(writer, element);
                break;
            case JsonValueKind.Array:
                WriteArray(writer, element);
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            default:
                writer.WriteRawValue(element.GetRawText());
                break;
        }
    }

    private static void WriteObject(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(property.Name);
            Write(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartArray();
        foreach (var item in element.EnumerateArray())
        {
            Write(writer, item);
        }

        writer.WriteEndArray();
    }
}
