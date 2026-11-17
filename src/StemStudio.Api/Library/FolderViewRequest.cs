using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;

namespace StemStudio.Api.Library;

/// <summary>Query string of the folder view (backend-api 1.22.0 parameters <c>FolderSort</c>, <c>KindFilter</c>, <c>StatusFilter</c>, paging).</summary>
/// <param name="Sort">Child order.</param>
/// <param name="Kind">Item kind filter.</param>
/// <param name="Status">Item status filter.</param>
/// <param name="Limit">Child folders per page (1..500).</param>
/// <param name="Offset">Child folders to skip (0 or more).</param>
public sealed record FolderViewRequest(string? Sort, string? Kind, string? Status, int? Limit, int? Offset)
{
    /// <summary>Validates the request for a folder.</summary>
    /// <param name="folderId">The folder id from the route, or null for the root.</param>
    /// <param name="errors">Collects the violated rules (field = parameter name).</param>
    /// <returns>The query (valid only when no error was added).</returns>
    public FolderQuery ToQuery(string? folderId, List<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var level = CheckFolder(folderId, errors);
        var query = new FolderQuery
        {
            FolderId = folderId,
            Sort = QueryParsing.Enum<FolderSortDto>(Sort, "sort", errors),
            Kind = QueryParsing.Enum<AssetKind>(Kind, "kind", errors),
            Status = QueryParsing.Enum<AssetStatus>(Status, "status", errors),
            Limit = Limit ?? ContractLimits.LibraryFolderPageDefault,
            Offset = Offset ?? 0,
        };
        Check(errors, query.Sort != FolderSortDto.Year || level == FolderLevel.Artist, "sort", "Sort by year is available for the albums of an artist only.");
        Check(errors, query.Limit is >= 1 and <= ContractLimits.LibraryFolderPageMax, "limit", $"Must be between 1 and {ContractLimits.LibraryFolderPageMax}.");
        Check(errors, query.Offset >= 0, "offset", "Must be 0 or more.");
        return query;
    }

    private static FolderLevel? CheckFolder(string? folderId, List<DomainError> errors)
    {
        if (folderId is null)
        {
            return null;
        }

        var valid = FolderId.TryParse(folderId, out var level);
        Check(errors, valid, "folderId", "Not a folder id (ar, al or so followed by 16 hex characters).");
        return valid ? level : null;
    }

    private static void Check(List<DomainError> errors, bool valid, string field, string message)
    {
        if (!valid)
        {
            errors.Add(new DomainError(field, message));
        }
    }
}
