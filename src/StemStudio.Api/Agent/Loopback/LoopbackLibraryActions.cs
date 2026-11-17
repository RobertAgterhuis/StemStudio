using StemStudio.Agent.Ports;
using StemStudio.Contracts.Library;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>
/// Library folders, live search and library info over loopback (feature 022). Architect skeleton (022 Phase 0, T003): every
/// method throws until the backend implements it test-first against the endpoints (tasks.md T079 -> T080); the agent tools
/// (T077/T078) are tested against fake actions meanwhile.
/// </summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackLibraryActions(LoopbackClient client) : ILibraryActions
{
    /// <summary>Message of the skeleton methods.</summary>
    public const string PendingMessage = "Library loopback actions arrive with feature 022 (tasks.md T080).";

    /// <summary>The loopback client the implementation will use.</summary>
    public LoopbackClient Client { get; } = client;

    /// <inheritdoc />
    public Task<LibraryFolderViewDto> BrowseLibraryFolderAsync(LibraryFolderQuery query, CancellationToken cancellationToken) =>
        throw new NotImplementedException(PendingMessage);

    /// <inheritdoc />
    public Task<LibrarySearchResultDto> SearchLibraryAsync(LibrarySearchQuery query, CancellationToken cancellationToken) =>
        throw new NotImplementedException(PendingMessage);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> GetLibraryInfoAsync(LibraryInfoTarget target, CancellationToken cancellationToken) =>
        throw new NotImplementedException(PendingMessage);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> SetLibraryInfoAsync(LibraryInfoTarget target, LibraryInfoRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException(PendingMessage);
}
