using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Web.Components.Help;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Preferences;
using static StemStudio.Web.Tests.Help.HelpSamples;

namespace StemStudio.Web.Tests.Help;

/// <summary>Tour offers and the tour lifecycle in the app (T055, FR-027, data-model.md 1.3).</summary>
public sealed class TourOfferTests : HelpTestContext
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    public TourOfferTests()
    {
        Services.AddSingleton<IHelpCatalog>(new HelpCatalog(
        [
            .. Content,
            TourFile("import-basics", """{"id":"import-basics","version":2,"title":"Import basics","route":"/","levels":["beginner","advanced"],"steps":[{"target":"import-url","topic":"import-screen"}]}"""),
            TourFile("everywhere", """{"id":"everywhere","version":1,"title":"Everywhere","route":"*","levels":["expert"],"steps":[{"target":"help-button","topic":"stem"}]}"""),
        ]));
        Services.AddScoped<TourService>();
        Services.AddScoped<ITourLauncher>(s => s.GetRequiredService<TourService>());
    }

    private TourService Tours => Services.GetRequiredService<TourService>();

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> RenderOfferAsync(ExperienceLevel level, HelpProgress? help = null)
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = level, Help = help ?? HelpProgress.Empty }, true);
        await Preferences.LoadAsync();
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<TourOffer>());
    }

    private static HelpProgress Recorded(string id, int version) =>
        HelpProgress.Empty with { Tours = new Dictionary<string, TourRecord> { [id] = new(version, TourStatus.Completed, At) } };

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    public async Task Offer_IsShownForTheRouteAndLevel_WithoutStartingTheTour(ExperienceLevel level)
    {
        var offer = await RenderOfferAsync(level);

        offer.Find("[data-testid=tour-offer-import-basics]").TextContent.ShouldContain("Import basics");
        offer.Find("[data-testid=tour-offer-dismiss-import-basics]").GetAttribute("aria-label").ShouldBe("Dismiss the Import basics tour");
        Interop.StartedTour.ShouldBeNull();
    }

    [Fact]
    public async Task Offer_IsNotShown_AtALevelTheTourDoesNotTarget()
    {
        var offer = await RenderOfferAsync(ExperienceLevel.Expert);

        offer.FindAll("[data-testid=tour-offer-import-basics]").ShouldBeEmpty();
        offer.Find("[data-testid=tour-offer-everywhere]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Offer_IsNotShown_OnAnotherRoute()
    {
        GoTo("tracks");

        var offer = await RenderOfferAsync(ExperienceLevel.Beginner);

        offer.FindAll("[data-testid^=tour-offer-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Offer_FollowsNavigation()
    {
        var offer = await RenderOfferAsync(ExperienceLevel.Beginner);

        await offer.InvokeAsync(() => GoTo("tracks"));

        offer.FindAll("[data-testid^=tour-offer-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Offer_IsGone_AfterTheSameVersionWasRecorded()
    {
        var offer = await RenderOfferAsync(ExperienceLevel.Beginner, Recorded("import-basics", 2));

        offer.FindAll("[data-testid=tour-offer-import-basics]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Offer_Returns_ForANewerTourVersion()
    {
        var offer = await RenderOfferAsync(ExperienceLevel.Beginner, Recorded("import-basics", 1));

        offer.Find("[data-testid=tour-offer-import-basics]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Start_RunsTheTour()
    {
        var offer = await RenderOfferAsync(ExperienceLevel.Beginner);

        await offer.Find("[data-testid=tour-offer-import-basics] button").ClickAsync(new MouseEventArgs());

        Interop.StartedTour!.Id.ShouldBe("import-basics");
        Interop.StartedTour.Steps.Single().Title.ShouldBe("Import a song");
    }

    [Fact]
    public async Task Dismiss_RecordsTheVersion_AndHidesTheOffer()
    {
        var offer = await RenderOfferAsync(ExperienceLevel.Beginner);

        await offer.Find("[data-testid=tour-offer-dismiss-import-basics]").ClickAsync(new MouseEventArgs());

        Preferences.Current.Help.Tours["import-basics"].ShouldBe(new TourRecord(2, TourStatus.Dismissed, Time.GetUtcNow()));
        offer.FindAll("[data-testid=tour-offer-import-basics]").ShouldBeEmpty();
    }

    [Fact]
    public async Task CompletingTheTour_HidesTheOffer()
    {
        var offer = await RenderOfferAsync(ExperienceLevel.Beginner);
        await offer.Find("[data-testid=tour-offer-import-basics] button").ClickAsync(new MouseEventArgs());

        await offer.InvokeAsync(() => Interop.TourEnded!(new StemStudio.Web.Interop.TourEnded("import-basics", true, 0)));

        offer.FindAll("[data-testid=tour-offer-import-basics]").ShouldBeEmpty();
        Preferences.Current.Help.Tours["import-basics"].Status.ShouldBe(TourStatus.Completed);
    }

    [Fact]
    public async Task Navigation_ClosesARunningTour_AndRecordsItAsDismissed()
    {
        await Preferences.LoadAsync();
        await Tours.StartAsync("import-basics");

        GoTo("tracks");

        Interop.IsActive("tour:import-basics").ShouldBeFalse();
        Tours.Running.ShouldBeNull();
        Preferences.Current.Help.Tours["import-basics"].Status.ShouldBe(TourStatus.Dismissed);
    }

    [Fact]
    public async Task Navigation_WithoutARunningTour_RecordsNothing()
    {
        await Preferences.LoadAsync();
        await Tours.StartAsync("import-basics");
        await Tours.CloseAsync();

        GoTo("tracks");

        Preferences.Current.Help.Tours.ShouldBeEmpty();
    }

    [Fact]
    public async Task HelpPanel_ListsTheToursOfTheLevel_AndStartsOne()
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = ExperienceLevel.Advanced }, true);
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));
        await Help.OpenAsync("nope");
        shell.Render();

        shell.FindAll("[data-testid=help-tour-start-everywhere]").ShouldBeEmpty();
        await shell.Find("[data-testid=help-tour-start-import-basics]").ClickAsync(new MouseEventArgs());

        Interop.StartedTour!.Id.ShouldBe("import-basics");
        Help.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Shell_ShowsTheOfferAboveTheContent()
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = ExperienceLevel.Beginner }, true);

        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));

        shell.WaitForAssertion(() => shell.Find("main [data-testid=tour-offer-import-basics]").ShouldNotBeNull());
        await Task.CompletedTask;
    }
}
