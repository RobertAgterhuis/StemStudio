using StemStudio.Agent.Ports;
using StemStudio.Contracts.Library;

namespace StemStudio.AgentEval.Fixture;

/// <summary>
/// Library folders over the fixture (feature 022). Architect skeleton (022 Phase 0, T003): every call answers 501 until the
/// ai-agent seeds the folders for the evaluation cases LIB-01..LIB-05 (tasks.md T083).
/// </summary>
internal sealed class FixtureLibraryActions : ILibraryActions
{
    /// <summary>The answer of every skeleton call.</summary>
    public static AgentActionException Pending() => new(501, "not-implemented", "Library folders are not in the evaluation fixture yet (022 T083).");

    public Task<LibraryFolderViewDto> BrowseLibraryFolderAsync(LibraryFolderQuery query, CancellationToken cancellationToken) => throw Pending();

    public Task<LibrarySearchResultDto> SearchLibraryAsync(LibrarySearchQuery query, CancellationToken cancellationToken) => throw Pending();

    public Task<LibraryInfoStateDto> GetLibraryInfoAsync(LibraryInfoTarget target, CancellationToken cancellationToken) => throw Pending();

    public Task<LibraryInfoStateDto> SetLibraryInfoAsync(LibraryInfoTarget target, LibraryInfoRequest request, CancellationToken cancellationToken) =>
        throw Pending();
}
