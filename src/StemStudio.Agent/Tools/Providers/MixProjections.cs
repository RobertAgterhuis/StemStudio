using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Mixes;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Mix projections in dB (volumes are linear 0-1 in the API) and the open mixer's state.</summary>
internal static class MixProjections
{
    /// <summary>UI tool that reports the open mixer's state.</summary>
    public const string GetStateTool = "mixer_get_state";

    /// <summary>The settings part of a mix projection: channels, master, loop, metronome and practice tempo.</summary>
    public static JsonObject Settings(MixSettingsDto settings, JsonObject target)
    {
        var channels = settings.Channels.Select(channel => Channel(channel.Stem, channel.Volume, channel))
            .Concat(settings.DetailChannels.Select(detail => Channel($"{detail.Parent}.{detail.Stem}", detail.Volume, new ChannelSettingsDto(detail.Stem, detail.Volume, detail.Pan, detail.Mute, detail.Solo))));
        target["channels"] = new JsonArray([.. channels]);
        target["master"] = new JsonObject { ["gainDb"] = GainDb(settings.MasterVolume) };
        target["loop"] = settings.Loop is { } loop ? new JsonObject { ["startSeconds"] = loop.Start, ["endSeconds"] = loop.End, ["enabled"] = loop.Enabled } : null;
        var metronome = settings.Metronome;
        target["metronome"] = new JsonObject { ["enabled"] = metronome.Enabled, ["volume"] = metronome.Volume, ["beatsPerBar"] = metronome.BeatsPerBar, ["offsetMs"] = metronome.OffsetMs };
        target["practice"] = Practice(settings.Practice);
        return target;
    }

    /// <summary>The practice tempo (feature 012), read-only and numbers/flags only (tool catalogue 1.3.0).</summary>
    private static JsonObject Practice(PracticeSettingsDto practice)
    {
        var trainer = practice.Trainer;
        return new JsonObject
        {
            ["speedPercent"] = practice.SpeedPercent,
            ["transposeSemitones"] = practice.TransposeSemitones,
            ["trainer"] = new JsonObject { ["enabled"] = trainer.Enabled, ["startPercent"] = trainer.StartPercent, ["stepPercent"] = trainer.StepPercent, ["targetPercent"] = trainer.TargetPercent, ["passesPerStep"] = trainer.PassesPerStep },
            ["countIn"] = new JsonObject { ["bars"] = practice.CountIn.Bars, ["everyPass"] = practice.CountIn.EveryPass },
        };
    }

    /// <summary>The get_mix projection (the name is user text: untrusted).</summary>
    public static JsonObject Mix(MixDto mix) => Settings(mix.Settings, new JsonObject
    {
        ["mixId"] = mix.Id.ToString(),
        [ResultProjector.UntrustedField] = Untrusted(mix.Settings.Name),
        ["trackId"] = mix.TrackId.ToString(),
    });

    /// <summary>Maps the page's <c>mixer_get_state</c> result (settings in the MixSettingsDto shape) to the dB projection.</summary>
    public static ToolResult State(JsonElement arguments, JsonElement? result)
    {
        if (result is not { ValueKind: JsonValueKind.Object } page || !page.TryGetProperty("settings", out var settings))
        {
            return ToolResult.Of(JsonNode.Parse(result?.GetRawText() ?? "{}")!);
        }

        return ToolResult.Of(Settings(settings.Deserialize<MixSettingsDto>(Json)!, PageFields(page)));
    }

    private static JsonObject PageFields(JsonElement page)
    {
        var projection = new JsonObject();
        foreach (var name in new[] { "trackId", "mixId", "unsaved", "position", "playing", "karaoke" })
        {
            projection[name] = page.TryGetProperty(name, out var field) ? JsonNode.Parse(field.GetRawText()) : null;
        }

        return projection;
    }

    /// <summary>Reads the open mixer's settings through the UI (save_mix_as, update_mix).</summary>
    public static async Task<MixSettingsDto> ReadOpenMixerAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        var answer = invocation.Ui is { } ui ? await ui(GetStateTool, "{}", cancellationToken) : null;
        if (answer is not { Ok: true, Result: { } json })
        {
            throw new AgentActionException(ToolContext.MixerClosedReason);
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("settings", out var settings) && settings.Deserialize<MixSettingsDto>(Json) is { } dto
            ? dto
            : throw new AgentActionException("The mixer did not report its settings.");
    }

    private static JsonObject Channel(string stem, double volume, ChannelSettingsDto channel) => new()
    {
        ["stem"] = stem,
        ["gainDb"] = GainDb(volume),
        ["pan"] = channel.Pan,
        ["mute"] = channel.Mute,
        ["solo"] = channel.Solo,
    };
}
