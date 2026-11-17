using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Mixes;

namespace StemStudio.AgentEval.Fixture;

/// <summary>
/// The browser page of a case (mixer state and navigation), answering UI tools like the frontend: <c>mixer_get_state</c>
/// with <c>settings</c> (MixSettingsDto, linear volumes, empty name) and the change tools with <c>{previous, current}</c>.
/// </summary>
internal sealed class FixturePage : IUiToolGateway
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Channel> _initial;
    private readonly Dictionary<string, Channel> _channels;
    private readonly Guid? _mixId;
    private Guid? _openTrack;
    private JsonObject? _loop;
    private JsonObject _metronome = new() { ["enabled"] = false, ["volume"] = 0.8, ["beatsPerBar"] = 4, ["offsetMs"] = 0 };
    private bool _karaoke;

    public FixturePage(Guid? openTrack, Guid? mixId)
    {
        _openTrack = openTrack;
        _mixId = mixId;
        _initial = FixtureLibrary.Settings(string.Empty).Channels.ToDictionary(channel => channel.Stem, _ => new Channel(0, 0, false, false), StringComparer.Ordinal);
        _channels = new Dictionary<string, Channel>(_initial, StringComparer.Ordinal);
    }

    /// <summary>True when every channel is back at its initial value (the undo case).</summary>
    public bool IsRestored => _channels.All(pair => _initial[pair.Key] == pair.Value);

    public Task<UiToolResult> RequestAsync(UiToolRequest request, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(request.Arguments);
        var answer = Answer(request.Tool, document.RootElement);
        return Task.FromResult(answer is null
            ? new UiToolResult { Ok = false, Error = "The mixer of the track is not open" }
            : new UiToolResult { Ok = true, Result = answer.ToJsonString() });
    }

    public bool TryComplete(Guid runId, Guid stepId, UiToolResult result) => false;

    private JsonObject? Answer(string tool, JsonElement arguments) => tool switch
    {
        "open_mixer" => Open(arguments),
        "navigate" or "open_track" or "open_help" or "search_help" or "open_library_folder" or "open_lyrics_editor" => new JsonObject { ["ok"] = true, ["opened"] = true },
        _ when _openTrack is null => null,
        "mixer_get_state" => State(),
        "mixer_set_channel" => SetChannel(arguments),
        "set_loop" => SetLoop(arguments),
        "set_metronome" => SetMetronome(arguments),
        "show_karaoke" => Karaoke(arguments),
        _ => new JsonObject { ["ok"] = true },
    };

    private JsonObject Open(JsonElement arguments)
    {
        _openTrack = Guid.Parse(arguments.GetProperty("trackId").GetString()!);
        return new JsonObject { ["trackId"] = _openTrack.ToString() };
    }

    private JsonObject State()
    {
        var settings = FixtureLibrary.Settings(string.Empty) with { Channels = [.. _channels.Select(pair => pair.Value.ToSettings(pair.Key))] };
        return new JsonObject
        {
            ["trackId"] = _openTrack.ToString(),
            ["mixId"] = _mixId?.ToString(),
            ["unsaved"] = true,
            ["position"] = 0,
            ["playing"] = false,
            ["karaoke"] = _karaoke,
            ["settings"] = JsonSerializer.SerializeToNode(settings, Web),
        };
    }

    private JsonObject SetChannel(JsonElement arguments)
    {
        var stem = arguments.GetProperty("stem").GetString()!;
        var previous = _channels.GetValueOrDefault(stem, new Channel(0, 0, false, false));
        var current = previous.Apply(arguments);
        _channels[stem] = current;
        return new JsonObject { ["stem"] = stem, ["previous"] = previous.ToJson(stem), ["current"] = current.ToJson(stem) };
    }

    private JsonObject SetLoop(JsonElement arguments)
    {
        var previous = _loop?.DeepClone();
        var start = LoopStart(arguments);
        _loop = new JsonObject { ["start"] = start, ["end"] = LoopEnd(arguments, start), ["enabled"] = Flag(arguments, "enabled") ?? true };
        return new JsonObject { ["previous"] = previous, ["current"] = _loop.DeepClone() };
    }

    private JsonObject SetMetronome(JsonElement arguments)
    {
        var previous = _metronome.DeepClone();
        _metronome = new JsonObject
        {
            ["enabled"] = Flag(arguments, "enabled") ?? _metronome["enabled"]!.GetValue<bool>(),
            ["volume"] = Number(arguments, "volume") ?? _metronome["volume"]!.GetValue<double>(),
            ["beatsPerBar"] = (int)(Number(arguments, "beatsPerBar") ?? _metronome["beatsPerBar"]!.GetValue<int>()),
            ["offsetMs"] = (int)(Number(arguments, "offsetMs") ?? _metronome["offsetMs"]!.GetValue<int>()),
        };
        return new JsonObject { ["previous"] = previous, ["current"] = _metronome.DeepClone() };
    }

    private JsonObject Karaoke(JsonElement arguments)
    {
        var previous = _karaoke;
        _karaoke = Flag(arguments, "enabled") ?? !_karaoke;
        return new JsonObject { ["previous"] = previous, ["current"] = _karaoke };
    }

    /// <summary>Loop bounds in seconds; a bar lasts 2 s at the fixture's 120 BPM in 4/4.</summary>
    private static double LoopStart(JsonElement arguments) => Number(arguments, "startSeconds") ?? ((Number(arguments, "startBar") - 1) * 2) ?? 0;

    private static double LoopEnd(JsonElement arguments, double start) => Number(arguments, "endSeconds") ?? (Number(arguments, "endBar") * 2) ?? start + 8;

    internal static double? Number(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    internal static bool? Flag(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    /// <summary>One mixer channel in the agent's view (gain in dB).</summary>
    private sealed record Channel(double GainDb, double Pan, bool Mute, bool Solo)
    {
        public Channel Apply(JsonElement arguments) => this with
        {
            GainDb = Gain(arguments),
            Pan = Number(arguments, "pan") ?? Pan,
            Mute = Flag(arguments, "mute") ?? Mute,
            Solo = Flag(arguments, "solo") ?? Solo,
        };

        public JsonObject ToJson(string stem) => new() { ["stem"] = stem, ["gainDb"] = GainDb, ["pan"] = Pan, ["mute"] = Mute, ["solo"] = Solo };

        public ChannelSettingsDto ToSettings(string stem) => new(stem, Math.Pow(10, GainDb / 20), Pan, Mute, Solo);

        private double Gain(JsonElement arguments) => Number(arguments, "gainDb") ?? GainDb + (Number(arguments, "gainChangeDb") ?? 0);
    }
}
