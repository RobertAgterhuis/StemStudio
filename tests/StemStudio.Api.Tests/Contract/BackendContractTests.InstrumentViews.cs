namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Feature 016 operations staged by the architect (Phase 0, T003) and not served yet. Kept in a file of its own so the 016 backend
/// lane moves its ids without touching the files other lanes edit.
/// </summary>
public sealed partial class BackendContractTests
{
    // Feature 016 (backend 1.15.0, staged in the 016 Phase 0, T003): the backend moves each id to InstrumentViewOperations in the task
    // that serves it - T065 (getChords, detectChords, deleteChords, editChordSegment, getChordSheet, downloadChordPro),
    // T046/T065 (getInstrumentCatalogue), T087 (drum hits), T096 (vocal pitch), T117 (getKey), T124 (getCapoSuggestions).
    private static readonly string[] InstrumentViewOperations =
    [
        "getChords", "detectChords", "deleteChords", "editChordSegment", "getChordSheet", "downloadChordPro", "getInstrumentCatalogue",
        "getDrumHits", "detectDrumHits", "deleteDrumHits", "getVocalPitch", "analyseVocalPitch", "deleteVocalPitch", "getKey", "getCapoSuggestions",
    ];

    private static readonly string[] PendingInstrumentViewOperations = [.. InstrumentViewsContractTests.Operations.Except(InstrumentViewOperations)];
}
