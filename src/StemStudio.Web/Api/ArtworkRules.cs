using StemStudio.Contracts;

namespace StemStudio.Web.Api;

/// <summary>Client-side checks of an own picture (feature 005, FR-013); the API and the worker repeat them.</summary>
public static class ArtworkRules
{
    /// <summary>Message for a file that is not JPEG, PNG or WebP.</summary>
    public const string UnsupportedTypeMessage = "Only JPEG, PNG and WebP pictures are supported.";

    /// <summary>Message for a file above <see cref="ContractLimits.MaxArtworkUploadBytes"/>.</summary>
    public const string TooLargeMessage = "The picture is larger than 10 MB (10,485,760 bytes).";

    /// <summary>Message for an empty file.</summary>
    public const string EmptyMessage = "The picture file is empty.";

    /// <summary>The <c>accept</c> attribute of the file input.</summary>
    public static string Accept { get; } = string.Join(",", ContractLimits.ArtworkMediaTypes);

    /// <summary>Validates a selected picture.</summary>
    /// <param name="contentType">The MIME type reported by the browser.</param>
    /// <param name="size">The size in bytes.</param>
    /// <returns>A readable reason, or null when the picture may be uploaded.</returns>
    public static string? Validate(string contentType, long size)
    {
        if (!ContractLimits.ArtworkMediaTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return UnsupportedTypeMessage;
        }

        return size switch
        {
            <= 0 => EmptyMessage,
            > ContractLimits.MaxArtworkUploadBytes => TooLargeMessage,
            _ => null,
        };
    }
}
