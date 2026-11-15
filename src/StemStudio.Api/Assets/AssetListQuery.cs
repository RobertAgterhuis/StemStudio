using StemStudio.Contracts;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>Query values of <c>GET /api/assets</c> as sent; <see cref="ToQuery"/> validates them.</summary>
/// <param name="Kind"><c>audio</c> or <c>score</c>.</param>
/// <param name="TrackId">Only items of this track.</param>
/// <param name="Status"><c>queued</c>, <c>rendering</c>, <c>ready</c> or <c>failed</c>.</param>
/// <param name="Q">Name contains (1..100 characters).</param>
/// <param name="Limit">Page size 1..200 (default 100).</param>
/// <param name="Offset">Items to skip (&gt;= 0).</param>
public sealed record AssetListQuery(string? Kind, Guid? TrackId, string? Status, string? Q, int? Limit, int? Offset)
{
    /// <summary>Longest search text.</summary>
    public const int MaxSearchLength = 100;

    /// <summary>The repository query, or the violations.</summary>
    /// <param name="errors">Violations with the query parameter as field.</param>
    /// <returns>The query (meaningless when errors were added).</returns>
    public AssetQuery ToQuery(List<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var query = new AssetQuery
        {
            Kind = Parse<AssetKind>(Kind, "kind", errors),
            TrackId = TrackId,
            Status = Parse<AssetStatus>(Status, "status", errors),
            NameContains = Q,
            Limit = Limit ?? 100,
            Offset = Offset ?? 0,
        };
        CheckPaging(errors, query);
        return query;
    }

    private void CheckPaging(List<DomainError> errors, AssetQuery query)
    {
        Check(errors, query.Limit is >= 1 and <= ContractLimits.MaxAssetPageSize, "limit", $"Must be between 1 and {ContractLimits.MaxAssetPageSize}.");
        Check(errors, query.Offset >= 0, "offset", "Must be 0 or more.");
        Check(errors, Q is null || Q.Length is >= 1 and <= MaxSearchLength, "q", $"Must be 1 to {MaxSearchLength} characters long.");
    }

    private static T? Parse<T>(string? value, string field, List<DomainError> errors)
        where T : struct, Enum
    {
        if (value is null)
        {
            return null;
        }

        var valid = Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && !int.TryParse(value, out _);
        Check(errors, valid, field, $"'{value}' is not a valid {field}.");
        return valid ? parsed : null;
    }

    private static void Check(List<DomainError> errors, bool valid, string field, string message)
    {
        if (!valid)
        {
            errors.Add(new DomainError(field, message));
        }
    }
}
