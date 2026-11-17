namespace StemStudio.Api.Tests.Contract;

// All operation ids of feature 023 (backend 1.23.0). The expected contract operation list uses this full list; the backend-023
// lane removes ids from PendingLyricAssistOperations (Pending023) as it serves them (22+23 round decision).
public sealed partial class BackendContractTests
{
    private static readonly string[] LyricAssistOperations =
    [
        "getLyricAssistStatus", "getLyricAssistSettings", "updateLyricAssistSettings", "createLyricAssist", "listLyricAssists", "getLyricAssist", "streamLyricAssistEvents",
        "cancelLyricAssist", "listLyricSuggestions", "acceptLyricSuggestions", "rejectLyricSuggestions", "markSuggestionIntentional",
        "listIntentionalMarks", "createIntentionalMark", "deleteIntentionalMark", "updateSheetAssistSettings", "confirmOwnWork", "revokeOwnWork",
        "undoLyricSuggestions", "listLyricsRevisions", "restoreLyricsRevision", "getSheetSyllables", "countSyllables", "findRhymes",
        "listLyricWords", "addLyricWord", "deleteLyricWord", "guessLyricsLanguage",
    ];
}
