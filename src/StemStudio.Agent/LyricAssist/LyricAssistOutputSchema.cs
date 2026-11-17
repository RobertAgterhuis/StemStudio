using System.Text.Json.Nodes;

namespace StemStudio.Agent.LyricAssist;

/// <summary>
/// The model output schema lyric-assist-output 1.0.0 (JSON Schema 2020-12), embedded from
/// <c>specs/023-lyric-writing-assistant/contracts/lyric-assist-output.schema.json</c>: Ollama <c>format</c> and the input schema of the
/// Claude tool <c>emit_suggestions</c>.
/// </summary>
public static class LyricAssistOutputSchema
{
    /// <summary>Schema version.</summary>
    public const string Version = "1.0.0";

    /// <summary>Logical name of the embedded resource.</summary>
    public const string ResourceName = "StemStudio.Agent.LyricAssist.lyric-assist-output.schema.json";

    /// <summary>Name of the single forced Claude tool whose input schema is this schema.</summary>
    public const string ClaudeToolName = "emit_suggestions";

    private static readonly Lazy<string> LazyText = new(Read);

    /// <summary>The schema text as embedded.</summary>
    public static string Text => LazyText.Value;

    /// <summary>A fresh parsed copy (callers may change it).</summary>
    /// <returns>The schema object.</returns>
    public static JsonObject Parse() => JsonNode.Parse(Text)!.AsObject();

    private static string Read()
    {
        using var stream = typeof(LyricAssistOutputSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The lyric-assist-output schema resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
