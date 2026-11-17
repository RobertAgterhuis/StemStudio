using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>
/// Library folders, live search and library info over loopback (feature 022, T080): the public endpoints of backend-api
/// 1.22.0 (tag <c>library</c>). Folder ids from the model are escaped before they become a path segment; validation and
/// problems stay with the endpoints (they become readable <see cref="AgentActionException"/>s).
/// </summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackLibraryActions(LoopbackClient client) : ILibraryActions
{
    /// <summary>The loopback client.</summary>
    public LoopbackClient Client { get; } = client;

    /// <inheritdoc />
    public Task<LibraryFolderViewDto> BrowseLibraryFolderAsync(LibraryFolderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var folder = query.FolderId is { } id ? ApiRoutes.LibraryFolder(Uri.EscapeDataString(id)) : ApiRoutes.LibraryFolders;
        var path = LoopbackQuery.Build(folder, ("sort", LoopbackQuery.Name(query.Sort)), ("kind", LoopbackQuery.Name(query.Kind)), ("limit", LoopbackQuery.Number(query.Limit)));
        return Client.GetAsync<LibraryFolderViewDto>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LibrarySearchResultDto> SearchLibraryAsync(LibrarySearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = LoopbackQuery.Build(ApiRoutes.LibrarySearch, ("q", query.Query), ("within", query.Within), ("limit", LoopbackQuery.Number(query.Limit)));
        return Client.GetAsync<LibrarySearchResultDto>(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> GetLibraryInfoAsync(LibraryInfoTarget target, CancellationToken cancellationToken) =>
        Client.GetAsync<LibraryInfoStateDto>(PathOf(target), cancellationToken);

    /// <inheritdoc />
    public Task<LibraryInfoStateDto> SetLibraryInfoAsync(LibraryInfoTarget target, LibraryInfoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Client.SendAsync<LibraryInfoStateDto>(HttpMethod.Put, PathOf(target), request, cancellationToken);
    }

    private static string PathOf(LibraryInfoTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Scope == LibraryInfoScope.Track ? ApiRoutes.TrackLibraryInfo(target.Id) : ApiRoutes.AssetLibraryInfo(target.Id);
    }
}
