using System.Text;
using System.Text.RegularExpressions;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>
/// Where the user is in the library (feature 022, research R-09): <c>/library</c> (artists), <c>/library/{folderId}</c>
/// (any level, the id prefix gives the level), <c>?q=</c> (the live search) and <c>?view=all</c> (the 007 flat list).
/// Parses browser URLs and builds the relative links the pages use.
/// </summary>
/// <param name="FolderId">The open folder, or null at the root.</param>
/// <param name="Query">The search text, or null.</param>
/// <param name="AllItems">True for the "All items" view.</param>
public sealed partial record LibraryLocation(string? FolderId, string? Query, bool AllItems)
{
    /// <summary>The relative route of the library root.</summary>
    public const string RootPath = "library";

    /// <summary>The library root without search.</summary>
    public static LibraryLocation Root { get; } = new(null, null, false);

    /// <summary>The "All items" view.</summary>
    public static LibraryLocation All { get; } = new(null, null, true);

    /// <summary>True when the open folder id has the contract format (<c>ar|al|so</c> + 16 hex); false at the root.</summary>
    public bool HasValidFolder => FolderId is not null && FolderIdFormat().IsMatch(FolderId);

    /// <summary>The level of the open folder from its id prefix, or null at the root or for an invalid id.</summary>
    public FolderLevelDto? Level => HasValidFolder ? LevelOf(FolderId!) : null;

    /// <summary>True while the search text is not blank (the results replace the folder view).</summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(Query);

    /// <summary>Parses a browser URL (absolute or relative to the base) into a location.</summary>
    /// <param name="uri">The URL, e.g. <c>http://host/library/al0123456789abcdef?q=snare</c>.</param>
    /// <returns>The location; a path outside the library gives <see cref="Root"/>.</returns>
    public static LibraryLocation Parse(string uri)
    {
        var (path, query) = Split(uri);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindLastIndex(segments, s => s.Equals(RootPath, StringComparison.OrdinalIgnoreCase));
        var folder = index >= 0 && index + 1 < segments.Length ? Uri.UnescapeDataString(segments[index + 1]) : null;
        var parameters = ParseQuery(query);
        var text = parameters.GetValueOrDefault("q");
        var all = folder is null && string.Equals(parameters.GetValueOrDefault("view"), "all", StringComparison.Ordinal);
        return new LibraryLocation(folder, Clip(text), all);
    }

    /// <summary>The level of a folder id by its prefix.</summary>
    /// <param name="folderId">A folder id in the contract format.</param>
    /// <returns>The level.</returns>
    public static FolderLevelDto LevelOf(string folderId) => folderId[..2] switch
    {
        "ar" => FolderLevelDto.Artist,
        "al" => FolderLevelDto.Album,
        _ => FolderLevelDto.Song,
    };

    /// <summary>The relative link of a folder.</summary>
    /// <param name="folderId">The folder id.</param>
    /// <returns><c>library/{folderId}</c>.</returns>
    public static string FolderUrl(string folderId) => $"{RootPath}/{Uri.EscapeDataString(folderId)}";

    /// <summary>The relative link of an item inside its song folder (ui-test-ids 1.22.0 <c>export-saved-open</c>).</summary>
    /// <param name="songFolderId">The song folder id.</param>
    /// <param name="assetId">The item.</param>
    /// <returns><c>library/{songFolderId}#asset-{assetId}</c>.</returns>
    public static string ItemUrl(string songFolderId, Guid assetId) => $"{FolderUrl(songFolderId)}#{ItemAnchor(assetId)}";

    /// <summary>The element id of an item row, the target of <see cref="ItemUrl"/>.</summary>
    /// <param name="assetId">The item.</param>
    /// <returns><c>asset-{assetId}</c>.</returns>
    public static string ItemAnchor(Guid assetId) => $"asset-{assetId:D}";

    /// <summary>The same place with another search text (blank removes it).</summary>
    /// <param name="query">The new text.</param>
    /// <returns>The new location.</returns>
    public LibraryLocation WithQuery(string? query) => this with { Query = string.IsNullOrWhiteSpace(query) ? null : Clip(query), AllItems = AllItems && string.IsNullOrWhiteSpace(query) };

    /// <summary>The relative URL of this location.</summary>
    /// <returns>For example <c>library/so0123456789abcdef?q=snare</c>.</returns>
    public string ToUrl()
    {
        var url = new StringBuilder(FolderId is null ? RootPath : FolderUrl(FolderId));
        if (IsSearching)
        {
            url.Append("?q=").Append(Uri.EscapeDataString(Query!));
        }
        else if (AllItems && FolderId is null)
        {
            url.Append("?view=all");
        }

        return url.ToString();
    }

    private static string? Clip(string? text) =>
        text is { Length: > ContractLimits.LibrarySearchMaxLength } ? text[..ContractLimits.LibrarySearchMaxLength] : text;

    private static (string Path, string Query) Split(string uri)
    {
        var withoutFragment = uri.Split('#', 2)[0];
        var parts = withoutFragment.Split('?', 2);
        var path = Uri.TryCreate(parts[0], UriKind.Absolute, out var absolute) ? absolute.AbsolutePath : parts[0];
        return (path, parts.Length > 1 ? parts[1] : string.Empty);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var name = Unescape(parts[0]);
            values.TryAdd(name, parts.Length > 1 ? Unescape(parts[1]) : string.Empty);
        }

        return values;
    }

    private static string Unescape(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    [GeneratedRegex(ContractLimits.FolderIdPattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex FolderIdFormat();
}
