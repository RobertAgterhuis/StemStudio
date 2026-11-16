using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools;

/// <summary>The JSON the model sees for one tool result.</summary>
/// <param name="Json">Projection, at most 4 KB.</param>
/// <param name="Tainted">True when it carries external text (<c>untrusted_text</c>, FR-013).</param>
/// <param name="Truncated">True when it was cut to the limit.</param>
public sealed record ProjectedResult(string Json, bool Tainted, bool Truncated);

/// <summary>
/// Final safety net for tool results: removes fields that must never reach the model (lyrics, biographies, paths, URLs,
/// personal names; FR-026), caps the result at 4 KB with a truncation marker, and reports taint when external text is
/// present in a field named <c>untrusted_text</c>. Providers build the projections from whitelisted fields.
/// </summary>
public static class ResultProjector
{
    /// <summary>The only field name that may carry external text.</summary>
    public const string UntrustedField = "untrusted_text";

    /// <summary>Result size limit in UTF-8 bytes.</summary>
    public const int MaxBytes = AgentContractLimits.ResultMaxBytes;

    private const int MarkerReserve = 32;
    private const int MinStringLength = 16;

    private static readonly JsonSerializerOptions Options = new() { Encoder = CanonicalJson.Encoder };

    private static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "lyrics", "lyricsText", "text", "lines", "plainText", "biography", "extract", "path", "filePath", "localPath", "url",
        "audioUrl", "vocalAudioUrl", "sampleUrl", "pageUrl", "historyUrl", "sourceUrl", "personName", "apiKey",
    };

    /// <summary>Projects a provider result.</summary>
    /// <param name="projection">The provider's projection; null means an empty object.</param>
    /// <returns>The projected result.</returns>
    public static ProjectedResult Project(JsonNode? projection)
    {
        var node = projection?.DeepClone() ?? new JsonObject();
        Strip(node);
        var tainted = Nodes(node).OfType<JsonObject>().Any(HasUntrustedText);
        return Size(node) <= MaxBytes ? new ProjectedResult(Serialize(node), tainted, false) : new ProjectedResult(Serialize(Shrink(node)), tainted, true);
    }

    /// <summary>A readable error result (what the model sees for a failed call).</summary>
    /// <param name="message">Readable message (capped at 500 characters).</param>
    /// <returns>The projected result.</returns>
    public static ProjectedResult Error(string message)
    {
        var text = message.Length > AgentContractLimits.ErrorMaxLength ? message[..AgentContractLimits.ErrorMaxLength] : message;
        return new ProjectedResult(Serialize(new JsonObject { ["ok"] = false, ["error"] = text }), false, false);
    }

    /// <summary>Cleans external text for an <c>untrusted_text</c> field: control characters removed, line breaks as spaces, capped.</summary>
    /// <param name="text">External text.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>The cleaned text, or null when empty.</returns>
    public static string? Untrusted(string? text, int maxLength)
    {
        var builder = new StringBuilder();
        foreach (var c in text ?? string.Empty)
        {
            AppendClean(builder, c);
        }

        var clean = builder.ToString().Trim();
        return clean.Length == 0 ? null : clean[..Math.Min(clean.Length, maxLength)];
    }

    /// <summary>"title - artist", or the title alone.</summary>
    /// <param name="title">Title.</param>
    /// <param name="artist">Artist, when known.</param>
    /// <returns>The joined text.</returns>
    public static string Join(string title, string? artist) => string.IsNullOrWhiteSpace(artist) ? title : $"{title} - {artist}";

    private static void AppendClean(StringBuilder builder, char c)
    {
        if (c is '\n' or '\r' or '\t')
        {
            builder.Append(' ');
        }
        else if (!char.IsControl(c))
        {
            builder.Append(c);
        }
    }

    private static bool HasUntrustedText(JsonObject node) =>
        node[UntrustedField] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0;

    private static void Strip(JsonNode node)
    {
        foreach (var item in Nodes(node).OfType<JsonObject>().ToList())
        {
            foreach (var key in item.Select(property => property.Key).Where(Forbidden.Contains).ToList())
            {
                item.Remove(key);
            }
        }
    }

    private static JsonNode Shrink(JsonNode node)
    {
        var root = node is JsonValue ? new JsonObject { ["value"] = node } : node;
        while (Size(root) > MaxBytes - MarkerReserve && (RemoveLastItem(root) || HalveLongestString(root)))
        {
            // Each pass removes one array item or halves one string until the result fits.
        }

        return Mark(root);
    }

    private static JsonNode Mark(JsonNode root)
    {
        if (root is JsonArray array)
        {
            array.Add(new JsonObject { ["truncated"] = true });
        }
        else
        {
            root.AsObject()["truncated"] = true;
        }

        return root;
    }

    private static bool RemoveLastItem(JsonNode root)
    {
        var largest = Nodes(root).OfType<JsonArray>().Where(array => array.Count > 0).MaxBy(Size);
        largest?.RemoveAt(largest.Count - 1);
        return largest is not null;
    }

    private static bool HalveLongestString(JsonNode root)
    {
        var longest = Nodes(root).OfType<JsonValue>()
            .Select(value => (Value: value, Text: value.TryGetValue<string>(out var text) ? text : string.Empty))
            .Where(item => item.Text.Length > MinStringLength)
            .MaxBy(item => item.Text.Length);
        longest.Value?.ReplaceWith(longest.Text[..(longest.Text.Length / 2)] + "...");
        return longest.Value is not null;
    }

    private static IEnumerable<JsonNode> Nodes(JsonNode? node)
    {
        if (node is null)
        {
            yield break;
        }

        yield return node;
        foreach (var child in Children(node).SelectMany(Nodes))
        {
            yield return child;
        }
    }

    private static IEnumerable<JsonNode?> Children(JsonNode node) => node switch
    {
        JsonObject item => [.. item.Select(property => property.Value)],
        JsonArray array => [.. array],
        _ => [],
    };

    private static int Size(JsonNode node) => Encoding.UTF8.GetByteCount(Serialize(node));

    private static string Serialize(JsonNode node) => node.ToJsonString(Options);
}
