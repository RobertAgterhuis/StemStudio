namespace StemStudio.Api.Tests.Contract;

// Pending operation ids of feature 022 in their own file (022+023 round start) so the backend-022 lane edits only this file.
public sealed partial class BackendContractTests
{
    // Feature 022 (backend 1.22.0, staged in the 022 Phase 0, T002): the backend removes each id when it serves it -
    // T043 (getLibraryRoot, getLibraryFolder), T063 (searchLibrary), T073 (the six library-info operations).
    private static readonly string[] PendingLibraryOperations =
    [
        "getLibraryRoot", "getLibraryFolder", "searchLibrary",
        "getTrackLibraryInfo", "setTrackLibraryInfo", "resetTrackLibraryInfo",
        "getAssetLibraryInfo", "setAssetLibraryInfo", "resetAssetLibraryInfo",
    ];
}
