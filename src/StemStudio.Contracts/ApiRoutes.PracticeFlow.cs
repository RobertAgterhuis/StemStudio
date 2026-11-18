using System.Globalization;
using StemStudio.Contracts.Setlists;

namespace StemStudio.Contracts;

/// <summary>Routes of the practice flow (backend-api 1.13.0, feature 014, tags <c>sections</c>, <c>score-follow</c>, <c>setlists</c>, <c>practice</c>).</summary>
public static partial class ApiRoutes
{
    /// <summary>GET (list) / POST (create) setlists.</summary>
    public const string Setlists = "/api/setlists";

    /// <summary>GET / PUT the practice log switch (<c>getPracticeLogSettings</c>, <c>updatePracticeLogSettings</c>).</summary>
    public const string PracticeLogSettings = "/api/practice/settings";

    /// <summary>GET (list, query <c>trackId</c>, <c>before</c>, <c>limit</c>) / DELETE (query <c>trackId</c>; absent = all) sessions.</summary>
    public const string PracticeSessions = "/api/practice/sessions";

    /// <summary>GET: the practice summary of a track (use <see cref="PracticeSummaryFor"/>).</summary>
    public const string PracticeSummary = "/api/practice/summary";

    /// <summary>GET: practice totals per track (use <see cref="PracticeOverviewFor"/>).</summary>
    public const string PracticeOverview = "/api/practice/overview";

    /// <summary>GET / PUT the sections of a track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string Sections(Guid trackId) => $"{Track(trackId)}/sections";

    /// <summary>POST: detect the sections of a track (again).</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string SectionsDetect(Guid trackId) => $"{Sections(trackId)}/detect";

    /// <summary>GET: the ready scores of a track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL.</returns>
    public static string TrackScores(Guid trackId) => $"{Track(trackId)}/scores";

    /// <summary>GET / POST the score view of a score asset.</summary>
    /// <param name="assetId">Score asset id.</param>
    /// <returns>The relative URL.</returns>
    public static string ScoreView(Guid assetId) => $"/api/assets/{assetId}/score-view";

    /// <summary>GET: one SVG page of a score view (the <c>url</c> of a page adds <c>?v=</c>).</summary>
    /// <param name="assetId">Score asset id.</param>
    /// <param name="page">1-based page number.</param>
    /// <returns>The relative URL.</returns>
    public static string ScoreViewPage(Guid assetId, int page) =>
        string.Create(CultureInfo.InvariantCulture, $"{ScoreView(assetId)}/pages/{page}");

    /// <summary>GET / PATCH / DELETE one setlist.</summary>
    /// <param name="setlistId">Setlist id.</param>
    /// <returns>The relative URL.</returns>
    public static string Setlist(Guid setlistId) => $"{Setlists}/{setlistId}";

    /// <summary>GET one setlist and reset its removed-entries counter.</summary>
    /// <param name="setlistId">Setlist id.</param>
    /// <returns>The relative URL with <c>acknowledgeRemoved=true</c>.</returns>
    public static string SetlistAcknowledgingRemoved(Guid setlistId) => $"{Setlist(setlistId)}?acknowledgeRemoved=true";

    /// <summary>PUT: replace the ordered entries of a setlist.</summary>
    /// <param name="setlistId">Setlist id.</param>
    /// <returns>The relative URL.</returns>
    public static string SetlistEntries(Guid setlistId) => $"{Setlist(setlistId)}/entries";

    /// <summary>GET: the setlist PDF; every option is sent.</summary>
    /// <param name="setlistId">Setlist id.</param>
    /// <param name="options">Parts to include.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string SetlistPdf(Guid setlistId, SetlistPdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"{Setlist(setlistId)}/pdf?lyrics={Bool(options.Lyrics)}&sections={Bool(options.Sections)}"
            + $"&notes={Bool(options.Notes)}&pageBreakPerSong={Bool(options.PageBreakPerSong)}";
    }

    /// <summary>PUT (cumulative upsert) / DELETE one practice session.</summary>
    /// <param name="sessionId">Session id (made by the browser).</param>
    /// <returns>The relative URL.</returns>
    public static string PracticeSession(Guid sessionId) => $"{PracticeSessions}/{sessionId}";

    /// <summary>DELETE: the sessions of one track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string PracticeSessionsOf(Guid trackId) => $"{PracticeSessions}?trackId={trackId}";

    /// <summary>GET: the practice summary of a track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="days">Window in local days, 1..365.</param>
    /// <param name="utcOffsetMinutes">Browser offset from UTC, -840..840.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string PracticeSummaryFor(Guid trackId, int days, int utcOffsetMinutes) =>
        string.Create(CultureInfo.InvariantCulture, $"{PracticeSummary}?trackId={trackId}&days={days}&utcOffsetMinutes={utcOffsetMinutes}");

    /// <summary>GET: practice totals per track.</summary>
    /// <param name="utcOffsetMinutes">Browser offset from UTC, -840..840.</param>
    /// <returns>The relative URL with the query.</returns>
    public static string PracticeOverviewFor(int utcOffsetMinutes) =>
        string.Create(CultureInfo.InvariantCulture, $"{PracticeOverview}?utcOffsetMinutes={utcOffsetMinutes}");

    private static string Bool(bool value) => value ? "true" : "false";
}
