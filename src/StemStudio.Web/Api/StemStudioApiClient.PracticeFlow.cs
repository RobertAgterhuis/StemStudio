using StemStudio.Contracts;
using StemStudio.Contracts.Practice;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Setlists;

namespace StemStudio.Web.Api;

/// <summary>Sections, score follow, setlists and the practice log (feature 014, backend contract 1.13.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<SectionSetDto> GetSectionsAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<SectionSetDto>(HttpMethod.Get, ApiRoutes.Sections(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<SectionSetDto> ReplaceSectionsAsync(Guid trackId, SectionsUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SectionSetDto>(HttpMethod.Put, ApiRoutes.Sections(trackId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<SectionSetDto> DetectSectionsAsync(Guid trackId, bool overwrite, CancellationToken cancellationToken = default) =>
        SendAsync<SectionSetDto>(HttpMethod.Post, ApiRoutes.SectionsDetect(trackId), JsonBody(new DetectSectionsRequest { Overwrite = overwrite }), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TrackScoreDto>> ListTrackScoresAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<TrackScoreDto>>(HttpMethod.Get, ApiRoutes.TrackScores(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<ScoreViewDto> GetScoreViewAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        SendAsync<ScoreViewDto>(HttpMethod.Get, ApiRoutes.ScoreView(assetId), null, cancellationToken);

    /// <inheritdoc />
    public Task<ScoreViewDto> CreateScoreViewAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        SendAsync<ScoreViewDto>(HttpMethod.Post, ApiRoutes.ScoreView(assetId), null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SetlistSummaryDto>> ListSetlistsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<SetlistSummaryDto>>(HttpMethod.Get, ApiRoutes.Setlists, null, cancellationToken);

    /// <inheritdoc />
    public Task<SetlistDto> CreateSetlistAsync(SetlistCreateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SetlistDto>(HttpMethod.Post, ApiRoutes.Setlists, JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<SetlistDto> GetSetlistAsync(Guid setlistId, bool acknowledgeRemoved = false, CancellationToken cancellationToken = default) =>
        SendAsync<SetlistDto>(
            HttpMethod.Get, acknowledgeRemoved ? ApiRoutes.SetlistAcknowledgingRemoved(setlistId) : ApiRoutes.Setlist(setlistId), null, cancellationToken);

    /// <inheritdoc />
    public Task<SetlistDto> UpdateSetlistAsync(Guid setlistId, SetlistUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SetlistDto>(HttpMethod.Patch, ApiRoutes.Setlist(setlistId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteSetlistAsync(Guid setlistId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.Setlist(setlistId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SetlistDto> ReplaceSetlistEntriesAsync(Guid setlistId, SetlistEntriesUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SetlistDto>(HttpMethod.Put, ApiRoutes.SetlistEntries(setlistId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<PracticeLogSettingsDto> GetPracticeLogSettingsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PracticeLogSettingsDto>(HttpMethod.Get, ApiRoutes.PracticeLogSettings, null, cancellationToken);

    /// <inheritdoc />
    public Task<PracticeLogSettingsDto> UpdatePracticeLogSettingsAsync(PracticeLogSettingsUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<PracticeLogSettingsDto>(HttpMethod.Put, ApiRoutes.PracticeLogSettings, JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<PracticeSessionDto> UpsertPracticeSessionAsync(Guid sessionId, PracticeSessionInputDto session, CancellationToken cancellationToken = default) =>
        SendAsync<PracticeSessionDto>(HttpMethod.Put, ApiRoutes.PracticeSession(sessionId), JsonBody(session), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PracticeSessionDto>> ListPracticeSessionsAsync(Guid? trackId, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<PracticeSessionDto>>(HttpMethod.Get, SessionsUri(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<DeletedCountDto> DeletePracticeSessionsAsync(Guid? trackId, CancellationToken cancellationToken = default) =>
        SendAsync<DeletedCountDto>(HttpMethod.Delete, SessionsUri(trackId), null, cancellationToken);

    /// <inheritdoc />
    public async Task DeletePracticeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.PracticeSession(sessionId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PracticeSummaryDto> GetPracticeSummaryAsync(Guid trackId, int days, int utcOffsetMinutes, CancellationToken cancellationToken = default) =>
        SendAsync<PracticeSummaryDto>(HttpMethod.Get, ApiRoutes.PracticeSummaryFor(trackId, days, utcOffsetMinutes), null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PracticeTrackTotalDto>> GetPracticeOverviewAsync(int utcOffsetMinutes, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<PracticeTrackTotalDto>>(HttpMethod.Get, ApiRoutes.PracticeOverviewFor(utcOffsetMinutes), null, cancellationToken);

    private static string SessionsUri(Guid? trackId) => trackId is { } id ? ApiRoutes.PracticeSessionsOf(id) : ApiRoutes.PracticeSessions;
}
