using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Mixer;

/// <summary>
/// UI-side mixer model of one track: channels, master, metronome, loop markers and the dirty flag
/// (US2, FR-011a). Every mutator marks the mixer dirty and returns what must be sent to the engine.
/// </summary>
public sealed partial class MixerState
{
    private readonly Dictionary<string, ChannelState> _byStem;
    private double? _loopStart;
    private double? _loopEnd;

    /// <summary>Creates the default mixer for a completed track.</summary>
    /// <param name="track">The track; its stems define the channels (in <see cref="StemNames.SixStem"/> order).</param>
    public MixerState(TrackDto track)
    {
        TrackId = track.Id;
        Duration = track.DurationSeconds ?? 0;
        DetectedBeatsPerBar = track.BeatGrid?.BeatsPerBar ?? 4;
        var present = track.Stems.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
        Channels = StemNames.SixStem.Where(present.Contains).Select(name => new ChannelState(name)).ToList();
        Groups = BuildGroups(track, present);
        _byStem = Channels.Concat(Groups.SelectMany(g => g.Stems.Select(s => s.Channel))).ToDictionary(c => c.Stem, StringComparer.Ordinal);
        Metronome = new MetronomeSettingsDto(track.BeatGrid?.Reliable ?? false, ContractLimits.DefaultMetronomeVolume, null, 0, DetectedBeatsPerBar);
    }

    /// <summary>The track id.</summary>
    public Guid TrackId { get; }

    /// <summary>Track duration in seconds (the engine's value once loaded; 0 when unknown).</summary>
    public double Duration { get; set; }

    /// <summary>Beats per bar of the detected grid (4 when unknown); restored by the metronome reset.</summary>
    public int DetectedBeatsPerBar { get; }

    /// <summary>One channel per available standard stem.</summary>
    public IReadOnlyList<ChannelState> Channels { get; }

    /// <summary>Master gain 0..1 (default 1).</summary>
    public double MasterVolume { get; private set; } = ContractLimits.DefaultMasterVolume;

    /// <summary>Current metronome settings.</summary>
    public MetronomeSettingsDto Metronome { get; private set; }

    /// <summary>The last valid loop region (what is saved and what the engine has), or null.</summary>
    public LoopRegionDto? Loop { get; private set; }

    /// <summary>Marker A as entered.</summary>
    public double? LoopStart => _loopStart;

    /// <summary>Marker B as entered.</summary>
    public double? LoopEnd => _loopEnd;

    /// <summary>True while looping is on.</summary>
    public bool LoopEnabled => Loop?.Enabled == true;

    /// <summary>Why the last loop change was refused, or null.</summary>
    public string? LoopError { get; private set; }

    /// <summary>Id of the loaded or last saved mix; null for an unsaved mixer.</summary>
    public Guid? MixId { get; private set; }

    /// <summary>Name of the loaded or last saved mix; null for an unsaved mixer.</summary>
    public string? MixName { get; private set; }

    /// <summary>True after any change since the last load or save (FR-011a).</summary>
    public bool IsDirty { get; private set; }

    /// <summary>True when at least one audible-level channel is soloed (solo flags of hidden channels are ignored).</summary>
    public bool AnySolo => _byStem.Values.Any(c => c.Solo && !IsHidden(c));

    /// <summary>
    /// Mirror of the engine's gate (contract 2.0.0): hidden and muted channels are silent; with any audible-level
    /// solo only soloed channels play.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>Whether the channel is audible.</returns>
    public bool IsAudible(ChannelState channel) => !IsHidden(channel) && !channel.Mute && (!AnySolo || channel.Solo);

    /// <summary>Gets a channel by engine id (stem name or <c>&lt;parent&gt;.&lt;detail&gt;</c>).</summary>
    /// <param name="stem">The channel id.</param>
    /// <returns>The channel.</returns>
    public ChannelState Channel(string stem) =>
        _byStem.TryGetValue(stem, out var channel) ? channel : throw new ArgumentException($"Unknown stem '{stem}'.", nameof(stem));

    /// <summary>Sets a fader (clamped 0..1).</summary>
    /// <param name="stem">The stem.</param>
    /// <param name="volume">The gain.</param>
    /// <returns>The channel settings for the engine.</returns>
    public ChannelSettingsDto SetVolume(string stem, double volume) => Change(stem, c => c.Volume = Math.Clamp(volume, 0, 1));

    /// <summary>Sets a pan (clamped -1..1).</summary>
    /// <param name="stem">The stem.</param>
    /// <param name="pan">The pan.</param>
    /// <returns>The channel settings for the engine.</returns>
    public ChannelSettingsDto SetPan(string stem, double pan) => Change(stem, c => c.Pan = Math.Clamp(pan, -1, 1));

    /// <summary>Toggles mute.</summary>
    /// <param name="stem">The stem.</param>
    /// <returns>The channel settings for the engine.</returns>
    public ChannelSettingsDto ToggleMute(string stem) => Change(stem, c => c.Mute = !c.Mute);

    /// <summary>Toggles solo.</summary>
    /// <param name="stem">The stem.</param>
    /// <returns>The channel settings for the engine.</returns>
    public ChannelSettingsDto ToggleSolo(string stem) => Change(stem, c => c.Solo = !c.Solo);

    /// <summary>Sets the master gain (clamped 0..1).</summary>
    /// <param name="volume">The gain.</param>
    /// <returns>The clamped gain for the engine.</returns>
    public double SetMaster(double volume)
    {
        MasterVolume = Math.Clamp(volume, 0, 1);
        IsDirty = true;
        return MasterVolume;
    }

    private ChannelSettingsDto Change(string stem, Action<ChannelState> change)
    {
        var channel = Channel(stem);
        change(channel);
        IsDirty = true;
        return channel.ToDto();
    }
}
