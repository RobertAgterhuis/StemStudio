using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>Client-side checks of corrected lookup names (feature 005, FR-009); the API repeats them (422).</summary>
public static class MetadataNames
{
    /// <summary>Message for an empty or too long title.</summary>
    public const string TitleMessage = "Enter a title of 1 to 500 characters.";

    /// <summary>Message for an empty or too long artist.</summary>
    public const string ArtistMessage = "Enter an artist of 1 to 500 characters.";

    /// <summary>Validates both names after trimming.</summary>
    /// <param name="title">The entered title.</param>
    /// <param name="artist">The entered artist.</param>
    /// <returns>The first readable problem, or null when both names are valid.</returns>
    public static string? Validate(string title, string artist) =>
        !IsValid(title) ? TitleMessage
        : !IsValid(artist) ? ArtistMessage
        : null;

    /// <summary>The lookup request with trimmed names.</summary>
    /// <param name="title">The entered title.</param>
    /// <param name="artist">The entered artist.</param>
    /// <returns>The request body.</returns>
    public static MetadataLookupRequest Request(string title, string artist) => new(title.Trim(), artist.Trim());

    private static bool IsValid(string value) => value.Trim().Length is > 0 and <= ContractLimits.MetadataNameMaxLength;
}
