using StemStudio.Contracts.Practice;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Setlists;

namespace StemStudio.Web.Api;

/// <summary>Sections, score follow, setlists and the practice log (feature 014, backend contract 1.13.0).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/tracks/{id}/sections</c> (status <c>none</c> when never detected).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The section set.</returns>
    Task<SectionSetDto> GetSectionsAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/tracks/{id}/sections</c>: replaces the whole list (409 <c>sections-changed</c>, 422 throw).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="request">Revision and sections.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored set.</returns>
    Task<SectionSetDto> ReplaceSectionsAsync(Guid trackId, SectionsUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/sections/detect</c>: (re-)detection (409 <c>sections-edited</c> without overwrite).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="overwrite">Replace edited sections.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued set.</returns>
    Task<SectionSetDto> DetectSectionsAsync(Guid trackId, bool overwrite, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/scores</c>: the sheet-music items of a track.</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The scores.</returns>
    Task<IReadOnlyList<TrackScoreDto>> ListTrackScoresAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/assets/{id}/score-view</c>.</summary>
    /// <param name="assetId">Score asset.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The score view.</returns>
    Task<ScoreViewDto> GetScoreViewAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/assets/{id}/score-view</c>: starts the view job (older items).</summary>
    /// <param name="assetId">Score asset.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued view.</returns>
    Task<ScoreViewDto> CreateScoreViewAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/setlists</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The setlists.</returns>
    Task<IReadOnlyList<SetlistSummaryDto>> ListSetlistsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/setlists</c>.</summary>
    /// <param name="request">Name and note.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new setlist.</returns>
    Task<SetlistDto> CreateSetlistAsync(SetlistCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/setlists/{id}</c> (optionally acknowledging the removed-entries note).</summary>
    /// <param name="setlistId">Setlist.</param>
    /// <param name="acknowledgeRemoved">Clears the removed-entries count.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The setlist.</returns>
    Task<SetlistDto> GetSetlistAsync(Guid setlistId, bool acknowledgeRemoved = false, CancellationToken cancellationToken = default);

    /// <summary><c>PATCH /api/setlists/{id}</c>: rename or annotate.</summary>
    /// <param name="setlistId">Setlist.</param>
    /// <param name="request">Changed fields.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The setlist.</returns>
    Task<SetlistDto> UpdateSetlistAsync(Guid setlistId, SetlistUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/setlists/{id}</c>.</summary>
    /// <param name="setlistId">Setlist.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when deleted.</returns>
    Task DeleteSetlistAsync(Guid setlistId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/setlists/{id}/entries</c>: replaces the ordered entries.</summary>
    /// <param name="setlistId">Setlist.</param>
    /// <param name="request">The entries.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The setlist.</returns>
    Task<SetlistDto> ReplaceSetlistEntriesAsync(Guid setlistId, SetlistEntriesUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/practice/settings</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The practice log switch.</returns>
    Task<PracticeLogSettingsDto> GetPracticeLogSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/practice/settings</c>.</summary>
    /// <param name="request">Changed fields.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The settings.</returns>
    Task<PracticeLogSettingsDto> UpdatePracticeLogSettingsAsync(PracticeLogSettingsUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/practice/sessions/{id}</c>: cumulative upsert (409 <c>practice-log-off</c> throws).</summary>
    /// <param name="sessionId">Session id made by the browser.</param>
    /// <param name="session">Cumulative values.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored session.</returns>
    Task<PracticeSessionDto> UpsertPracticeSessionAsync(Guid sessionId, PracticeSessionInputDto session, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/practice/sessions?trackId=</c>.</summary>
    /// <param name="trackId">Track, or null for every track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The sessions, newest first.</returns>
    Task<IReadOnlyList<PracticeSessionDto>> ListPracticeSessionsAsync(Guid? trackId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/practice/sessions?trackId=</c>: the log of a track, or everything.</summary>
    /// <param name="trackId">Track, or null for every track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of deleted sessions.</returns>
    Task<DeletedCountDto> DeletePracticeSessionsAsync(Guid? trackId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/practice/sessions/{id}</c>.</summary>
    /// <param name="sessionId">Session.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when deleted.</returns>
    Task DeletePracticeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/practice/summary</c>.</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="days">Window in days (1..365).</param>
    /// <param name="utcOffsetMinutes">Local offset of the browser.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The summary.</returns>
    Task<PracticeSummaryDto> GetPracticeSummaryAsync(Guid trackId, int days, int utcOffsetMinutes, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/practice/overview</c>: totals per track.</summary>
    /// <param name="utcOffsetMinutes">Local offset of the browser.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The totals, last practised first.</returns>
    Task<IReadOnlyList<PracticeTrackTotalDto>> GetPracticeOverviewAsync(int utcOffsetMinutes, CancellationToken cancellationToken = default);
}
