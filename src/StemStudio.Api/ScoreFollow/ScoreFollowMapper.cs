using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Practice;

namespace StemStudio.Api.ScoreFollow;

/// <summary>Maps score views to the backend 1.13.0 contract and worker bar boxes to the domain.</summary>
public static class ScoreFollowMapper
{
    /// <summary>Page width the worker renders (data-model 2.5).</summary>
    public const int PageWidthPx = 840;

    /// <summary>Page height the worker renders (data-model 2.5).</summary>
    public const int PageHeightPx = 1188;

    /// <summary>The DTO of a view; a missing view has status <c>none</c>.</summary>
    /// <param name="assetId">Score item.</param>
    /// <param name="view">The view, or null.</param>
    /// <param name="plan">The plan of the score (for track and offset of a missing view), or null.</param>
    /// <returns>The DTO.</returns>
    public static ScoreViewDto ToDto(Guid assetId, ScoreView? view, ScoreViewPlan? plan) => view is null
        ? new ScoreViewDto { AssetId = assetId, Status = ScoreViewStatusDto.None, TrackId = plan?.TrackId, OffsetSeconds = plan?.OffsetSeconds ?? 0, Pages = [], Bars = [], Error = null }
        : new ScoreViewDto
        {
            AssetId = assetId,
            Status = StatusOf(view),
            TrackId = view.TrackId,
            OffsetSeconds = view.OffsetSeconds,
            Pages = view.Status == ScoreViewStatus.Ready ? Pages(view) : [],
            Bars = view.Status == ScoreViewStatus.Ready ? view.Bars.Select(ToDto).ToList() : [],
            Error = view.Error,
        };

    /// <summary>The contract status of a view (none when missing).</summary>
    /// <param name="view">The view, or null.</param>
    /// <returns>The status.</returns>
    public static ScoreViewStatusDto StatusOf(ScoreView? view) => view is null ? ScoreViewStatusDto.None : (ScoreViewStatusDto)((int)view.Status + 1);

    /// <summary>The contract profile of a score item.</summary>
    /// <param name="profile">Domain profile.</param>
    /// <returns>The DTO value.</returns>
    public static ScoreProfileDto ProfileOf(Domain.Scores.ScoreProfile profile) => Scores.ScoreMapper.Map<ScoreProfileDto>(profile);

    private static List<ScoreViewPageDto> Pages(ScoreView view) => Enumerable.Range(1, view.PageCount).Select(page => new ScoreViewPageDto
    {
        Page = page,
        Url = string.Create(CultureInfo.InvariantCulture, $"{ApiRoutes.ScoreViewPage(view.AssetId, page)}?v={view.Revision}"),
        WidthPx = PageWidthPx,
        HeightPx = PageHeightPx,
    }).ToList();

    private static ScoreBarDto ToDto(ScoreBar bar) => new()
    {
        Index = bar.Index,
        Number = bar.Number,
        Page = bar.Page,
        X = bar.X,
        Y = bar.Y,
        Width = bar.Width,
        Height = bar.Height,
        StartSeconds = bar.StartSeconds,
        EndSeconds = bar.EndSeconds,
    };
}
