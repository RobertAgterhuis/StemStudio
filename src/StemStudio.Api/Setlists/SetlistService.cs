using StemStudio.Api.SongSections;
using StemStudio.Contracts.Setlists;
using StemStudio.Domain;
using StemStudio.Domain.Setlists;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Setlists;

/// <summary>Setlist CRUD and the entry list (014 FR-015, FR-016, data-model 3.6).</summary>
/// <param name="setlists">Setlists.</param>
/// <param name="sections">Section sets (for the section counts).</param>
/// <param name="clock">Clock.</param>
public sealed class SetlistService(ISetlistRepository setlists, ISectionRepository sections, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary><c>GET /api/setlists</c>: most recently updated first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 with the summaries.</returns>
    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        var all = await setlists.ListAsync(cancellationToken);
        var tracks = await setlists.TracksAsync(all.SelectMany(setlist => setlist.Entries.Select(entry => entry.TrackId)).Distinct().ToList(), cancellationToken);
        var durations = tracks.ToDictionary(pair => pair.Key, pair => pair.Value.DurationSeconds);
        return TypedResults.Ok(all.Select(setlist => SetlistMapper.ToSummary(setlist, durations)).ToList());
    }

    /// <summary><c>POST /api/setlists</c>.</summary>
    /// <param name="request">Name and note.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201 with the setlist or 422.</returns>
    public async Task<IResult> CreateAsync(SetlistCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var setlist = Setlist.Create(Guid.NewGuid(), request.Name, request.Note, Now);
            await setlists.AddAsync(setlist, cancellationToken);
            return TypedResults.Created(Contracts.ApiRoutes.Setlist(setlist.Id), await ToDtoAsync(setlist, cancellationToken));
        }
        catch (DomainValidationException invalid)
        {
            return PracticeFlowProblems.Invalid(invalid.Errors);
        }
    }

    /// <summary><c>GET /api/setlists/{id}</c>; <paramref name="acknowledgeRemoved"/> resets the removed-entries counter.</summary>
    /// <param name="id">Setlist id.</param>
    /// <param name="acknowledgeRemoved">The UI showed the notice.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, bool acknowledgeRemoved, CancellationToken cancellationToken)
    {
        if (await setlists.FindAsync(id, cancellationToken) is not { } setlist)
        {
            return NotFound(id);
        }

        var dto = await ToDtoAsync(setlist, cancellationToken);
        if (acknowledgeRemoved && setlist.AcknowledgeRemoved())
        {
            await setlists.SaveAsync(cancellationToken);
        }

        return TypedResults.Ok(dto);
    }

    /// <summary><c>PATCH /api/setlists/{id}</c>.</summary>
    /// <param name="id">Setlist id.</param>
    /// <param name="request">New name and/or note.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 422.</returns>
    public Task<IResult> UpdateAsync(Guid id, SetlistUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(id, setlist => setlist.Update(request.Name, request.Note, Now), cancellationToken);
    }

    /// <summary><c>PUT /api/setlists/{id}/entries</c>: replaces the ordered entries.</summary>
    /// <param name="id">Setlist id.</param>
    /// <param name="request">The entries.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 422.</returns>
    public async Task<IResult> ReplaceEntriesAsync(Guid id, SetlistEntriesUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inputs = (request.Entries ?? []).Select(entry => new SetlistEntryInput(entry.Id, entry.TrackId, entry.MixId, entry.Note)).ToList();
        var tracks = await setlists.TracksAsync(inputs.Select(input => input.TrackId).Distinct().ToList(), cancellationToken);
        var mixes = await setlists.MixesAsync(inputs.Select(input => input.MixId).OfType<Guid>().Distinct().ToList(), cancellationToken);
        var library = new SetlistEntryContext(tracks.Keys.ToHashSet(), mixes.ToDictionary(pair => pair.Key, pair => pair.Value.TrackId));
        return await ChangeAsync(id, setlist => setlist.ReplaceEntries(inputs, library, Guid.NewGuid, Now), cancellationToken);
    }

    /// <summary><c>DELETE /api/setlists/{id}</c> (tracks and mixes stay).</summary>
    /// <param name="id">Setlist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await setlists.FindAsync(id, cancellationToken) is not { } setlist)
        {
            return NotFound(id);
        }

        await setlists.RemoveAsync(setlist, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>The library facts of a setlist's entries.</summary>
    /// <param name="setlist">The setlist.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Tracks, mixes and section counts.</returns>
    public async Task<SetlistLibrary> LibraryAsync(Setlist setlist, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setlist);
        var trackIds = setlist.Entries.Select(entry => entry.TrackId).Distinct().ToList();
        return new SetlistLibrary(
            await setlists.TracksAsync(trackIds, cancellationToken),
            await setlists.MixesAsync(setlist.Entries.Select(entry => entry.MixId).OfType<Guid>().Distinct().ToList(), cancellationToken),
            await sections.CountsAsync(trackIds, cancellationToken));
    }

    /// <summary>404 of an unknown setlist.</summary>
    /// <param name="id">Setlist id.</param>
    /// <returns>The problem.</returns>
    public static IResult NotFound(Guid id) => ApiProblems.NotFound($"Setlist {id} does not exist.");

    private async Task<IResult> ChangeAsync(Guid id, Action<Setlist> change, CancellationToken cancellationToken)
    {
        if (await setlists.FindAsync(id, cancellationToken) is not { } setlist)
        {
            return NotFound(id);
        }

        try
        {
            change(setlist);
        }
        catch (DomainValidationException invalid)
        {
            return PracticeFlowProblems.Invalid(invalid.Errors);
        }

        await setlists.SaveAsync(cancellationToken);
        return TypedResults.Ok(await ToDtoAsync(setlist, cancellationToken));
    }

    private async Task<SetlistDto> ToDtoAsync(Setlist setlist, CancellationToken cancellationToken) =>
        SetlistMapper.ToDto(setlist, await LibraryAsync(setlist, cancellationToken));
}
