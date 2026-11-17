namespace StemStudio.Contracts;

/// <summary>Routes of the library folders, live search and library info (backend-api 1.22.0, feature 022, tag <c>library</c>).</summary>
public static partial class ApiRoutes
{
    /// <summary>GET: the library root (artist folders); query <c>sort</c>, <c>kind</c>, <c>status</c>, <c>limit</c>, <c>offset</c>.</summary>
    public const string LibraryFolders = "/api/library/folders";

    /// <summary>GET: the live search; query <c>q</c> (required), <c>within</c>, <c>limit</c>.</summary>
    public const string LibrarySearch = "/api/library/search";

    /// <summary>Prefix of the library ETag value (<c>"lib-&lt;revision&gt;"</c>, quotes included).</summary>
    public const string LibraryETagPrefix = "\"lib-";

    /// <summary>GET: an artist, album or song folder.</summary>
    /// <param name="folderId">Folder id (<see cref="ContractLimits.FolderIdPattern"/>).</param>
    /// <returns>The route.</returns>
    public static string LibraryFolder(string folderId) => $"{LibraryFolders}/{folderId}";

    /// <summary>GET, PUT, DELETE: library info of a track (the names used to file all its items).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <returns>The route.</returns>
    public static string TrackLibraryInfo(Guid trackId) => $"{Tracks}/{trackId}/library-info";

    /// <summary>GET, PUT, DELETE: library info of one library item.</summary>
    /// <param name="assetId">Item identifier.</param>
    /// <returns>The route.</returns>
    public static string AssetLibraryInfo(Guid assetId) => $"{Asset(assetId)}/library-info";

    /// <summary>The <c>ETag</c> header value of a library revision.</summary>
    /// <param name="revision">Library revision (16 hex).</param>
    /// <returns><c>"lib-&lt;revision&gt;"</c> with the quotes.</returns>
    public static string LibraryETag(string revision) => $"{LibraryETagPrefix}{revision}\"";
}
