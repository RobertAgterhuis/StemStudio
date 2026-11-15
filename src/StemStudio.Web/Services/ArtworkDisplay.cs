using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>The two stored sizes of a track picture (feature 005, FR-007).</summary>
public enum ArtworkVariant
{
    /// <summary>600 px cover (track detail, mixer header).</summary>
    Cover,

    /// <summary>160 px thumbnail (track list rows).</summary>
    Thumb,
}

/// <summary>Display rules of track pictures and their placeholder (feature 005, FR-006, ui-test-ids 1.4.0).</summary>
public static class ArtworkDisplay
{
    private const string NoInitials = "?";

    /// <summary>The <c>data-artwork-source</c> value: the contract source, or <c>none</c> without a picture.</summary>
    /// <param name="artwork">The track's picture, or null.</param>
    /// <returns>For example <c>coverartarchive</c>.</returns>
    public static string SourceName(ArtworkDto? artwork) => artwork?.Source switch
    {
        ArtworkSourceDto.User => "user",
        ArtworkSourceDto.Spotify => "spotify",
        ArtworkSourceDto.Embedded => "embedded",
        ArtworkSourceDto.CoverArtArchive => "coverartarchive",
        ArtworkSourceDto.Youtube => "youtube",
        _ => "none",
    };

    /// <summary>The browser URL of one variant (the API URL is relative, like the stems).</summary>
    /// <param name="publicBase">API base URL as seen by the browser.</param>
    /// <param name="artwork">The picture.</param>
    /// <param name="variant">Cover or thumbnail.</param>
    /// <returns>The absolute URL.</returns>
    public static string Url(Uri publicBase, ArtworkDto artwork, ArtworkVariant variant) =>
        new Uri(publicBase, variant == ArtworkVariant.Cover ? artwork.CoverUrl : artwork.ThumbUrl).ToString();

    /// <summary>The pixel size of a variant (width = height, the pictures are square).</summary>
    /// <param name="variant">Cover or thumbnail.</param>
    /// <returns>600 or 160.</returns>
    public static int Size(ArtworkVariant variant) => variant == ArtworkVariant.Cover ? 600 : 160;

    /// <summary>Up to two initials of the title: the first letter or digit of its first two words.</summary>
    /// <param name="title">The track title.</param>
    /// <returns>Upper-case initials, or <c>?</c> when the title has no letters or digits.</returns>
    public static string Initials(string title)
    {
        var initials = title
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.FirstOrDefault(char.IsLetterOrDigit))
            .Where(c => c != default)
            .Take(2)
            .ToArray();
        return initials.Length == 0 ? NoInitials : new string(initials).ToUpperInvariant();
    }

    /// <summary>A stable stem colour name for the placeholder (FNV-1a hash of the title), never a raw colour.</summary>
    /// <param name="title">The track title.</param>
    /// <returns>One of <see cref="StemNames.SixStem"/>.</returns>
    public static string Tone(string title)
    {
        var hash = 2166136261u;
        foreach (var c in title)
        {
            hash = unchecked((hash ^ c) * 16777619u);
        }

        return StemNames.SixStem[(int)(hash % (uint)StemNames.SixStem.Count)];
    }
}
