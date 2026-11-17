using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>One picture of a folder's art: the thumb as <c>src</c> and, for a full cover, a <c>srcset</c> with the cover.</summary>
/// <param name="Src">The thumb URL (160 px).</param>
/// <param name="SrcSet">For example <c>.../thumb 160w, .../cover 600w</c>; null for mosaic cells (thumb only).</param>
public sealed record ArtPicture(string Src, string? SrcSet);

/// <summary>
/// What a folder's art renders (feature 022, research R-05, data-model 7): one cover, a 2x2 mosaic of 2-4 thumbs (empty
/// cells get the placeholder tone) or the lettered placeholder. Picture URLs must be API artwork paths
/// (<c>/api/tracks/...</c>); anything else falls back to the placeholder, so the page never loads a foreign picture.
/// </summary>
public sealed class FolderArtModel
{
    /// <summary>Tile edge in CSS px at comfortable density.</summary>
    public const int ComfortableSize = 160;

    /// <summary>Tile edge in CSS px at compact density.</summary>
    public const int CompactSize = 128;

    /// <summary>The width of the cover in the <c>srcset</c>.</summary>
    public const int CoverWidth = 600;

    private const string ArtworkPathPrefix = "/api/tracks/";

    private FolderArtModel(FolderArtKindDto kind, IReadOnlyList<ArtPicture> pictures, ArtPlaceholderDto placeholder, int size)
    {
        (Kind, Pictures, Glyph, Tone, Size) = (kind, pictures, placeholder.Glyph, placeholder.Tone, size);
    }

    /// <summary>What renders: cover, mosaic or placeholder (also <c>data-art</c>).</summary>
    public FolderArtKindDto Kind { get; }

    /// <summary>The pictures: 1 for a cover, 2-4 for a mosaic, none for the placeholder.</summary>
    public IReadOnlyList<ArtPicture> Pictures { get; }

    /// <summary>Mosaic cells without a picture (4 minus the pictures), drawn in the placeholder tone.</summary>
    public int EmptyCells => Kind == FolderArtKindDto.Mosaic ? ContractLimits.FolderArtMosaicMax - Pictures.Count : 0;

    /// <summary>The placeholder letter (one grapheme; <c>?</c> when the API sent none).</summary>
    public string Glyph { get; }

    /// <summary>The tone 0..7.</summary>
    public int Tone { get; }

    /// <summary>The tile edge in CSS px (also the image <c>width</c>/<c>height</c>).</summary>
    public int Size { get; }

    /// <summary>The CSS class of the tone (design tokens <c>--ss-art-tone-0..7</c>).</summary>
    public string ToneClass => $"ss-art-tone-{Tone.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The <c>sizes</c> attribute of a cover: the tile width.</summary>
    public string Sizes => $"{Size.ToString(CultureInfo.InvariantCulture)}px";

    /// <summary>The edge of one mosaic cell in CSS px.</summary>
    public int CellSize => Size / 2;

    /// <summary>The model of a folder's art.</summary>
    /// <param name="art">The art of the API.</param>
    /// <param name="apiBase">The API base URL the browser uses.</param>
    /// <param name="compact">True at compact density (128 px tiles).</param>
    /// <returns>The model.</returns>
    public static FolderArtModel From(FolderArtDto art, Uri apiBase, bool compact = false)
    {
        var size = compact ? CompactSize : ComfortableSize;
        var glyph = string.IsNullOrWhiteSpace(art.Placeholder.Glyph) ? "?" : art.Placeholder.Glyph;
        var tone = ((art.Placeholder.Tone % ContractLimits.ArtToneCount) + ContractLimits.ArtToneCount) % ContractLimits.ArtToneCount;
        var images = art.Images.Where(i => IsArtworkPath(i.ThumbUrl) && IsArtworkPath(i.CoverUrl)).Take(ContractLimits.FolderArtMosaicMax).ToList();
        var kind = KindOf(art.Kind, images.Count);
        IReadOnlyList<ArtPicture> pictures = kind switch
        {
            FolderArtKindDto.Cover => [Cover(images[0], apiBase)],
            FolderArtKindDto.Mosaic => [.. images.Select(i => new ArtPicture(Absolute(apiBase, i.ThumbUrl), null))],
            _ => [],
        };
        return new FolderArtModel(kind, pictures, new ArtPlaceholderDto { Glyph = glyph, Tone = tone }, size);
    }

    private static FolderArtKindDto KindOf(FolderArtKindDto requested, int images) => (requested, images) switch
    {
        (_, 0) => FolderArtKindDto.Placeholder,
        (FolderArtKindDto.Mosaic, > 1) => FolderArtKindDto.Mosaic,
        (FolderArtKindDto.Placeholder, _) => FolderArtKindDto.Placeholder,
        _ => FolderArtKindDto.Cover,
    };

    private static ArtPicture Cover(ArtImageDto image, Uri apiBase)
    {
        var thumb = Absolute(apiBase, image.ThumbUrl);
        var cover = Absolute(apiBase, image.CoverUrl);
        return new ArtPicture(thumb, $"{thumb} {ComfortableSize.ToString(CultureInfo.InvariantCulture)}w, {cover} {CoverWidth.ToString(CultureInfo.InvariantCulture)}w");
    }

    private static bool IsArtworkPath(string url) =>
        url.StartsWith(ArtworkPathPrefix, StringComparison.Ordinal) && !url.Contains("..", StringComparison.Ordinal) && !url.Contains(' ', StringComparison.Ordinal);

    private static string Absolute(Uri apiBase, string path) => new Uri(apiBase, path).ToString();
}
