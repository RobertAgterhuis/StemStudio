using System.Text.Json;
using Microsoft.JSInterop;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Recording;

/// <summary>The recording choices of this browser (015 data-model 4).</summary>
public sealed record RecordingSettings
{
    /// <summary>Label of the last used input, or null.</summary>
    public string? DeviceLabel { get; init; }

    /// <summary>Channel mode.</summary>
    public InputChannelModeDto InputMode { get; init; } = InputChannelModeDto.Mono1;

    /// <summary>Recorded part.</summary>
    public RecordingPartDto Part { get; init; } = RecordingPartDto.Vocals;

    /// <summary>Gate the original part while recording.</summary>
    public bool MuteOriginal { get; init; } = true;

    /// <summary>Count-in bars 0..2.</summary>
    public int CountInBars { get; init; } = 1;

    /// <summary>Direct monitoring through the browser.</summary>
    public bool Monitoring { get; init; }

    /// <summary>Monitoring gain 0..1.</summary>
    public double MonitoringVolume { get; init; } = 0.7;

    /// <summary>The defaults.</summary>
    public static RecordingSettings Default { get; } = new();
}

/// <summary>
/// Keeps <see cref="RecordingSettings"/> in <c>localStorage["stemstudio.recording"]</c> (the <c>recording.*</c> keys of
/// data-model 4, next to the 004 record like the karaoke choices). Unusable or out-of-range values fall back to the
/// defaults field by field; unavailable storage gives the defaults and skips writes.
/// </summary>
/// <param name="js">The circuit's JS runtime.</param>
public sealed class RecordingPreferences(IJSRuntime js)
{
    /// <summary>The local-storage key.</summary>
    public const string StorageKey = "stemstudio.recording";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the stored choices.</summary>
    /// <returns>The choices, or the defaults.</returns>
    public async Task<RecordingSettings> LoadAsync() => Parse(await BrowserStorage.GetAsync(js, StorageKey));

    /// <summary>Stores the choices (best effort).</summary>
    /// <param name="settings">The choices.</param>
    /// <returns>A task that completes when the write was tried.</returns>
    public Task SaveAsync(RecordingSettings settings) =>
        BrowserStorage.SetAsync(js, StorageKey, JsonSerializer.Serialize(settings, Json));

    /// <summary>Parses a stored record: missing fields keep their defaults, out-of-range values get them back.</summary>
    /// <param name="stored">The stored JSON, or null.</param>
    /// <returns>The choices; the defaults when the record is unreadable.</returns>
    public static RecordingSettings Parse(string? stored) => Read(stored) is { } raw ? Validated(raw) : RecordingSettings.Default;

    private static RecordingSettings Validated(RecordingSettings raw) => raw with
    {
        DeviceLabel = Label(raw.DeviceLabel),
        CountInBars = raw.CountInBars is >= 0 and <= 2 ? raw.CountInBars : RecordingSettings.Default.CountInBars,
        MonitoringVolume = raw.MonitoringVolume is >= 0 and <= 1 ? raw.MonitoringVolume : RecordingSettings.Default.MonitoringVolume,
    };

    private static RecordingSettings? Read(string? stored)
    {
        try
        {
            return stored is null ? null : JsonSerializer.Deserialize<RecordingSettings>(stored, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Label(string? label) =>
        string.IsNullOrWhiteSpace(label) || label.Length > RecordingLimits.MaxDeviceLabelLength ? null : label;
}
