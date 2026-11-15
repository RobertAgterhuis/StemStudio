using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Track library in the design system (T029, FR-005): ribbons, states, search and sort at Advanced+.</summary>
public sealed class TracksDesignTests : PageTestContext
{
    private static readonly Guid OlderId = Guid.Parse("22222222-2222-3333-4444-555555555555");
    private static readonly Guid RunningId = Guid.Parse("33333333-2222-3333-4444-555555555555");

    private static readonly TrackDto Newest = TestData.Track() with { Title = "Zebra crossing", Artist = "Band B", CreatedAt = TestData.Created.AddDays(2) };
    private static readonly TrackDto Older = TestData.Track() with { Id = OlderId, Title = "Apple pie", Artist = "Alpha", CreatedAt = TestData.Created };
    private static readonly TrackDto Running = TestData.Track(TrackStatusDto.Separating) with
    {
        Id = RunningId, Title = "Middle", Artist = null, Stems = [], CreatedAt = TestData.Created.AddDays(1),
    };

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, params TrackDto[] tracks)
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns(tracks);
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<Tracks>());
    }

    private static IEnumerable<string> RowIds(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        page.FindAll("[data-testid^=track-row-]:not([data-testid^=track-row-status])").Select(r => r.GetAttribute("data-testid")!);

    [Fact]
    public void Rows_KeepTheirIds_AndShowARibbonOfTheStems()
    {
        var page = RenderAt(ExperienceLevel.Advanced, Newest, Running);

        var ribbon = page.Find($"[data-testid=track-ribbon-{TestData.TrackId}]");
        ribbon.GetAttribute("data-stems").ShouldBe("vocals,drums,bass,guitar,piano,other");
        page.Find($"[data-testid=track-ribbon-{RunningId}]").GetAttribute("data-stems").ShouldBe("");
        page.Find($"[data-testid=track-row-{TestData.TrackId}] [data-testid=track-link]").ShouldNotBeNull();
        page.Find($"[data-testid=track-row-status-{RunningId}]").TextContent.ShouldBe("Separating");
    }

    [Fact]
    public void DefaultOrder_IsNewestFirst()
    {
        var page = RenderAt(ExperienceLevel.Advanced, Older, Newest, Running);

        RowIds(page).ShouldBe([$"track-row-{TestData.TrackId}", $"track-row-{RunningId}", $"track-row-{OlderId}"]);
    }

    [Fact]
    public void Beginner_HasNoSearchOrSort_AndAPractiseAction()
    {
        var page = RenderAt(ExperienceLevel.Beginner, Newest);

        page.FindAll("[data-testid=tracks-search]").ShouldBeEmpty();
        page.FindAll("[data-testid=tracks-sort]").ShouldBeEmpty();
        page.Find("[data-testid=track-mixer-link]").TextContent.Trim().ShouldBe("Practise");
    }

    [Theory]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void AdvancedAndExpert_HaveSearchAndSort(ExperienceLevel level)
    {
        var page = RenderAt(level, Newest);

        page.Find("[data-testid=tracks-search]").TagName.ShouldBe("INPUT");
        page.Find("[data-testid=tracks-sort]").TagName.ShouldBe("SELECT");
        page.Find("[data-testid=track-mixer-link]").TextContent.Trim().ShouldBe("Open mixer");
    }

    [Fact]
    public void Search_FiltersByTitleOrArtist_IgnoringCase()
    {
        var page = RenderAt(ExperienceLevel.Advanced, Older, Newest, Running);

        page.Find("[data-testid=tracks-search]").Input("ALPHA");

        RowIds(page).ShouldBe([$"track-row-{OlderId}"]);

        page.Find("[data-testid=tracks-search]").Input("crossing");
        RowIds(page).ShouldBe([$"track-row-{TestData.TrackId}"]);
    }

    [Fact]
    public void Search_WithoutMatches_SaysSo()
    {
        var page = RenderAt(ExperienceLevel.Advanced, Older);

        page.Find("[data-testid=tracks-search]").Input("nothing here");

        RowIds(page).ShouldBeEmpty();
        page.Find(".ss-no-results").TextContent.ShouldContain("No tracks match");
    }

    [Theory]
    [InlineData("oldest", new[] { "22222222-2222-3333-4444-555555555555", "33333333-2222-3333-4444-555555555555", "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a" })]
    [InlineData("title", new[] { "22222222-2222-3333-4444-555555555555", "33333333-2222-3333-4444-555555555555", "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a" })]
    [InlineData("artist", new[] { "22222222-2222-3333-4444-555555555555", "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a", "33333333-2222-3333-4444-555555555555" })]
    [InlineData("newest", new[] { "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a", "33333333-2222-3333-4444-555555555555", "22222222-2222-3333-4444-555555555555" })]
    public void Sort_ReordersClientSide(string sort, string[] ids)
    {
        var page = RenderAt(ExperienceLevel.Advanced, Older, Newest, Running);

        page.Find("[data-testid=tracks-sort]").Change(sort);

        RowIds(page).ShouldBe(ids.Select(id => $"track-row-{id}"));
        Api.Received(1).ListTracksAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Expert_AddsBpmAndModelColumns()
    {
        var page = RenderAt(ExperienceLevel.Expert, Newest);

        var headers = page.FindAll("th").Select(h => h.TextContent.Trim()).ToList();
        headers.ShouldContain("BPM");
        headers.ShouldContain("Model");
        page.Find($"[data-testid=track-row-{TestData.TrackId}]").TextContent.ShouldContain(TrackFacts.Bpm(Newest));
    }

    [Fact]
    public void Advanced_HasNoExpertColumns()
    {
        var page = RenderAt(ExperienceLevel.Advanced, Newest);

        page.FindAll("th").Select(h => h.TextContent.Trim()).ShouldNotContain("BPM");
    }

    [Fact]
    public void Empty_IsADesignedEmptyState_WithTheImportAction()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        var empty = page.Find("[data-testid=tracks-empty]");
        empty.ClassList.ShouldContain("ss-state-empty");
        empty.QuerySelector("a")!.GetAttribute("href").ShouldBe("");
    }

    [Fact]
    public void Loading_IsASkeleton()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<TrackDto>>().Task);

        var page = Render<Tracks>();

        page.Find(".ss-skeleton[role=status]").TextContent.ShouldContain("Loading...");
    }
}
