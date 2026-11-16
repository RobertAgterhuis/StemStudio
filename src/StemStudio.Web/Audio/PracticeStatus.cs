using System.Text.Json.Serialization;

namespace StemStudio.Web.Audio;

/// <summary>Where the time-stretch stage is (<c>StretchState</c> in types.ts 2.3.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StretchState>))]
public enum StretchState
{
    /// <summary>Not inserted: original speed and key, nothing loaded.</summary>
    [JsonStringEnumMemberName("bypass")]
    Bypass,

    /// <summary>The processor and the WASM module are being loaded.</summary>
    [JsonStringEnumMemberName("loading")]
    Loading,

    /// <summary>In the graph; speed and transpose are heard.</summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>Not usable in this browser; playback runs at the original speed and key.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}

/// <summary>Speed trainer state (<c>TrainerState</c> in types.ts 2.3.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrainerState>))]
public enum TrainerState
{
    /// <summary>Disabled, or no usable loop.</summary>
    [JsonStringEnumMemberName("off")]
    Off,

    /// <summary>Enabled and not playing.</summary>
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    /// <summary>Playing passes below the target.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>The target speed is reached; the loop keeps playing at it.</summary>
    [JsonStringEnumMemberName("target")]
    Target,
}

/// <summary>Speed trainer progress of a <see cref="PracticeStatus"/>.</summary>
/// <param name="State">Trainer state.</param>
/// <param name="Pass">Current pass (1-based), 0 when not running.</param>
/// <param name="SpeedPercent">Speed of the current pass in percent.</param>
public sealed record TrainerStatus(TrainerState State, int Pass, int SpeedPercent);

/// <summary>Count-in progress of a <see cref="PracticeStatus"/>.</summary>
/// <param name="Available">False without a usable tempo (no reliable grid and no manual BPM).</param>
/// <param name="Active">A count-in is running now.</param>
/// <param name="BeatsRemaining">Beats left N..1 while active, 0 otherwise.</param>
/// <param name="BeatsTotal">Bars x beats per bar of the metronome settings.</param>
public sealed record CountInStatus(bool Available, bool Active, int BeatsRemaining, int BeatsTotal);

/// <summary>
/// Live practice snapshot of the engine (<c>PracticeStatus</c> in types.ts 2.3.0): sent with <c>OnPracticeChanged</c>
/// and returned by <c>getPracticeStatus()</c>. Not persisted.
/// </summary>
/// <param name="SpeedPercent">Speed heard now (the trainer's speed while it runs).</param>
/// <param name="TransposeSemitones">Transpose heard now.</param>
/// <param name="Stretch">State of the time-stretch stage.</param>
/// <param name="StretchReason">Reason text; set only when <paramref name="Stretch"/> is unavailable.</param>
/// <param name="LatencyMs">Heard latency of the stage in ms while engaged, else 0.</param>
/// <param name="Underruns">Stretch FIFO underruns since the track was loaded (Expert diagnostics).</param>
/// <param name="Trainer">Speed trainer progress.</param>
/// <param name="CountIn">Count-in progress.</param>
public sealed record PracticeStatus(
    int SpeedPercent,
    int TransposeSemitones,
    StretchState Stretch,
    string? StretchReason,
    double LatencyMs,
    long Underruns,
    TrainerStatus Trainer,
    CountInStatus CountIn)
{
    /// <summary>The snapshot before the engine reports anything: original speed and key, stage bypassed.</summary>
    public static PracticeStatus Initial { get; } =
        new(100, 0, StretchState.Bypass, null, 0, 0, new TrainerStatus(TrainerState.Off, 0, 100), new CountInStatus(false, false, 0, 0));

    /// <summary>The snapshot after the stage could not be created: original speed and key, trainer off.</summary>
    /// <param name="reason">The engine's reason.</param>
    /// <returns>The snapshot.</returns>
    public static PracticeStatus ForUnavailable(string reason) => Initial with { Stretch = StretchState.Unavailable, StretchReason = reason };
}
