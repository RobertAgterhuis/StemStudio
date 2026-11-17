using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Components.Lyrics.Assist;
using StemStudio.Web.Components.Songs;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>
/// bUnit context of the lyrics editor page (<c>/lyrics/{id}</c>) with the assistant: the sheet "Zomeravond", a ready local model,
/// a check whose stream sends nothing (tests queue events with <see cref="Stream"/>), and a substituted editor interop.
/// </summary>
public abstract class LyricEditorTestContext : PageTestContext
{
    protected LyricEditorTestContext()
    {
        Services.AddSingleton(EditorInterop);
        EditorInterop.GetSelectionAsync(Arg.Any<Microsoft.AspNetCore.Components.ElementReference>()).Returns((0, 0));
        Api.GetLyricsSheetAsync(LyricsId, Arg.Any<CancellationToken>()).Returns(Sheet());
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status());
        Api.GetLyricAssistSettingsAsync(Arg.Any<CancellationToken>()).Returns(LyricAssistData.Settings());
        Api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Queued));
        Api.GetLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Completed));
        Api.ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>()).Returns(List());
        Stream();
    }

    /// <summary>The browser side of the editor.</summary>
    protected ILyricEditorInterop EditorInterop { get; } = Substitute.For<ILyricEditorInterop>();

    /// <summary>Makes every assist stream these events.</summary>
    /// <param name="events">The events.</param>
    protected void Stream(params (string Type, object Data)[] events) =>
        Api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(_ => Events(events));

    /// <summary>A check that streams these suggestions and completes.</summary>
    /// <param name="suggestions">The suggestions.</param>
    protected void CheckFinds(params LyricSuggestionDto[] suggestions) => Stream([
        .. suggestions.Select(s => (LyricAssistEventTypes.SuggestionAdded, (object)s)),
        (LyricAssistEventTypes.Completed, Completed(spelling: suggestions.Count(s => s.Category == SuggestionCategoryDto.Spelling),
            grammar: suggestions.Count(s => s.Category == SuggestionCategoryDto.Grammar))),
    ]);

    /// <summary>Renders the page at a level and opens the pane.</summary>
    protected IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> Open(
        ExperienceLevel level = ExperienceLevel.Advanced, LyricsSheetDto? sheet = null, Guid? assist = null)
    {
        if (sheet is not null)
        {
            Api.GetLyricsSheetAsync(LyricsId, Arg.Any<CancellationToken>()).Returns(sheet);
        }

        if (assist is { } id)
        {
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo($"lyrics/{LyricsId}?assist={id}");
        }

        var page = RenderAtLevel<LyricsEditorPage>(level, p => p.Add(x => x.Id, LyricsId));
        if (assist is null)
        {
            page.WaitForElement("[data-testid=lyric-assist-toggle]").Click();
        }

        page.WaitForElement("[data-testid=lyric-assist-pane]");
        return page;
    }

    /// <summary>Opens the pane and runs a check that finds the given suggestions.</summary>
    protected IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> Checked(ExperienceLevel level, params LyricSuggestionDto[] suggestions)
    {
        CheckFinds(suggestions);
        var page = Open(level);
        page.Find("[data-testid=lyric-assist-check]").Click();
        page.WaitForAssertion(() => page.Find("[data-testid=lyric-assist-pane]").GetAttribute("data-state").ShouldBe("done"));
        return page;
    }

    /// <summary>The test id selector.</summary>
    protected static string Id(string testId) => $"[data-testid=\"{testId}\"]";
}
