using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist.Fakes;

/// <summary>
/// A scripted assistant output (contracts/DELTA.md section 4): <c>{"version": 1, "blocks": [&lt;lyric-assist-output&gt;, ...]}</c>, one
/// entry per block. Used by <see cref="ScriptedLyricModel"/>, the unit tests and the E2E fake stack
/// (<c>STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT</c> = a file, or a folder resolved with <see cref="Resolve"/>).
/// </summary>
public sealed class LyricScript
{
    /// <summary>The only supported script format version.</summary>
    public const int FormatVersion = 1;

    /// <summary>The output of a block the script does not cover.</summary>
    public const string EmptyOutput = "{\"items\":[]}";

    private LyricScript(IReadOnlyList<string> blocks) => Blocks = blocks;

    /// <summary>The block outputs as JSON text, in block order.</summary>
    public IReadOnlyList<string> Blocks { get; }

    /// <summary>Parses a script.</summary>
    /// <param name="json">Script JSON.</param>
    /// <returns>The script.</returns>
    /// <exception cref="InvalidDataException">Not a version-1 script with a <c>blocks</c> array of objects.</exception>
    public static LyricScript Parse(string json)
    {
        var root = ParseObject(json);
        if (root["version"] is not JsonValue version || version.GetValueKind() != JsonValueKind.Number || version.GetValue<int>() != FormatVersion)
        {
            throw new InvalidDataException("A lyric script needs \"version\": 1.");
        }

        return new LyricScript(Outputs(root["blocks"]));
    }

    /// <summary>Reads and parses a script file.</summary>
    /// <param name="path">File path.</param>
    /// <returns>The script.</returns>
    public static LyricScript Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    /// <summary>
    /// The script file for an assist: <paramref name="path"/> itself when it is a file; in a folder
    /// <c>&lt;title-slug&gt;-&lt;kind&gt;.json</c>, else <c>&lt;kind&gt;.json</c>; null when none exists (every block answers
    /// <see cref="EmptyOutput"/>).
    /// </summary>
    /// <param name="path">The configured file or folder.</param>
    /// <param name="title">Sheet title.</param>
    /// <param name="kind">Assist kind.</param>
    /// <returns>The script file, or null.</returns>
    public static string? Resolve(string path, string title, AssistKindDto kind)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var kindName = JsonSerializer.Serialize(kind).Trim('"');
        return new[] { $"{Slug(title)}-{kindName}.json", $"{kindName}.json" }.Select(name => Path.Combine(path, name)).FirstOrDefault(File.Exists);
    }

    /// <summary>The output of a block (<see cref="EmptyOutput"/> beyond the script).</summary>
    /// <param name="blockIndex">0-based block index.</param>
    /// <returns>JSON text.</returns>
    public string OutputFor(int blockIndex) => blockIndex >= 0 && blockIndex < Blocks.Count ? Blocks[blockIndex] : EmptyOutput;

    /// <summary>Lower-case ASCII letters and digits; every other run of characters becomes one <c>-</c>.</summary>
    /// <param name="title">A sheet title.</param>
    /// <returns>The slug, e.g. <c>zomeravond</c> for "Zomeravond".</returns>
    public static string Slug(string title)
    {
        var text = new string([.. title.ToLower(CultureInfo.InvariantCulture).Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);
        return string.Join('-', text.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("A lyric script is a JSON object.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("A lyric script is not valid JSON.", error);
        }
    }

    private static List<string> Outputs(JsonNode? blocks) =>
        blocks is JsonArray array && array.All(block => block is JsonObject)
            ? [.. array.Select(block => block!.ToJsonString())]
            : throw new InvalidDataException("A lyric script needs a \"blocks\" array of output objects.");
}
