using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Result of a metronome edit: the new settings, or why the input was refused.</summary>
/// <param name="Settings">The new settings; null when refused.</param>
/// <param name="Error">The reason; null when accepted.</param>
public sealed record MetronomeEdit(MetronomeSettingsDto? Settings, string? Error);

/// <summary>Validates manual metronome corrections (FR-014: BPM 30-300, offset -2000..2000 ms, beats per bar 1-12).</summary>
public static class MetronomeRules
{
    /// <summary>Reason for an invalid BPM.</summary>
    public const string BpmMessage = "BPM must be a number between 30 and 300 (leave empty to follow the detected beats).";

    /// <summary>Reason for an invalid offset.</summary>
    public const string OffsetMessage = "Offset must be a whole number of milliseconds between -2000 and +2000.";

    /// <summary>Reason for an invalid beats-per-bar value.</summary>
    public const string BeatsPerBarMessage = "Beats per bar must be a whole number between 1 and 12.";

    /// <summary>Applies a BPM override; empty text clears it.</summary>
    /// <param name="current">The current settings.</param>
    /// <param name="text">The entered text.</param>
    /// <returns>The edit.</returns>
    public static MetronomeEdit Bpm(MetronomeSettingsDto current, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new(current with { BpmOverride = null }, null);
        }

        var ok = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm)
            && bpm is >= ContractLimits.MinBpm and <= ContractLimits.MaxBpm;
        return ok ? new(current with { BpmOverride = bpm }, null) : new(null, BpmMessage);
    }

    /// <summary>Applies a grid offset in milliseconds.</summary>
    /// <param name="current">The current settings.</param>
    /// <param name="text">The entered text.</param>
    /// <returns>The edit.</returns>
    public static MetronomeEdit Offset(MetronomeSettingsDto current, string? text) =>
        InputValue.Whole(text, ContractLimits.MinOffsetMs, ContractLimits.MaxOffsetMs) is { } ms
            ? new(current with { OffsetMs = ms }, null)
            : new(null, OffsetMessage);

    /// <summary>Applies beats per bar.</summary>
    /// <param name="current">The current settings.</param>
    /// <param name="text">The entered text.</param>
    /// <returns>The edit.</returns>
    public static MetronomeEdit BeatsPerBar(MetronomeSettingsDto current, string? text) =>
        InputValue.Whole(text, ContractLimits.MinBeatsPerBar, ContractLimits.MaxBeatsPerBar) is { } beats
            ? new(current with { BeatsPerBar = beats }, null)
            : new(null, BeatsPerBarMessage);

    /// <summary>Applies the click volume (clamped 0..1).</summary>
    /// <param name="current">The current settings.</param>
    /// <param name="volume">The gain.</param>
    /// <returns>The new settings.</returns>
    public static MetronomeSettingsDto Volume(MetronomeSettingsDto current, double volume) =>
        current with { Volume = Math.Clamp(volume, 0, 1) };
}
