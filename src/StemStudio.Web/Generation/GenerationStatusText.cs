using StemStudio.Contracts.Generation;
using StemStudio.Web.Services;

namespace StemStudio.Web.Generation;

/// <summary>Texts of the generation status banner (009 FR-010, ui-test-ids 1.8.0).</summary>
public static class GenerationStatusText
{
    /// <summary>Reason shown when the API gives none.</summary>
    public const string DefaultReason = "Generation is not available on this computer.";

    private static readonly Dictionary<GenerationAvailabilityDto, string> Titles = new()
    {
        [GenerationAvailabilityDto.NotInstalled] = "Generation is not installed.",
        [GenerationAvailabilityDto.Unavailable] = "Generation is not available.",
        [GenerationAvailabilityDto.Ready] = "Generation is ready.",
    };

    /// <summary>The headline of a state.</summary>
    /// <param name="status">The status.</param>
    /// <returns>For example "Generation is ready.".</returns>
    public static string Title(GenerationStatusDto status) => Titles[status.State];

    /// <summary>Why generation cannot be used, or null when it is ready.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The reason from the API, or <see cref="DefaultReason"/>.</returns>
    public static string? Reason(GenerationStatusDto status) =>
        status.State == GenerationAvailabilityDto.Ready ? null : status.Reason ?? DefaultReason;

    /// <summary>Why the voice features (the singer service) cannot be used, or null when they are ready.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The singer's reason, or a default text.</returns>
    public static string? VoiceReason(GenerationStatusDto status) =>
        status.Voice.State == GenerationAvailabilityDto.Ready ? null : status.Voice.Reason ?? "The voice features are not available on this computer.";

    /// <summary>"First use downloads about X of model files." while ready and models are missing.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The notice, or null.</returns>
    public static string? DownloadNotice(GenerationStatusDto status) =>
        status.State == GenerationAvailabilityDto.Ready && status.DownloadBytesNeeded > 0
            ? $"First use downloads about {ByteSize.Format(status.DownloadBytesNeeded)} of model files."
            : null;
}
