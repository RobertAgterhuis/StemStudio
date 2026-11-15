using System.Globalization;
using StemStudio.Contracts.Assets;

namespace StemStudio.Web.Services;

/// <summary>Readable texts of library items (feature 007, FR-005, FR-007).</summary>
public static class AssetText
{
    /// <summary>The <c>data-status</c> value.</summary>
    /// <param name="status">The status.</param>
    /// <returns>For example <c>rendering</c>.</returns>
    public static string Value(AssetStatusDto status) => status.ToString().ToLowerInvariant();

    /// <summary>The status text: "Queued", "Rendering 42 %", "Ready", "Failed".</summary>
    /// <param name="asset">The item.</param>
    /// <returns>The text.</returns>
    public static string Status(AssetDto asset) =>
        asset.Status == AssetStatusDto.Rendering ? $"Rendering {asset.Progress.ToString(CultureInfo.InvariantCulture)} %" : asset.Status.ToString();

    /// <summary>Format details of a ready audio item: "FLAC 16-bit", "MP3 320 kbps".</summary>
    /// <param name="audio">The audio facts, or null before the item is ready.</param>
    /// <returns>The text, or null.</returns>
    public static string? Format(AudioFactsDto? audio) => audio switch
    {
        null => null,
        { Mp3BitrateKbps: { } kbps } => $"{Name(audio.Format)} {kbps.ToString(CultureInfo.InvariantCulture)} kbps",
        { BitDepth: { } bits } => $"{Name(audio.Format)} {bits.ToString(CultureInfo.InvariantCulture)}-bit",
        _ => Name(audio.Format),
    };

    /// <summary>"Lowered by 1.8 dB to avoid clipping", or null when no gain was applied.</summary>
    /// <param name="audio">The audio facts.</param>
    /// <returns>The note, or null.</returns>
    public static string? GainNote(AudioFactsDto? audio) =>
        audio is { AppliedGainDb: < 0 } ? $"Lowered by {(-audio.AppliedGainDb).ToString("0.0", CultureInfo.InvariantCulture)} dB to avoid clipping" : null;

    private static string Name(AudioFileFormatDto format) => format.ToString().ToUpperInvariant();
}
