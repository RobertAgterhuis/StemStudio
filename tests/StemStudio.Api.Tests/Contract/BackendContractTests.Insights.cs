namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Feature 020 operations staged by the architect (Phase 0, T002) and not served yet. Kept in a file of its own so the 020 backend
/// lane moves its ids without touching the files other lanes edit.
/// </summary>
public sealed partial class BackendContractTests
{
    // Feature 020 (backend 1.19.0, staged in the 020 Phase 0, T002): the backend moves each id to InsightsOperations in the task that
    // serves it - T045 (getQualityReport, requestQualityReport), T065 (getHandoutOptions, createHandout), T067 (practice notes).
    private static readonly string[] InsightsOperations = ["getQualityReport", "requestQualityReport", "getHandoutOptions", "createHandout", "getPracticeNotes", "setPracticeNote"];

    private static readonly string[] PendingInsightsOperations = [.. InsightsContractTests.Operations.Except(InsightsOperations)];
}
