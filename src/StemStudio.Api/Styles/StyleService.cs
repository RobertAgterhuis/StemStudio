using StemStudio.Domain;
using StemStudio.Domain.Styles;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Styles;

/// <summary>The style library read side (009 FR-003, FR-004).</summary>
/// <param name="styles">Style repository.</param>
public sealed class StyleService(IStyleRepository styles)
{
    /// <summary>Matching styles, presets first, then by name.</summary>
    /// <param name="query">The filters.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 400.</returns>
    public async Task<IResult> ListAsync(StyleListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var errors = new List<DomainError>();
        var filter = query.ToFilter(errors);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors, string.Empty);
        }

        var all = await styles.ListAsync(cancellationToken);
        return TypedResults.Ok(StyleFilter.Order(all.Where(filter.Matches)).Select(StyleMapper.ToDto).ToList());
    }

    /// <summary>One style.</summary>
    /// <param name="id">Style id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await styles.FindAsync(id, cancellationToken) is { } style ? TypedResults.Ok(StyleMapper.ToDto(style)) : NotFound(id);

    /// <summary>404 for an unknown style.</summary>
    /// <param name="id">Style id.</param>
    /// <returns>The problem.</returns>
    public static IResult NotFound(Guid id) => ApiProblems.NotFound($"Style {id} does not exist.");
}
