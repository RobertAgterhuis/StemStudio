using StemStudio.Web.Mixer;

namespace StemStudio.Web.Agent;

/// <summary>
/// The mixer UI tools of the open mixer page (tool catalogue 1.0.0, feature 001, availability <c>+mixer</c>):
/// state, channel, master, reset, transport, loop and metronome. Changes go through <see cref="IMixerControls"/>
/// and every change tool returns the previous and the current values (the undo entry of FR-017).
/// </summary>
public sealed partial class MixerUiTools : IAgentUiToolHandler
{
    private readonly IMixerControls _controls;
    private readonly Dictionary<string, Func<UiToolArguments, Task<object>>> _tools;

    /// <summary>Creates the tools for one open mixer.</summary>
    /// <param name="controls">The page's control handlers.</param>
    public MixerUiTools(IMixerControls controls)
    {
        _controls = controls;
        _tools = new(StringComparer.Ordinal)
        {
            ["mixer_get_state"] = _ => Task.FromResult(GetState()),
            ["mixer_set_channel"] = SetChannelAsync,
            ["mixer_set_master"] = SetMasterAsync,
            ["mixer_reset"] = _ => ResetAsync(),
            ["transport"] = TransportAsync,
            ["set_loop"] = SetLoopAsync,
            ["set_metronome"] = SetMetronomeAsync,
        };
    }

    /// <summary>The tool names this handler owns.</summary>
    public static IReadOnlySet<string> Names { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "mixer_get_state", "mixer_set_channel", "mixer_set_master", "mixer_reset", "transport", "set_loop", "set_metronome",
    };

    /// <summary>The open track.</summary>
    public Guid TrackId => State.TrackId;

    /// <summary>The open track's title.</summary>
    public string Title => _controls.Title;

    /// <summary>The mix loaded in the mixer, or null.</summary>
    public Guid? MixId => State.MixId;

    private MixerState State => _controls.State;

    /// <inheritdoc />
    public bool CanRun(string tool) => Names.Contains(tool);

    /// <inheritdoc />
    public async Task<UiToolResult> ExecuteAsync(string tool, UiToolArguments arguments)
    {
        if (!_tools.TryGetValue(tool, out var run))
        {
            return UiToolResult.Fail($"The mixer cannot run {tool}.");
        }

        try
        {
            return UiToolResult.Done(await run(arguments));
        }
        catch (UiToolException ex)
        {
            return UiToolResult.Fail(ex.Message);
        }
    }

    private object GetState() => new
    {
        trackId = State.TrackId,
        mixId = State.MixId,
        unsaved = State.IsDirty,
        channels = Visible().Select(View).ToList(),
        master = Gain.ToDb(State.MasterVolume),
        position = _controls.Position,
        playing = _controls.Playing,
        loop = State.Loop,
        metronome = State.Metronome,
    };

    private async Task<object> SetChannelAsync(UiToolArguments args)
    {
        var stem = args.RequiredText("stem");
        var channel = Visible().FirstOrDefault(c => c.Stem == stem) ?? throw new UiToolException($"This track has no channel '{stem}'.");
        var gain = TargetDb(args, channel.Volume);
        var previous = View(channel);
        await (gain is { } db ? _controls.SetVolumeAsync(stem, Gain.ToVolume(db)) : Task.CompletedTask);
        await (args.Number("pan") is { } pan ? _controls.SetPanAsync(stem, pan) : Task.CompletedTask);
        await ToggleWhenAsync(args.Bool("mute"), channel.Mute, () => _controls.ToggleMuteAsync(stem));
        await ToggleWhenAsync(args.Bool("solo"), channel.Solo, () => _controls.ToggleSoloAsync(stem));
        await _controls.RefreshAsync();
        return new { stem, previous, current = View(channel) };
    }

    private async Task<object> SetMasterAsync(UiToolArguments args)
    {
        var previous = Gain.ToDb(State.MasterVolume);
        if (TargetDb(args, State.MasterVolume) is { } db)
        {
            await _controls.SetMasterAsync(Gain.ToVolume(db));
        }

        await _controls.RefreshAsync();
        return new { previous, current = Gain.ToDb(State.MasterVolume) };
    }

    private async Task<object> ResetAsync()
    {
        var previous = Visible().Select(View).ToList();
        foreach (var channel in Visible())
        {
            await _controls.SetVolumeAsync(channel.Stem, 1.0);
            await _controls.SetPanAsync(channel.Stem, 0);
            await ToggleWhenAsync(false, channel.Mute, () => _controls.ToggleMuteAsync(channel.Stem));
            await ToggleWhenAsync(false, channel.Solo, () => _controls.ToggleSoloAsync(channel.Stem));
        }

        await _controls.RefreshAsync();
        return new { previous };
    }

    private static double? TargetDb(UiToolArguments args, double volume)
    {
        var absolute = args.Number("gainDb");
        var change = args.Number("gainChangeDb");
        if (absolute is not null && change is not null)
        {
            throw new UiToolException("Use gainDb or gainChangeDb, not both.");
        }

        return absolute ?? (change is { } delta ? Gain.RawDb(volume) + delta : null);
    }

    private static Task ToggleWhenAsync(bool? wanted, bool current, Func<Task> toggle) =>
        wanted is { } value && value != current ? toggle() : Task.CompletedTask;

    private static ChannelView View(ChannelState channel) => new(channel.Stem, Gain.ToDb(channel.Volume), channel.Pan, channel.Mute, channel.Solo);

    private IEnumerable<ChannelState> Visible() => State.AllChannels.Where(c => !State.IsHidden(c));

    private sealed record ChannelView(string Stem, double GainDb, double Pan, bool Mute, bool Solo);
}
