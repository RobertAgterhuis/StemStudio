using StemStudio.Contracts;
using StemStudio.Contracts.Styles;
using StemStudio.Domain;
using StemStudio.Domain.Styles;
using StemStudio.Persistence.Repositories;
using StyleFields = StemStudio.Domain.Styles.StyleFields;

namespace StemStudio.Api.Styles;

/// <summary>Creates, updates, deletes and duplicates user styles (009 US3, FR-004, FR-005); presets are read-only.</summary>
/// <param name="styles">Style repository.</param>
/// <param name="guard">The imitation guard with the library artists.</param>
/// <param name="clock">Clock.</param>
public sealed class StyleEditingService(IStyleRepository styles, LibraryStyleGuard guard, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Creates a user style.</summary>
    /// <param name="input">The editor input.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201, 400 or 409.</returns>
    public async Task<IResult> CreateAsync(StyleInput? input, CancellationToken cancellationToken)
    {
        var (fields, problem) = await ValidAsync(input, null, cancellationToken);
        return problem ?? await AddAsync(Style.CreateUser(Guid.NewGuid(), fields!, Now), cancellationToken);
    }

    /// <summary>Replaces a user style; generations keep their snapshot.</summary>
    /// <param name="id">Style id.</param>
    /// <param name="input">The editor input.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 403, 404 or 409.</returns>
    public async Task<IResult> UpdateAsync(Guid id, StyleInput? input, CancellationToken cancellationToken)
    {
        var style = await styles.FindAsync(id, cancellationToken);
        if (UserStyleProblem(style, id) is { } refused)
        {
            return refused;
        }

        var (fields, problem) = await ValidAsync(input, id, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        style!.Update(fields!, Now);
        return await SavedAsync(() => styles.SaveAsync(cancellationToken), style, TypedResults.Ok(StyleMapper.ToDto(style)));
    }

    /// <summary>Deletes a user style.</summary>
    /// <param name="id">Style id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204, 403 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var style = await styles.FindAsync(id, cancellationToken);
        if (UserStyleProblem(style, id) is { } refused)
        {
            return refused;
        }

        await styles.RemoveAsync(style!, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Copies a preset or user style into a new user style "&lt;name&gt; (copy)".</summary>
    /// <param name="id">Style id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201 or 404.</returns>
    public async Task<IResult> DuplicateAsync(Guid id, CancellationToken cancellationToken)
    {
        var style = await styles.FindAsync(id, cancellationToken);
        if (style is null)
        {
            return StyleService.NotFound(id);
        }

        var name = StyleNaming.CopyName(style.Name, await styles.NamesAsync(cancellationToken));
        return await AddAsync(Style.CreateUser(Guid.NewGuid(), style.Fields with { Name = name }, Now), cancellationToken);
    }

    private static IResult? UserStyleProblem(Style? style, Guid id) => style switch
    {
        null => StyleService.NotFound(id),
        { IsPreset: true } => StyleProblems.PresetReadOnly(),
        _ => null,
    };

    private async Task<(StyleFields? Fields, IResult? Problem)> ValidAsync(StyleInput? input, Guid? exclude, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return (null, ApiProblems.Validation("name", "The name must be 1 to 60 characters."));
        }

        var fields = StyleMapper.ToFields(input).Normalized();
        var problem = await ProblemAsync(fields, exclude, cancellationToken);
        return (problem is null ? fields : null, problem);
    }

    private async Task<IResult?> ProblemAsync(StyleFields fields, Guid? exclude, CancellationToken cancellationToken)
    {
        var limits = StyleRules.Validate(fields);
        if (limits.Count > 0)
        {
            return ApiProblems.Validation(limits, string.Empty);
        }

        var hits = await guard.CheckAsync(fields, cancellationToken);
        if (hits.Count > 0)
        {
            return StyleProblems.Imitation(hits);
        }

        return await styles.IsNameTakenAsync(StyleNaming.Normalize(fields.Name), exclude, cancellationToken) ? StyleProblems.NameTaken(fields.Name) : null;
    }

    private Task<IResult> AddAsync(Style style, CancellationToken cancellationToken) =>
        SavedAsync(() => styles.AddAsync(style, cancellationToken), style, TypedResults.Created(ApiRoutes.Style(style.Id), StyleMapper.ToDto(style)));

    private static async Task<IResult> SavedAsync(Func<Task> save, Style style, IResult success)
    {
        try
        {
            await save();
            return success;
        }
        catch (DuplicateKeyException)
        {
            return StyleProblems.NameTaken(style.Name);
        }
    }
}

/// <summary>The imitation guard with the artists of the user's own tracks (ADR-018 D8).</summary>
/// <param name="tracks">Track repository.</param>
public sealed class LibraryStyleGuard(ITrackRepository tracks)
{
    /// <summary>Guard hits of a style.</summary>
    /// <param name="fields">The style fields.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>One error per offending field.</returns>
    public async Task<IReadOnlyList<DomainError>> CheckAsync(StyleFields fields, CancellationToken cancellationToken) =>
        (await GuardAsync(cancellationToken)).Check(fields);

    /// <summary>The guard with the current library artists.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The guard.</returns>
    public async Task<StyleTextGuard> GuardAsync(CancellationToken cancellationToken)
    {
        var all = await tracks.ListAsync(cancellationToken);
        return StyleTextGuard.Create(all.SelectMany(track => new[] { track.Artist, track.Metadata.ArtistName }));
    }
}

/// <summary>Problem responses of the style endpoints (backend 1.8.0).</summary>
public static class StyleProblems
{
    /// <summary>400 <c>imitation</c> with the offending fields.</summary>
    /// <param name="hits">The guard errors.</param>
    /// <returns>The problem.</returns>
    public static IResult Imitation(IReadOnlyList<DomainError> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        var errors = hits.GroupBy(hit => hit.Field, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(hit => hit.Message).ToArray(), StringComparer.Ordinal);
        return TypedResults.ValidationProblem(errors, title: "Styles cannot imitate an artist.", type: "imitation");
    }

    /// <summary>409 <c>style-name-taken</c>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The problem.</returns>
    public static IResult NameTaken(string name) =>
        TypedResults.Problem($"A style named \"{name}\" already exists.", statusCode: StatusCodes.Status409Conflict, title: "Style name taken", type: "style-name-taken");

    /// <summary>403 <c>preset-read-only</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult PresetReadOnly() => TypedResults.Problem(
        "Presets cannot be changed. Duplicate the preset to make your own version.", statusCode: StatusCodes.Status403Forbidden, title: "Preset is read-only", type: "preset-read-only");
}
