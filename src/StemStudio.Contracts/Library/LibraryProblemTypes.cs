namespace StemStudio.Contracts.Library;

/// <summary>Problem <c>type</c> codes of the <c>library</c> endpoints (<c>https://stemstudio.local/problems/&lt;code&gt;</c>, backend-api 1.22.0).</summary>
public static class LibraryProblemTypes
{
    /// <summary>Base URI of every problem type.</summary>
    public const string BaseUri = "https://stemstudio.local/problems/";

    /// <summary>404: the folder id is well-formed but no item is filed in it (any more); also an unknown <c>within</c>.</summary>
    public const string FolderNotFound = "folder-not-found";

    /// <summary>422: invalid folder id, sort, filter, paging or search text (<c>errors</c> names the parameter).</summary>
    public const string LibraryQueryInvalid = "library-query-invalid";

    /// <summary>409: the item is a recorded play-along take (015); it lives with its track and is not filed.</summary>
    public const string NotFileable = "not-fileable";

    /// <summary>The full <c>type</c> URI of a code.</summary>
    /// <param name="code">One of the codes above.</param>
    /// <returns>The URI text.</returns>
    public static string Uri(string code) => BaseUri + code;
}
