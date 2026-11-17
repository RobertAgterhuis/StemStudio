namespace StemStudio.Api.Tests.Contract;

// Pending operation ids of feature 023 in their own file (022+023 round start) so the backend-023 lane edits only this file.
public sealed partial class BackendContractTests
{
    // Feature 023 (backend 1.23.0, staged in the 023 Phase 0, T001/T005): the backend removes each id when it serves it -
    // T028 (status, assists, events, cancel), T030 (suggestions, marks, sheet settings, undo, revisions, words), T039 (syllables),
    // T046 (rhymes, language guess), T062 (lyric-assist settings), T075 (own-work override).
    private static readonly string[] PendingLyricAssistOperations =
    [];
}
