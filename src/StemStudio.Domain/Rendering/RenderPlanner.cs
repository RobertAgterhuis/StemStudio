using System.Globalization;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Rendering;

/// <summary>One input of a render: a channel file with its gain and pan.</summary>
/// <param name="ChannelId">Channel id.</param>
/// <param name="Path">File relative to the API data directory.</param>
/// <param name="Gain">Gain 0..1 (never 0 in a plan).</param>
/// <param name="Pan">Pan -1..1.</param>
public sealed record PlannedInput(string ChannelId, string Path, double Gain, double Pan);

/// <summary>What the worker renders (data-model 4.3).</summary>
/// <param name="Inputs">Inputs in mixer order.</param>
/// <param name="MasterGain">Master gain 0..1.</param>
/// <param name="ChannelText">The channels for the contents text, e.g. "vocals + bass" or "6 channels".</param>
/// <param name="Mode">Raw or mixed.</param>
public sealed record RenderPlan(IReadOnlyList<PlannedInput> Inputs, double MasterGain, string ChannelText, ExportMode Mode)
{
    /// <summary>The contents text without a range, e.g. "drums, raw" or "6 channels, mixed".</summary>
    public string Contents => $"{ChannelText}, {(Mode == ExportMode.Raw ? "raw" : "mixed")}";
}

/// <summary>Thrown when no channel of an export would be audible (422 <c>nothing-to-export</c>).</summary>
public sealed class NothingToExportException : Exception
{
    /// <summary>The fixed message.</summary>
    public const string Text = "Nothing to export: no channel is audible";

    /// <summary>Creates the exception.</summary>
    public NothingToExportException()
        : base(Text)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public NothingToExportException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public NothingToExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Turns an export request into a render plan (data-model 4.3, binding; shared vectors <c>render-vectors.json</c>): the
/// audible level of the engine's gate rule v2 for <c>channels = null</c>, explicit channels with their fader and pan, or
/// raw channels at unity gain.
/// </summary>
public static class RenderPlanner
{
    /// <summary>Most channels of one export.</summary>
    public const int MaxChannels = 56;

    /// <summary>Above this many channels the contents text counts them.</summary>
    public const int MaxListedChannels = 4;

    /// <summary>Plans an export.</summary>
    /// <param name="source">The track's channels.</param>
    /// <param name="mode">Raw or mixed.</param>
    /// <param name="channels">Explicit channel ids, or null for every audible channel (mix only).</param>
    /// <param name="mix">The mix (required for mix mode).</param>
    /// <returns>The plan.</returns>
    /// <exception cref="DomainValidationException">A channel is unknown, repeated or missing on the track, or a required value is missing.</exception>
    /// <exception cref="NothingToExportException">No channel would be audible.</exception>
    public static RenderPlan Plan(RenderSource source, ExportMode mode, IReadOnlyList<string>? channels, ExportMix? mix)
    {
        ArgumentNullException.ThrowIfNull(source);
        var selected = Selection(source, mode, channels, mix);
        var inputs = (mode == ExportMode.Raw ? Raw(selected!) : Mixed(source, selected, mix!))
            .Where(input => input.Gain > 0).OrderBy(input => input.Channel.SortKey).ToList();
        if (inputs.Count == 0)
        {
            throw new NothingToExportException();
        }

        var planned = inputs.Select(input => new PlannedInput(input.Channel.Id, input.Channel.PathFor(source.TrackId), input.Gain, input.Pan)).ToList();
        return new RenderPlan(planned, mode == ExportMode.Raw ? 1 : mix!.MasterVolume, ChannelText(planned), mode);
    }

    /// <summary>The mix rules of feature 002 for an export mix (name, metronome and loop do not apply).</summary>
    /// <param name="mix">The export mix.</param>
    /// <param name="track">The completed track.</param>
    /// <returns>Violations with paths relative to the mix; empty when valid.</returns>
    public static IReadOnlyList<DomainError> ValidateMix(ExportMix mix, Track track)
    {
        ArgumentNullException.ThrowIfNull(mix);
        var settings = new MixSettings
        {
            Name = "export",
            MasterVolume = mix.MasterVolume,
            Channels = mix.Channels,
            Metronome = new MetronomeSettings { Enabled = false },
            Expanded = mix.Expanded,
            DetailChannels = mix.DetailChannels,
        };
        return settings.Validate(track).Where(error => error.Field != "trackId").ToList();
    }

    private static List<RenderChannel>? Selection(RenderSource source, ExportMode mode, IReadOnlyList<string>? channels, ExportMix? mix)
    {
        var errors = new ValidationErrors();
        errors.Check(mode == ExportMode.Raw || mix is not null, "mix", "Send the mixer state or a saved mix.");
        var countValid = channels is null ? mode == ExportMode.Mix : channels.Count is > 0 and <= MaxChannels;
        errors.Check(countValid, "channels", $"Select 1 to {MaxChannels} channels.");
        var selected = channels is null || errors.Errors.Count > 0 ? null : Parse(errors, source, channels);
        errors.ThrowIfAny();
        return selected;
    }

    private static List<RenderChannel> Parse(ValidationErrors errors, RenderSource source, IReadOnlyList<string> channels)
    {
        var (selected, seen) = (new List<RenderChannel>(), new HashSet<string>(StringComparer.Ordinal));
        for (var i = 0; i < channels.Count; i++)
        {
            var field = string.Create(CultureInfo.InvariantCulture, $"channels[{i}]");
            if (Check(errors, field, source, channels[i]) is { } channel && Unique(errors, field, seen, channel))
            {
                selected.Add(channel);
            }
        }

        return selected;
    }

    private static RenderChannel? Check(ValidationErrors errors, string field, RenderSource source, string id)
    {
        var channel = RenderChannel.Parse(id);
        errors.Check(channel is not null, field, $"'{id}' is not a channel id.");
        var missing = channel is not null && !source.Has(channel);
        errors.Check(!missing, field, channel?.Detail is null ? $"The track has no stem {id}." : $"The track has no detail stem {id}.");
        return missing ? null : channel;
    }

    private static bool Unique(ValidationErrors errors, string field, HashSet<string> seen, RenderChannel channel)
    {
        var added = seen.Add(channel.Id);
        errors.Check(added, field, "Each channel can be selected only once.");
        return added;
    }

    private static IEnumerable<Weighted> Raw(List<RenderChannel> selected) => selected.Select(channel => new Weighted(channel, 1, 0));

    private static IEnumerable<Weighted> Mixed(RenderSource source, List<RenderChannel>? selected, ExportMix mix) => selected is null
        ? Audible(source, mix)
        : selected.Select(channel => Weigh(channel, Fader(mix, channel)));

    private static IEnumerable<Weighted> Audible(RenderSource source, ExportMix mix)
    {
        var channels = source.AllChannels().Select(channel => (Channel: channel, Fader: Fader(mix, channel), Hidden: IsHidden(mix, channel))).ToList();
        var anySolo = channels.Any(channel => !channel.Hidden && channel.Fader.Solo);
        return channels.Where(channel => !channel.Hidden && !channel.Fader.Mute && (!anySolo || channel.Fader.Solo))
            .Select(channel => Weigh(channel.Channel, channel.Fader));
    }

    private static bool IsHidden(ExportMix mix, RenderChannel channel) =>
        mix.Expanded.Contains(channel.Parent, StringComparer.Ordinal) == (channel.Detail is null);

    private static FaderSettings Fader(ExportMix mix, RenderChannel channel) => channel.Detail is null
        ? mix.Channels.FirstOrDefault(entry => entry.Stem == channel.Parent) ?? ChannelSettings.Default(channel.Parent)
        : mix.DetailChannels.FirstOrDefault(entry => entry.Parent == channel.Parent && entry.Stem == channel.Detail)
            ?? DetailChannelSettings.Default(channel.Parent, channel.Detail);

    private static Weighted Weigh(RenderChannel channel, FaderSettings fader) => new(channel, fader.Volume, fader.Pan);

    private static string ChannelText(List<PlannedInput> inputs) => inputs.Count > MaxListedChannels
        ? string.Create(CultureInfo.InvariantCulture, $"{inputs.Count} channels")
        : string.Join(" + ", inputs.Select(input => input.ChannelId));

    private sealed record Weighted(RenderChannel Channel, double Gain, double Pan);
}
