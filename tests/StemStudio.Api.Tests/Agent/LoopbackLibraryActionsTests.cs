using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Loopback;
using StemStudio.Contracts.Library;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// Architect skeleton (022 Phase 0, T003): <see cref="IStemStudioActions.Library"/> is wired to <see cref="LoopbackLibraryActions"/>,
/// whose methods are pending until T080. The backend replaces these tests with the T079 endpoint tests (red first).
/// </summary>
public sealed class LoopbackLibraryActionsTests
{
    private static readonly LoopbackClient Client = new(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000/") });

    public static TheoryData<string, Func<ILibraryActions, Task>> Calls => new()
    {
        { "BrowseLibraryFolder", actions => actions.BrowseLibraryFolderAsync(new LibraryFolderQuery(), CancellationToken.None) },
        { "SearchLibrary", actions => actions.SearchLibraryAsync(new LibrarySearchQuery { Query = "snare" }, CancellationToken.None) },
        { "GetLibraryInfo", actions => actions.GetLibraryInfoAsync(LibraryInfoTarget.ForTrack(Guid.NewGuid()), CancellationToken.None) },
        { "SetLibraryInfo", actions => actions.SetLibraryInfoAsync(LibraryInfoTarget.ForItem(Guid.NewGuid()), LibraryInfoRequest.Automatic, CancellationToken.None) },
    };

    [Fact]
    public void LoopbackActions_ExposeTheLibraryGroup()
    {
        var actions = new LoopbackActions(Client);

        actions.Library.ShouldBeOfType<LoopbackLibraryActions>().Client.ShouldBeSameAs(Client);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void EveryLibraryAction_IsPendingUntilT080(string name, Func<ILibraryActions, Task> call)
    {
        var error = Should.Throw<NotImplementedException>(() => call(new LoopbackLibraryActions(Client)));

        error.Message.ShouldBe(LoopbackLibraryActions.PendingMessage, name);
    }
}
