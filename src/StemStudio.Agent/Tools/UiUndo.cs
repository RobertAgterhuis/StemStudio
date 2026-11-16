using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Model;

namespace StemStudio.Agent.Tools;

/// <summary>
/// The inverse calls of the mixer UI tools, built from the page's answer (<c>{previous, current}</c>, tool catalogue 001):
/// the previous values set back with the same tool. Values are clamped to the tools' argument ranges.
/// </summary>
public static class UiUndo
{
    private const double MinGainDb = -60;
    private const double MaxGainDb = 12;

    /// <summary>The inverse calls of one executed UI tool (empty when the answer carries no previous values).</summary>
    /// <param name="tool">UI tool name.</param>
    /// <param name="answer">The page's answer.</param>
    /// <returns>The inverse calls in the order to record them.</returns>
    public static IReadOnlyList<UndoAction> From(string tool, JsonElement answer) =>
        answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("previous", out var previous) ? Inverse(tool, previous) : [];

    private static IReadOnlyList<UndoAction> Inverse(string tool, JsonElement previous) => tool switch
    {
        "mixer_set_channel" => [Channel(previous)],
        "mixer_reset" when previous.ValueKind == JsonValueKind.Array => [.. previous.EnumerateArray().Select(Channel)],
        "mixer_set_master" when previous.ValueKind == JsonValueKind.Number => [Action(tool, new JsonObject { ["gainDb"] = Gain(previous) })],
        "set_loop" => [Loop(previous)],
        "show_karaoke" when previous.ValueKind is JsonValueKind.True or JsonValueKind.False => [Action(tool, new JsonObject { ["enabled"] = previous.GetBoolean() })],
        "set_metronome" when previous.ValueKind == JsonValueKind.Object => [Metronome(previous)],
        _ => [],
    };

    private static UndoAction Channel(JsonElement channel) => Action("mixer_set_channel", new JsonObject
    {
        ["stem"] = channel.GetProperty("stem").GetString(),
        ["gainDb"] = Gain(channel.GetProperty("gainDb")),
        ["pan"] = Math.Clamp(channel.GetProperty("pan").GetDouble(), -1, 1),
        ["mute"] = channel.GetProperty("mute").GetBoolean(),
        ["solo"] = channel.GetProperty("solo").GetBoolean(),
    });

    private static UndoAction Loop(JsonElement loop) => loop.ValueKind == JsonValueKind.Object
        ? Action("set_loop", new JsonObject
        {
            ["startSeconds"] = loop.GetProperty("start").GetDouble(),
            ["endSeconds"] = loop.GetProperty("end").GetDouble(),
            ["enabled"] = loop.GetProperty("enabled").GetBoolean(),
        })
        : Action("set_loop", new JsonObject { ["enabled"] = false });

    private static UndoAction Metronome(JsonElement metronome) => Action("set_metronome", new JsonObject
    {
        ["enabled"] = metronome.GetProperty("enabled").GetBoolean(),
        ["volume"] = Math.Clamp(metronome.GetProperty("volume").GetDouble(), 0, 1),
        ["beatsPerBar"] = Math.Clamp(metronome.GetProperty("beatsPerBar").GetInt32(), 2, 12),
        ["offsetMs"] = Math.Clamp(metronome.GetProperty("offsetMs").GetInt32(), -2000, 2000),
    });

    private static double Gain(JsonElement value) => Math.Clamp(value.GetDouble(), MinGainDb, MaxGainDb);

    private static UndoAction Action(string tool, JsonObject arguments) =>
        new() { Tool = tool, Arguments = CanonicalJson.Canonicalize(arguments.ToJsonString()) };
}
