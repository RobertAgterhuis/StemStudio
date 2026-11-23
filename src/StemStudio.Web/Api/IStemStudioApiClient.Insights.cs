using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Api;

/// <summary>Stem quality report, lesson handouts and practice notes (feature 020, backend contract 1.19.0).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/tracks/{id}/quality</c> (<c>getQualityReport</c>; status <c>none</c> when never made).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report with tips.</returns>
    Task<QualityReportDto> GetQualityReportAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/quality</c> (<c>requestQualityReport</c>; 409 <c>track-not-ready</c>/<c>quality-running</c>, 503).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued report (the last ready stems stay readable).</returns>
    Task<QualityReportDto> RequestQualityReportAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/handouts/options</c> (<c>getHandoutOptions</c>).</summary>
    /// <param name="source">Song or setlist.</param>
    /// <param name="family">Family.</param>
    /// <param name="transposition">Transposition (<c>melodic</c> only); null = none in the query.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Ingredients with status, notation choices, transpositions, paper and estimate.</returns>
    Task<HandoutOptionsDto> GetHandoutOptionsAsync(
        HandoutSourceDto source, HandoutFamilyDto family, TranspositionDto? transposition = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/assets/handouts</c> (<c>createHandout</c>; 409 <c>track-not-ready</c>, <c>setlist-empty</c>, <c>too-many-songs</c>, 422, 503).</summary>
    /// <param name="request">Source, family and the optional choices (null = defaults).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new library item (kind <c>handout</c>, status <c>queued</c>).</returns>
    Task<AssetDto> CreateHandoutAsync(CreateHandoutRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/tracks/{id}/practice-notes</c> (<c>getPracticeNotes</c>).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>One note per family that has one.</returns>
    Task<PracticeNotesDto> GetPracticeNotesAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/tracks/{id}/practice-notes/{family}</c> (<c>setPracticeNote</c>; an empty text deletes the note, 204).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="family">Family.</param>
    /// <param name="text">The note (at most 4,000 characters).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored note, or null when the note was deleted.</returns>
    Task<PracticeNoteDto?> SetPracticeNoteAsync(Guid trackId, HandoutFamilyDto family, string text, CancellationToken cancellationToken = default);
}
