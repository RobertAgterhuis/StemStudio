using StemStudio.Contracts;

namespace StemStudio.Web.Api;

/// <summary>Client-side upload checks (FR-003, FR-004); the API repeats them authoritatively.</summary>
public static class UploadRules
{
    /// <summary>Message for a file that is not .mp3, .wav or .flac.</summary>
    public const string UnsupportedTypeMessage = "Only MP3, WAV and FLAC files are supported.";

    /// <summary>Message for a file above <see cref="ContractLimits.MaxUploadBytes"/>.</summary>
    public const string TooLargeMessage = "The file is larger than 200 MiB (209,715,200 bytes).";

    /// <summary>Message for an empty file.</summary>
    public const string EmptyMessage = "The file is empty.";

    /// <summary>The <c>accept</c> attribute of the file input.</summary>
    public static string Accept { get; } = string.Join(",", ContractLimits.UploadExtensions);

    /// <summary>Validates a selected file.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="size">The size in bytes.</param>
    /// <returns>A readable reason, or null when the file may be uploaded.</returns>
    public static string? Validate(string fileName, long size)
    {
        var extension = Path.GetExtension(fileName);
        if (!ContractLimits.UploadExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return UnsupportedTypeMessage;
        }

        return size switch
        {
            <= 0 => EmptyMessage,
            > ContractLimits.MaxUploadBytes => TooLargeMessage,
            _ => null,
        };
    }
}
