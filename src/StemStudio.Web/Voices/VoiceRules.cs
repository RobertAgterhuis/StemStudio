using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Generation;

namespace StemStudio.Web.Voices;

/// <summary>Checks and texts of the voice features (010 FR-005, FR-006, FR-012).</summary>
public static class VoiceRules
{
    /// <summary>The longest recording in seconds; the recorder stops by itself.</summary>
    public const int MaxRecordingSeconds = (int)ContractLimits.MaxVoiceSampleSeconds;

    /// <summary>The <c>accept</c> attribute of the sample input.</summary>
    public const string Accept = ".wav,.flac,.mp3,.webm,audio/*";

    private static readonly string[] Extensions = [".wav", ".flac", ".mp3", ".webm"];

    /// <summary>Why a file cannot be used as a sample, or null (the API checks the singing itself).</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The readable problem.</returns>
    public static string? FileProblem(string fileName, long size) =>
        !Extensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()) ? "Choose a WAV, FLAC, MP3 or WebM recording."
        : size > ContractLimits.MaxVoiceSampleBytes ? "A recording can be at most 20 MB."
        : null;

    /// <summary>The singer's reason when profiles cannot be saved (singer not installed), else null.</summary>
    /// <param name="status">The generation status.</param>
    /// <returns>The reason.</returns>
    public static string? SaveBlocked(GenerationStatusDto status) =>
        status.Voice.State == GenerationAvailabilityDto.NotInstalled ? GenerationStatusText.VoiceReason(status) : null;

    /// <summary>The singer's reason when takes cannot be converted (not ready), else null.</summary>
    /// <param name="status">The generation status.</param>
    /// <returns>The reason.</returns>
    public static string? ConversionBlocked(GenerationStatusDto status) => GenerationStatusText.VoiceReason(status);

    /// <summary>The pitch shift from its input: empty means 0, values are clamped to -12..12 semitones.</summary>
    /// <param name="value">The parsed input.</param>
    /// <returns>The semitones.</returns>
    public static int Pitch(double? value) => (int)Math.Clamp(value ?? 0, -12, 12);

    /// <summary>The consent record of a profile as one line.</summary>
    /// <param name="consent">The record.</param>
    /// <returns>For example "Statement consent.v1, agreed by Sam Example on 14 Nov 2026".</returns>
    public static string ConsentLine(VoiceConsentDto consent) =>
        $"Statement {consent.StatementVersion}, agreed by {consent.PersonName} on {consent.ConsentedAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
}
