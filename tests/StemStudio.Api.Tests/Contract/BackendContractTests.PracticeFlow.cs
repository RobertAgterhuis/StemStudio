namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Feature 014 operations: served ones and the ones staged by the architect (Phase 0 part 2, T003) and not served yet. Kept in a
/// file of its own so the 014 backend lane moves its ids without touching the files other lanes edit (the 015 lane has its own list).
/// </summary>
public sealed partial class BackendContractTests
{
    // Feature 014 (backend 1.13.0, staged in the 014 Phase 0 part 2, T003): the backend removes each id when it serves it -
    // T055 (getSections, detectSections), T071 (replaceSections), T079 (listTrackScores, getScoreView, createScoreView,
    // getScoreViewPage), T089 (setlist CRUD, replaceSetlistEntries), T091 (getSetlistPdf), T097 (practice log).
    // Feature 014 operations the backend serves (moved here from the pending list as each task lands).
    private static readonly string[] PracticeFlowOperations =
    [
        "getSections",
        "replaceSections",
        "detectSections",
        "listTrackScores",
        "getScoreView",
        "createScoreView",
        "getScoreViewPage",
        "listSetlists",
        "createSetlist",
        "getSetlist",
        "updateSetlist",
        "deleteSetlist",
        "replaceSetlistEntries",
        "getSetlistPdf",
        "getPracticeLogSettings",
        "updatePracticeLogSettings",
        "listPracticeSessions",
        "deletePracticeSessions",
        "upsertPracticeSession",
        "deletePracticeSession",
        "getPracticeSummary",
        "getPracticeOverview",
    ];

    // Every 014 operation is served (T055..T097); the list stays for the shared operation-id assertions.
    private static readonly string[] PendingPracticeFlowOperations = [];
}
