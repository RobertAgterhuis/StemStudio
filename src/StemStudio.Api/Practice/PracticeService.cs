using StemStudio.Api.SongSections;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Practice;
using StemStudio.Domain;
using StemStudio.Domain.Practice;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Practice;

/// <summary>
/// The local practice log (014 FR-025..FR-028, ADR-023 D6): settings, session upserts, lists, summary, overview and
/// deletes. Nothing here calls another host, and no practice value is logged.
/// </summary>
/// <param name="practice">Practice log storage.</param>
/// <param name="tracks">Tracks.</param>
/// <param name="setlists">Track and mix look-ups.</param>
/// <param name="sections">Section sets (current labels).</param>
/// <param name="clock">Clock.</param>
/// <param name="gate">One upsert at a time (the browser may save the same new session twice at once: timer and stop).</param>
public sealed class PracticeService(
    IPracticeRepository practice, ITrackRepository tracks, ISetlistRepository setlists, ISectionRepository sections, TimeProvider clock, PracticeUpsertGate gate)
{
    /// <summary>Default and largest page of <c>listPracticeSessions</c>.</summary>
    public const int DefaultLimit = 50;

    private const int MaxLimit = 200;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary><c>GET /api/practice/settings</c>.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200.</returns>
    public async Task<IResult> GetSettingsAsync(CancellationToken cancellationToken) =>
        TypedResults.Ok(PracticeMapper.ToDto(await practice.GetSettingsAsync(cancellationToken), await practice.CountSessionsAsync(cancellationToken)));

    /// <summary><c>PUT /api/practice/settings</c>.</summary>
    /// <param name="request">The switch and/or the notice.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 422 when nothing is given.</returns>
    public async Task<IResult> UpdateSettingsAsync(PracticeLogSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request is { Enabled: null, NoticeAcknowledged: null })
        {
            return Invalid("enabled", "Give enabled and/or noticeAcknowledged.");
        }

        var settings = await practice.GetSettingsAsync(cancellationToken);
        settings.Update(request.Enabled, request.NoticeAcknowledged, Now);
        await practice.SaveAsync(cancellationToken);
        return TypedResults.Ok(PracticeMapper.ToDto(settings, await practice.CountSessionsAsync(cancellationToken)));
    }

    /// <summary><c>PUT /api/practice/sessions/{id}</c>: creates or replaces a session with cumulative values.</summary>
    /// <param name="sessionId">Id made by the browser.</param>
    /// <param name="request">The values.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 unknown track, 409 <c>practice-log-off</c> or 422.</returns>
    public async Task<IResult> UpsertAsync(Guid sessionId, PracticeSessionInputDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!(await practice.GetSettingsAsync(cancellationToken)).Enabled)
        {
            return PracticeFlowProblems.Conflict(PracticeFlowProblemTypes.PracticeLogOff, "The practice log is off", "Nothing was stored; switch the log on to record practice.");
        }

        if (await tracks.FindAsync(request.TrackId, cancellationToken) is null)
        {
            return TrackService.NotFound(request.TrackId);
        }

        return await MixProblemAsync(request, cancellationToken) ?? await StoreAsync(sessionId, PracticeMapper.ToInput(request), cancellationToken);
    }

    /// <summary><c>GET /api/practice/sessions</c>: newest first.</summary>
    /// <param name="trackId">Only this track, or all.</param>
    /// <param name="before">Only sessions that started before.</param>
    /// <param name="limit">Page size 1..200 (default 50).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 422.</returns>
    public async Task<IResult> ListAsync(Guid? trackId, DateTimeOffset? before, int? limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > MaxLimit)
        {
            return Invalid("limit", $"Must be between 1 and {MaxLimit}.");
        }

        var page = await practice.ListSessionsAsync(trackId, before?.UtcDateTime, limit ?? DefaultLimit, cancellationToken);
        return TypedResults.Ok(page.Select(PracticeMapper.ToDto).ToList());
    }

    /// <summary><c>DELETE /api/practice/sessions</c>: the log of one track, or all of it.</summary>
    /// <param name="trackId">The track, or null for all.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 with the count.</returns>
    public async Task<IResult> DeleteManyAsync(Guid? trackId, CancellationToken cancellationToken) =>
        TypedResults.Ok(new DeletedCountDto { Deleted = await practice.DeleteSessionsAsync(trackId, cancellationToken) });

    /// <summary><c>DELETE /api/practice/sessions/{id}</c>.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await practice.FindSessionAsync(sessionId, cancellationToken) is not { } session)
        {
            return ApiProblems.NotFound($"Practice session {sessionId} does not exist.");
        }

        await practice.RemoveSessionAsync(session, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary><c>GET /api/practice/summary</c>.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="days">Local days 1..365 (default 30).</param>
    /// <param name="utcOffsetMinutes">-840..840 (default 0).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 422.</returns>
    public async Task<IResult> SummaryAsync(Guid? trackId, int? days, int? utcOffsetMinutes, CancellationToken cancellationToken)
    {
        if ((WindowProblem(days, utcOffsetMinutes) ?? (trackId is null ? Invalid("trackId", "The track is required.") : null)) is { } problem)
        {
            return problem;
        }

        var id = trackId!.Value;
        if (await tracks.FindAsync(id, cancellationToken) is null)
        {
            return TrackService.NotFound(id);
        }

        var labels = (await sections.FindAsync(id, cancellationToken))?.Sections.ToDictionary(section => section.Id, section => section.Label) ?? [];
        var summary = PracticeSummaryCalculator.Summarize(await practice.SessionsOfTrackAsync(id, cancellationToken), Window(days, utcOffsetMinutes), labels);
        return TypedResults.Ok(PracticeMapper.ToDto(id, summary));
    }

    /// <summary><c>GET /api/practice/overview</c>: per track, most recently practised first.</summary>
    /// <param name="utcOffsetMinutes">-840..840 (default 0).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 422.</returns>
    public async Task<IResult> OverviewAsync(int? utcOffsetMinutes, CancellationToken cancellationToken)
    {
        if (WindowProblem(null, utcOffsetMinutes) is { } problem)
        {
            return problem;
        }

        var totals = PracticeSummaryCalculator.Overview(await practice.AllSessionsAsync(cancellationToken), Window(null, utcOffsetMinutes));
        var known = await setlists.TracksAsync(totals.Select(total => total.TrackId).ToList(), cancellationToken);
        return TypedResults.Ok(totals.Where(total => known.ContainsKey(total.TrackId)).Select(total => PracticeMapper.ToDto(total, known[total.TrackId])).ToList());
    }

    private async Task<IResult> StoreAsync(Guid sessionId, PracticeSessionInput input, CancellationToken cancellationToken)
    {
        await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            var stored = await practice.FindSessionAsync(sessionId, cancellationToken);
            if (stored is null)
            {
                stored = PracticeSession.Create(sessionId, input, Now);
                await practice.AddSessionAsync(stored, cancellationToken);
            }
            else
            {
                stored.Replace(input, Now);
                await practice.SaveAsync(cancellationToken);
            }

            return TypedResults.Ok(PracticeMapper.ToDto(stored));
        }
        catch (DomainValidationException invalid)
        {
            return PracticeFlowProblems.Invalid(invalid.Errors);
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    private async Task<IResult?> MixProblemAsync(PracticeSessionInputDto request, CancellationToken cancellationToken)
    {
        if (request.MixId is not { } mixId)
        {
            return null;
        }

        var mixes = await setlists.MixesAsync([mixId], cancellationToken);
        return mixes.TryGetValue(mixId, out var mix) && mix.TrackId == request.TrackId ? null : Invalid("mixId", "The mix does not belong to this track.");
    }

    private SummaryWindow Window(int? days, int? utcOffsetMinutes) =>
        new(Now, days ?? ContractLimits.DefaultSummaryDays, utcOffsetMinutes ?? 0);

    private static IResult? WindowProblem(int? days, int? utcOffsetMinutes) => (days, utcOffsetMinutes) switch
    {
        ( < ContractLimits.MinSummaryDays or > ContractLimits.MaxSummaryDays, _) => Invalid("days", $"Must be between {ContractLimits.MinSummaryDays} and {ContractLimits.MaxSummaryDays}."),
        (_, < ContractLimits.MinUtcOffsetMinutes or > ContractLimits.MaxUtcOffsetMinutes) => Invalid("utcOffsetMinutes", $"Must be between {ContractLimits.MinUtcOffsetMinutes} and {ContractLimits.MaxUtcOffsetMinutes}."),
        _ => null,
    };

    private static IResult Invalid(string field, string message) =>
        PracticeFlowProblems.Invalid([new DomainError(field, message)]);
}

/// <summary>Serialises session upserts (find + insert) so a session id is inserted once.</summary>
public sealed class PracticeUpsertGate : IDisposable
{
    /// <summary>The lock.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => Lock.Dispose();
}
