using System.Globalization;
using System.Text;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>Query of <c>getLibraryRoot</c>/<c>getLibraryFolder</c> (backend API 1.22.0): order, filters and paging.</summary>
/// <param name="Sort">The child order; null = the default order of the level.</param>
/// <param name="Kind">Only items of this kind (song level).</param>
/// <param name="Status">Only items with this status (song level).</param>
/// <param name="Limit">Children per page (1..500); null = 200.</param>
/// <param name="Offset">Children to skip; null = 0.</param>
public sealed record LibraryFolderRequest(
    FolderSortDto? Sort = null,
    AssetKindDto? Kind = null,
    AssetStatusDto? Status = null,
    int? Limit = null,
    int? Offset = null)
{
    /// <summary>The default request (default order, no filter, first page).</summary>
    public static LibraryFolderRequest Default { get; } = new();

    /// <summary>The query string, empty or starting with <c>?</c>.</summary>
    /// <returns>For example <c>?sort=year&amp;limit=50</c>.</returns>
    public string ToQueryString()
    {
        var query = new StringBuilder();
        Add(query, "sort", Sort is { } sort ? Wire(sort) : null);
        Add(query, "kind", Kind is { } kind ? Wire(kind) : null);
        Add(query, "status", Status is { } status ? Wire(status) : null);
        Add(query, "limit", Limit?.ToString(CultureInfo.InvariantCulture));
        Add(query, "offset", Offset?.ToString(CultureInfo.InvariantCulture));
        return query.ToString();
    }

    /// <summary>The wire value of an enum whose members are the lower-case names (contract enums of this tag).</summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>For example <c>recent</c>.</returns>
    public static string Wire<T>(T value)
        where T : struct, Enum => value.ToString().ToLowerInvariant();

    /// <summary>Appends <c>name=value</c> (escaped) when the value is set.</summary>
    /// <param name="query">The query being built.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value, or null to skip.</param>
    internal static void Add(StringBuilder query, string name, string? value)
    {
        if (value is not null)
        {
            query.Append(query.Length == 0 ? '?' : '&').Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }
    }
}

/// <summary>Query of <c>searchLibrary</c> (backend API 1.22.0).</summary>
/// <param name="Query">The typed text (1..100 characters after trimming).</param>
/// <param name="Within">Limits the search to this folder (Advanced, "Only in this folder").</param>
/// <param name="Limit">Hits per group (1..200); null = 20.</param>
public sealed record LibrarySearchRequest(string Query, string? Within = null, int? Limit = null)
{
    /// <summary>The query string, starting with <c>?q=</c>.</summary>
    /// <returns>For example <c>?q=snare&amp;within=ar0123456789abcdef</c>.</returns>
    public string ToQueryString()
    {
        var query = new StringBuilder();
        LibraryFolderRequest.Add(query, "q", Query);
        LibraryFolderRequest.Add(query, "within", Within);
        LibraryFolderRequest.Add(query, "limit", Limit?.ToString(CultureInfo.InvariantCulture));
        return query.ToString();
    }
}
