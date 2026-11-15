using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Domain;
using StemStudio.Domain.Mixes;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Mixes;

/// <summary>Mix library: list, create, get, update, delete and duplicate (FR-016, FR-017).</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="mixes">Mix repository.</param>
/// <param name="clock">Clock.</param>
public sealed class MixService(ITrackRepository tracks, IMixRepository mixes, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Mix-library entries, newest first.</summary>
    /// <param name="trackId">Optional track filter as sent in the query string.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, or 400 for a malformed filter.</returns>
    public async Task<IResult> ListAsync(string? trackId, CancellationToken cancellationToken)
    {
        Guid? filter = null;
        if (trackId is not null)
        {
            if (!Guid.TryParse(trackId, out var parsed))
            {
                return ApiProblems.Validation("trackId", "Use the ID (GUID) of a track.");
            }

            filter = parsed;
        }

        return TypedResults.Ok((await mixes.ListAsync(filter, cancellationToken)).Select(MixMapper.ToDto).ToList());
    }

    /// <summary>One mix.</summary>
    /// <param name="id">Mix ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var mix = await mixes.FindAsync(id, cancellationToken);
        return mix is null ? NotFound(id) : TypedResults.Ok(MixMapper.ToDto(mix));
    }

    /// <summary>Creates a mix ("save" and "save as").</summary>
    /// <param name="request">The body.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201, 400, 404 (unknown track) or 409 (name taken).</returns>
    public async Task<IResult> CreateAsync(CreateMixRequest? request, CancellationToken cancellationToken)
    {
        if (request?.Settings is null)
        {
            return ApiProblems.Validation("settings", MixValidator.Required);
        }

        var (settings, errors) = MixValidator.ToDomain(request.Settings);
        if (settings is null)
        {
            return MixValidator.Problem(errors, "settings");
        }

        return await CreateForTrackAsync(request.TrackId, settings, cancellationToken);
    }

    private async Task<IResult> CreateForTrackAsync(Guid trackId, MixSettings settings, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        return track is null
            ? TrackService.NotFound(trackId)
            : await AddAsync(() => Mix.Create(Guid.NewGuid(), track, settings, Now), cancellationToken);
    }

    /// <summary>Replaces name and settings of a mix.</summary>
    /// <param name="id">Mix ID.</param>
    /// <param name="dto">The new settings.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404 or 409.</returns>
    public async Task<IResult> UpdateAsync(Guid id, MixSettingsDto? dto, CancellationToken cancellationToken)
    {
        if (dto is null)
        {
            return ApiProblems.Validation("body", MixValidator.Required);
        }

        var mix = await mixes.FindAsync(id, cancellationToken);
        if (mix is null)
        {
            return NotFound(id);
        }

        var (settings, errors) = MixValidator.ToDomain(dto);
        return settings is null ? MixValidator.Problem(errors, string.Empty) : await UpdateAsync(mix, settings, cancellationToken);
    }

    /// <summary>Deletes a mix; the track and its stems remain.</summary>
    /// <param name="id">Mix ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var mix = await mixes.FindAsync(id, cancellationToken);
        if (mix is null)
        {
            return NotFound(id);
        }

        await mixes.RemoveAsync(mix, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Copies a mix as "&lt;name&gt; (copy)", "(copy 2)", ...</summary>
    /// <param name="id">Mix ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201 or 404.</returns>
    public async Task<IResult> DuplicateAsync(Guid id, CancellationToken cancellationToken)
    {
        var mix = await mixes.FindAsync(id, cancellationToken);
        if (mix is null)
        {
            return NotFound(id);
        }

        var names = await mixes.NamesForTrackAsync(mix.TrackId, cancellationToken);
        return await AddAsync(() => mix.Duplicate(Guid.NewGuid(), names, Now), cancellationToken);
    }

    private static IResult NotFound(Guid id) => ApiProblems.NotFound($"Mix {id} does not exist.");

    private static IResult NameTaken(string name) =>
        ApiProblems.Conflict("Mix name taken", $"A mix named \"{name}\" already exists for this track.");

    private async Task<IResult> AddAsync(Func<Mix> create, CancellationToken cancellationToken)
    {
        Mix mix;
        try
        {
            mix = create();
        }
        catch (DomainValidationException invalid)
        {
            return MixValidator.Problem(invalid.Errors, "settings");
        }

        return await SaveAsync(mix, () => mixes.AddAsync(mix, cancellationToken), TypedResults.Created(ApiRoutes.Mix(mix.Id), MixMapper.ToDto(mix)));
    }

    private async Task<IResult> UpdateAsync(Mix mix, MixSettings settings, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(mix.TrackId, cancellationToken);
        try
        {
            mix.Update(track!, settings, Now);
        }
        catch (DomainValidationException invalid)
        {
            return MixValidator.Problem(invalid.Errors, string.Empty);
        }

        return await SaveAsync(mix, () => mixes.SaveAsync(cancellationToken), TypedResults.Ok(MixMapper.ToDto(mix)));
    }

    /// <summary>Saves after a name check; the unique index still catches a concurrent clash (409).</summary>
    private async Task<IResult> SaveAsync(Mix mix, Func<Task> save, IResult success)
    {
        var saved = !await mixes.IsNameTakenAsync(mix.TrackId, mix.NormalizedName, mix.Id, CancellationToken.None) && await TrySaveAsync(save);
        return saved ? success : NameTaken(mix.Name);
    }

    private static async Task<bool> TrySaveAsync(Func<Task> save)
    {
        try
        {
            await save();
            return true;
        }
        catch (DuplicateKeyException)
        {
            return false;
        }
    }
}
