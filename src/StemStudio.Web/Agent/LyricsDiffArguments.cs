using System.Text.Json;

namespace StemStudio.Web.Agent;

/// <summary>Reads the id-only arguments of <c>apply_lyric_suggestions</c> (tool catalogue 1.2.0: <c>lyricsId</c>, <c>suggestionIds</c>).</summary>
public static class LyricsDiffArguments
{
    /// <summary>The sheet and the distinct, valid suggestion ids; anything else in the arguments is ignored.</summary>
    /// <param name="arguments">The approval's arguments.</param>
    /// <returns>The sheet (or null) and the ids.</returns>
    public static (Guid? LyricsId, HashSet<Guid> Ids) Read(JsonElement arguments) =>
        (new UiToolArguments(arguments).Id("lyricsId"), [.. Items(arguments).Select(Parse).OfType<Guid>()]);

    private static JsonElement.ArrayEnumerator Items(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("suggestionIds", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
            : default;

    private static Guid? Parse(JsonElement item) =>
        item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var id) ? id : null;
}
