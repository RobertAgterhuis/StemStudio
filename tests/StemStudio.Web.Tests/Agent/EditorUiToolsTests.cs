using Microsoft.Extensions.DependencyInjection;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>
/// The layout UI tools of 022 (<c>open_library_folder</c>, catalogue 1.1.0, T081) and 023 (<c>open_lyrics_editor</c>, catalogue 1.2.0,
/// T058): navigation through the NavigationManager (so every unsaved-changes guard applies) and readable failures.
/// </summary>
public sealed class EditorUiToolsTests : PageTestContext
{
    private const string Folder = "ar0123456789abcdef";
    private static readonly Guid LyricsId = Guid.Parse("2c4e6a8b-0d1f-4a3b-8c5d-7e9f1a3b5c7d");
    private static readonly Guid AssistId = Guid.Parse("5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d");

    private AgentUiToolHost Host => Services.GetRequiredService<AgentUiToolHost>();

    private static UiRequestedEventDto Request(string tool, string arguments) => new()
    {
        StepId = AgentData.StepId,
        Tool = tool,
        Arguments = AgentData.Json(arguments),
        TimeoutSeconds = AgentContractLimits.UiToolTimeoutSeconds,
    };

    [Fact]
    public async Task OpenLibraryFolder_NavigatesToTheFolder()
    {
        var result = await Host.ExecuteAsync(Request("open_library_folder", $$"""{"folderId":"{{Folder}}"}"""));

        (result.Ok, result.Result!.Value.GetProperty("opened").GetBoolean()).ShouldBe((true, true));
        new Uri(NavigatedTo).AbsolutePath.ShouldBe($"/library/{Folder}");
    }

    [Fact]
    public async Task OpenLibraryFolder_WithAQuery_OpensTheSearchEscaped()
    {
        var result = await Host.ExecuteAsync(Request("open_library_folder", """{"query":"Ace & Spades"}"""));

        result.Ok.ShouldBeTrue();
        new Uri(NavigatedTo).PathAndQuery.ShouldBe("/library?q=Ace%20%26%20Spades");
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"folderId":"ar0123456789abcdef","query":"x"}""")]
    [InlineData("""{"folderId":"../settings"}""")]
    [InlineData("""{"folderId":"xx0123456789abcdef"}""")]
    [InlineData("""{"query":""}""")]
    public async Task OpenLibraryFolder_WithoutExactlyOneValidArgument_DoesNotNavigate(string arguments)
    {
        var before = NavigatedTo;

        var result = await Host.ExecuteAsync(Request("open_library_folder", arguments));

        result.Ok.ShouldBeTrue();
        result.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeFalse();
        result.Result!.Value.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        NavigatedTo.ShouldBe(before);
    }

    [Fact]
    public async Task OpenLibraryFolder_WithATooLongQuery_DoesNotNavigate()
    {
        var result = await Host.ExecuteAsync(Request("open_library_folder", $$"""{"query":"{{new string('a', 101)}}"}"""));

        result.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task OpenLyricsEditor_NavigatesToTheSheet()
    {
        var result = await Host.ExecuteAsync(Request("open_lyrics_editor", $$"""{"lyricsId":"{{LyricsId}}"}"""));

        result.Result!.Value.GetProperty("opened").GetBoolean().ShouldBeTrue();
        new Uri(NavigatedTo).PathAndQuery.ShouldBe($"/lyrics/{LyricsId}");
    }

    [Fact]
    public async Task OpenLyricsEditor_WithAnAssist_OpensThePaneOnIt()
    {
        await Host.ExecuteAsync(Request("open_lyrics_editor", $$"""{"lyricsId":"{{LyricsId}}","assistId":"{{AssistId}}"}"""));

        new Uri(NavigatedTo).PathAndQuery.ShouldBe($"/lyrics/{LyricsId}?assist={AssistId}");
    }

    [Fact]
    public async Task OpenLyricsEditor_WithoutASheet_FailsReadably()
    {
        var result = await Host.ExecuteAsync(Request("open_lyrics_editor", "{}"));

        (result.Ok, result.Error).ShouldBe((false, "The argument 'lyricsId' is required."));
    }

    [Theory]
    [InlineData("open_library_folder")]
    [InlineData("open_lyrics_editor")]
    public void Layout_OwnsTheTool(string tool) => NavigationUiTools.Names.ShouldContain(tool);
}
