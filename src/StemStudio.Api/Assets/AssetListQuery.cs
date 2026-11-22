using System.Text.RegularExpressions;
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
/// <param name="Origin"><c>export</c>, <c>transcription</c>, <c>generation</c> or <c>recording</c>; without it recorded takes are excluded (1.14.0).</param>
/// <param name="Channel">With <paramref name="TrackId"/>: only items whose score source is this channel (1.15.0).</param>
public sealed partial record AssetListQuery(string? Kind, Guid? TrackId, string? Status, string? Q, int? Limit, int? Offset, string? Origin = null, string? Channel = null)
{
    /// <summary>Longest search text.</summary>
    public const int MaxSearchLength = 100;

    /// <summary>Longest channel id accepted by the filter.</summary>
    public const int MaxChannelLength = 64;

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
            Origin = Parse<AssetOrigin>(Origin, "origin", errors),
            Channel = Channel,
            NameContains = Q,
            Limit = Limit ?? 100,
            Offset = Offset ?? 0,
        };
        CheckPaging(errors, query);
        return query;
    }

    private void CheckPaging(List<DomainError> errors, AssetQuery query)
    {
        QueryParsing.Require(errors, query.Limit is >= 1 and <= ContractLimits.MaxAssetPageSize, "limit", $"Must be between 1 and {ContractLimits.MaxAssetPageSize}.");
        QueryParsing.Require(errors, query.Offset >= 0, "offset", "Must be 0 or more.");
        QueryParsing.Require(errors, Q is null || Q.Length is >= 1 and <= MaxSearchLength, "q", $"Must be 1 to {MaxSearchLength} characters long.");
        QueryParsing.Require(errors, Channel is null || (Channel.Length <= MaxChannelLength && ChannelPattern().IsMatch(Channel)), "channel", "Must be a channel id such as bass or drums.kick.");
        QueryParsing.Require(errors, Channel is null || TrackId is not null, "channel", "Give trackId with channel.");
    }

    [GeneratedRegex(@"\A[a-z]+(\.[a-z-]+)?\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ChannelPattern();

    private static T? Parse<T>(string? value, string field, List<DomainError> errors)
        where T : struct, Enum => QueryParsing.Enum<T>(value, field, errors);
}
