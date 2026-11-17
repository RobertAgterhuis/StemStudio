using StemStudio.Agent.Ports;
using StemStudio.AgentEval.Fixture;
using StemStudio.Contracts.Library;

namespace StemStudio.AgentEval.Tests;

/// <summary>Architect skeleton (022 Phase 0, T003): the library group of the fixture answers 501 until the ai-agent seeds it (T083).</summary>
public sealed class FixtureLibraryActionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, Func<ILibraryActions, Task>> Calls => new()
    {
        { "BrowseLibraryFolder", actions => actions.BrowseLibraryFolderAsync(new LibraryFolderQuery(), Token) },
        { "SearchLibrary", actions => actions.SearchLibraryAsync(new LibrarySearchQuery { Query = "snare" }, Token) },
        { "GetLibraryInfo", actions => actions.GetLibraryInfoAsync(LibraryInfoTarget.ForTrack(Guid.NewGuid()), Token) },
        { "SetLibraryInfo", actions => actions.SetLibraryInfoAsync(LibraryInfoTarget.ForItem(Guid.NewGuid()), LibraryInfoRequest.Automatic, Token) },
    };

    [Fact]
    public void FixtureActions_ExposeTheLibraryGroup()
    {
        new FixtureActions(FixtureLibrary.Seed(Repo.Presets)).Library.ShouldBeOfType<FixtureLibraryActions>();
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void EveryLibraryAction_AnswersNotImplemented(string name, Func<ILibraryActions, Task> call)
    {
        var error = Should.Throw<AgentActionException>(() => call(new FixtureLibraryActions()));

        (error.StatusCode, error.ProblemCode).ShouldBe((501, "not-implemented"), name);
    }
}
