using System.Globalization;
using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Recording;

/// <summary>Why recording is not possible right now (<c>rec-unavailable</c> <c>data-reason</c>, ui-test-ids 1.14.0).</summary>
/// <param name="Reason"><c>insecure</c>, <c>unsupported</c>, <c>denied</c>, <c>not-found</c>, <c>busy</c> or <c>lost</c>.</param>
/// <param name="Message">One readable sentence with the next step.</param>
public sealed record RecordingUnavailable(string Reason, string Message);

/// <summary>Why Record is disabled (<c>rec-record-disabled-reason</c> <c>data-reason</c>).</summary>
/// <param name="Reason"><c>speed</c>, <c>trainer</c>, <c>limit</c>, <c>input</c> or <c>track</c>.</param>
/// <param name="Message">Readable reason.</param>
public sealed record RecordBlock(string Reason, string Message);

/// <summary>Texts and formatting of the recording UI (spec 015 US1-US2, FR-003, FR-009).</summary>
public static class RecordingText
{
    /// <summary>The privacy note (FR-003, FR-017).</summary>
    public const string Privacy = "Recordings stay on this computer: StemStudio sends them only to its own server here.";

    /// <summary>The headphones warning while monitoring (US1 scenario 6).</summary>
    public const string MonitoringWarning = "Use headphones while monitoring, or the speakers are recorded too.";

    /// <summary>The speed rule (FR-009).</summary>
    public const string SpeedRule = "Recording works at 100 % speed without transpose.";

    /// <summary>The parts in display order with their labels (FR-005; every family counts equally).</summary>
    public static IReadOnlyList<(RecordingPartDto Part, string Label)> Parts { get; } =
    [
        (RecordingPartDto.Vocals, "Vocals"), (RecordingPartDto.Guitar, "Guitar"), (RecordingPartDto.Bass, "Bass"),
        (RecordingPartDto.Piano, "Keys / piano"), (RecordingPartDto.Drums, "Drums"), (RecordingPartDto.Other, "Other / acoustic"),
        (RecordingPartDto.None, "None (no part muted)"),
    ];

    /// <summary>The channel modes with their labels (FR-001).</summary>
    public static IReadOnlyList<(InputChannelModeDto Mode, string Label)> Modes { get; } =
    [
        (InputChannelModeDto.Mono1, "Mono (input 1)"), (InputChannelModeDto.Mono2, "Mono (input 2)"), (InputChannelModeDto.Stereo, "Stereo (inputs 1 + 2)"),
    ];

    /// <summary>Maps the engine's unsupported reason code to the panel state.</summary>
    /// <param name="support">The engine's answer.</param>
    /// <returns>The unavailable state.</returns>
    public static RecordingUnavailable Unsupported(RecordingSupport support) => support.ReasonCode == "insecureContext"
        ? new("insecure", "Open StemStudio on the computer it runs on (http://localhost) to record; the browser blocks the microphone on network addresses.")
        : new("unsupported", support.Reason ?? "This browser cannot record here. Use Chrome or Edge.");

    /// <summary>Maps an engine rejection of <c>openInput</c> to the panel state, or null when it is not a device state.</summary>
    /// <param name="error">The rejection.</param>
    /// <returns>The unavailable state, or null.</returns>
    public static RecordingUnavailable? FromOpenError(RecordingException error) => error.Code switch
    {
        RecordingErrorCode.PermissionDenied => Denied,
        RecordingErrorCode.NotFound => new("not-found", "No microphone or audio input was found. Connect one and try again."),
        RecordingErrorCode.Busy => new("busy", "Another program is using the input. Close it and try again."),
        RecordingErrorCode.Unsupported => new("unsupported", error.Message),
        _ => null,
    };

    /// <summary>The denied state (FR-003).</summary>
    public static RecordingUnavailable Denied { get; } =
        new("denied", "The microphone is blocked. Allow it in the browser's address bar (site settings), then try again.");

    /// <summary>The unplugged state.</summary>
    /// <returns>The state.</returns>
    public static RecordingUnavailable Lost() => new("lost", "The input was disconnected. Connect it again and try again; a running take was kept.");

    /// <summary><c>m:ss.s</c> (rec-elapsed).</summary>
    /// <param name="seconds">Elapsed seconds.</param>
    /// <returns>The text.</returns>
    public static string Elapsed(double seconds)
    {
        var tenths = (long)Math.Floor(Math.Max(0, seconds) * 10);
        return string.Create(CultureInfo.InvariantCulture, $"{tenths / 600}:{tenths % 600 / 10.0:00.0}");
    }

    /// <summary>Milliseconds with one decimal.</summary>
    /// <param name="samples">Samples at 44.1 kHz.</param>
    /// <returns>For example <c>25.0 ms</c>.</returns>
    public static string Milliseconds(int samples) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(samples * 1000.0 / RecordingLimits.SampleRate, 1):0.0} ms");

    /// <summary>The label of a part.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The label.</returns>
    public static string PartLabel(RecordingPartDto part) => Parts.First(p => p.Part == part).Label;
}
