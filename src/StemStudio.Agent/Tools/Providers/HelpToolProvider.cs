using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>The in-app help tools of feature 004: UI tools of the layout's tool host, on every page (tool catalogue 1.0.0).</summary>
public sealed class HelpToolProvider : IAgentToolProvider
{
    private const string F = "004";
    private const int MaxTopics = 5;
    private const int MaxSummary = 300;

    /// <summary>The pages <c>navigate</c> can open.</summary>
    public static IReadOnlyList<string> Pages { get; } = ["import", "tracks", "mixes", "library", "styles", "generations", "lyrics", "songs", "voices", "settings"];

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Tool(F, "search_help", ToolKindDto.Ui, ToolRiskDto.Read) with
        {
            Title = "Search the help",
            Description = "Search StemStudio's own help topics (how the mixer, loops, exports and other features work). Returns up to 5 topics.",
            Schema = ToolSchema.Of(Param.Text("query", 1, 200).Describe("Search words, e.g. 'loop' or 'export mp3'.")),
            MapUiResult = Topics,
        },
        Tool(F, "open_help", ToolKindDto.Ui, ToolRiskDto.Read) with
        {
            Title = "Open a help topic",
            Description = "Open the help panel on a topic found with search_help.",
            Schema = ToolSchema.Of(Param.Text("topicId", 1, 80) with { Pattern = "^[a-z0-9-]+$" }),
        },
        Tool(F, "navigate", ToolKindDto.Ui, ToolRiskDto.Read) with
        {
            Title = "Open a page",
            Description = "Open a page of StemStudio by name (import, tracks, mixes, library, styles, generations, lyrics, songs, voices, settings). Unsaved mixer changes are asked about first.",
            Schema = ToolSchema.Of(Param.Choice("page", Pages)),
        },
    ];

    /// <summary>Keeps topic id, title and a short summary of at most five topics (own content, trusted).</summary>
    private static ToolResult Topics(JsonElement arguments, JsonElement? result) => ToolResult.Of(new JsonArray([..
        (result is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : Enumerable.Empty<JsonElement>())
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Take(MaxTopics)
            .Select(item => (JsonNode)new JsonObject
            {
                ["topicId"] = Field(item, "topicId"),
                ["title"] = Field(item, "title"),
                ["summary"] = Field(item, "summary") is { } summary && summary.Length > MaxSummary ? summary[..MaxSummary] : Field(item, "summary"),
            })]));

    private static string? Field(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
