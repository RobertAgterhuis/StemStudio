using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Shell;

public sealed class AppShellTests : PageTestContext
{
    private static RenderFragment Body => b => b.AddMarkupContent(0, "<h1 data-testid=\"page-body\">Page body</h1>");

    private IRenderedComponent<MainLayout> RenderShell() => Render<MainLayout>(p => p.Add(l => l.Body, Body));

    private static IElement Id(IRenderedComponent<MainLayout> shell, string testId) => shell.Find($"[data-testid={testId}]");

    [Fact]
    public void Rail_HasTheNavigationLinks_AndTheHelpItem()
    {
        var shell = RenderShell();

        shell.Find("nav").GetAttribute("aria-label").ShouldBe("Main navigation");
        Id(shell, "nav-import").GetAttribute("href").ShouldBe("");
        Id(shell, "nav-tracks").GetAttribute("href").ShouldBe("tracks");
        Id(shell, "nav-mixes").GetAttribute("href").ShouldBe("mixes");
        Id(shell, "nav-help").TagName.ShouldBe("BUTTON");
        shell.Find("nav").TextContent.ShouldContain("Import");
        shell.Markup.ShouldContain("StemStudio");
    }

    [Fact]
    public void SkipLink_IsTheFirstFocusableElement_AndTargetsMain()
    {
        var shell = RenderShell();

        var first = shell.Find("a[href], button, input");
        first.GetAttribute("data-testid").ShouldBe("skip-link");
        first.GetAttribute("href").ShouldBe("#main");
        shell.Find("main").Id.ShouldBe("main");
    }

    [Fact]
    public void Landmarks_AreNavHeaderAndMain()
    {
        var shell = RenderShell();

        shell.FindAll("header").Count.ShouldBe(1);
        shell.FindAll("main").Count.ShouldBe(1);
    }

    [Fact]
    public void ContextBar_HasLevelSwitch_Help_Palette_AndPreferencesButtons()
    {
        var shell = RenderShell();

        var levels = Id(shell, "level-switch");
        levels.GetAttribute("role").ShouldBe("radiogroup");
        levels.QuerySelectorAll("[role=radio]").Select(r => r.GetAttribute("data-testid")).ShouldBe(["level-beginner", "level-advanced", "level-expert"]);
        Id(shell, "help-toggle").GetAttribute("aria-expanded").ShouldBe("false");
        Id(shell, "command-palette-open").TagName.ShouldBe("BUTTON");
        Id(shell, "preferences-open").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public void Content_RendersOnlyAfterPreferencesLoaded_WithASkeletonBefore()
    {
        Store.Hold();
        var shell = RenderShell();

        shell.FindAll("[data-testid=page-body]").ShouldBeEmpty();
        shell.Find("main [role=status]").TextContent.ShouldContain("Loading");

        Store.Release();

        shell.WaitForAssertion(() => Id(shell, "page-body").ShouldNotBeNull());
    }

    [Fact]
    public void AppShell_CarriesLevelAndDensity_OfTheLoadedPreferences()
    {
        Store.Snapshot = new(UserPreferences.Default with { Level = ExperienceLevel.Expert }, true);

        var shell = RenderShell();

        var root = Id(shell, "app-shell");
        root.GetAttribute("data-level").ShouldBe("expert");
        root.GetAttribute("data-density").ShouldBe("compact");
        Id(shell, "level-expert").GetAttribute("aria-checked").ShouldBe("true");
    }

    [Fact]
    public void Defaults_AreBeginnerAndComfortable()
    {
        var shell = RenderShell();

        Id(shell, "app-shell").GetAttribute("data-level").ShouldBe("beginner");
        Id(shell, "app-shell").GetAttribute("data-density").ShouldBe("comfortable");
        Id(shell, "level-beginner").GetAttribute("aria-checked").ShouldBe("true");
        Id(shell, "level-beginner").GetAttribute("tabindex").ShouldBe("0");
        Id(shell, "level-advanced").GetAttribute("tabindex").ShouldBe("-1");
    }

    [Fact]
    public async Task ClickingALevel_SwitchesWithoutNavigation_AndAnnouncesIt()
    {
        var shell = RenderShell();
        var uri = NavigatedTo;

        await Id(shell, "level-expert").ClickAsync(new MouseEventArgs());

        Id(shell, "app-shell").GetAttribute("data-level").ShouldBe("expert");
        Id(shell, "level-expert").GetAttribute("aria-checked").ShouldBe("true");
        Id(shell, "level-beginner").GetAttribute("aria-checked").ShouldBe("false");
        Id(shell, "page-body").ShouldNotBeNull();
        NavigatedTo.ShouldBe(uri);
        Preferences.Current.Level.ShouldBe(ExperienceLevel.Expert);
        Id(shell, "toast").TextContent.ShouldBe("Level set to Expert");
        Id(shell, "toast").GetAttribute("role").ShouldBe("status");
    }

    [Theory]
    [InlineData("ArrowRight", "level-advanced")]
    [InlineData("ArrowDown", "level-advanced")]
    [InlineData("ArrowLeft", "level-expert")]
    [InlineData("ArrowUp", "level-expert")]
    [InlineData("End", "level-expert")]
    [InlineData("Home", "level-beginner")]
    public async Task ArrowKeys_MoveTheLevelSelection(string key, string selected)
    {
        var shell = RenderShell();

        await Id(shell, "level-beginner").KeyDownAsync(new KeyboardEventArgs { Key = key });

        Id(shell, selected).GetAttribute("aria-checked").ShouldBe("true");
    }

    [Fact]
    public async Task OtherKeys_DoNothing()
    {
        var shell = RenderShell();

        await Id(shell, "level-beginner").KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        Preferences.Current.Level.ShouldBeNull();
    }

    [Fact]
    public async Task Toast_DisappearsAfterAFewSeconds()
    {
        var shell = RenderShell();
        await Id(shell, "level-advanced").ClickAsync(new MouseEventArgs());

        Time.Advance(ToastService.Duration);

        shell.WaitForAssertion(() => shell.FindAll("[data-testid=toast]").ShouldBeEmpty());
    }

    [Fact]
    public async Task PreferencesPanel_OpensWithEveryChoice()
    {
        var shell = RenderShell();

        await Id(shell, "preferences-open").ClickAsync(new MouseEventArgs());

        Id(shell, "preferences-open").GetAttribute("aria-expanded").ShouldBe("true");
        var panel = Id(shell, "preferences-panel");
        foreach (var id in new[]
        {
            "pref-theme-system", "pref-theme-light", "pref-theme-dark", "pref-density-auto", "pref-density-comfortable",
            "pref-density-compact", "pref-motion-system", "pref-motion-reduced", "pref-reset-help",
        })
        {
            panel.QuerySelector($"[data-testid={id}]").ShouldNotBeNull(id);
        }

        panel.QuerySelectorAll("[data-testid=pref-storage-unavailable]").ShouldBeEmpty();
        Id(shell, "pref-theme-system").GetAttribute("aria-checked").ShouldBe("true");
    }

    [Fact]
    public async Task PreferencesPanel_ChangesThemeDensityAndMotion_Immediately()
    {
        var shell = RenderShell();
        await Id(shell, "preferences-open").ClickAsync(new MouseEventArgs());

        await Id(shell, "pref-theme-light").ClickAsync(new MouseEventArgs());
        await Id(shell, "pref-density-compact").ClickAsync(new MouseEventArgs());
        await Id(shell, "pref-motion-reduced").ClickAsync(new MouseEventArgs());

        Preferences.Current.Theme.ShouldBe(ThemePreference.Light);
        Preferences.Current.Motion.ShouldBe(MotionPreference.Reduced);
        Interop.Applied.Last().GetProperty("theme").GetString().ShouldBe("light");
        Id(shell, "app-shell").GetAttribute("data-density").ShouldBe("compact");
        Id(shell, "pref-theme-light").GetAttribute("aria-checked").ShouldBe("true");
    }

    [Fact]
    public async Task ResetHelp_ClearsHelpProgress_AndConfirms()
    {
        Store.Snapshot = new(UserPreferences.Default with { Help = HelpProgress.Empty with { WelcomeDismissed = true } }, true);
        var shell = RenderShell();
        await Id(shell, "preferences-open").ClickAsync(new MouseEventArgs());

        await Id(shell, "pref-reset-help").ClickAsync(new MouseEventArgs());

        Preferences.Current.Help.WelcomeDismissed.ShouldBeFalse();
        Id(shell, "toast").TextContent.ShouldBe("Help and tours reset");
    }

    [Fact]
    public async Task UnavailableStorage_IsSaidInThePanel()
    {
        Store.Snapshot = new(UserPreferences.Default, false);
        var shell = RenderShell();

        await Id(shell, "preferences-open").ClickAsync(new MouseEventArgs());

        Id(shell, "pref-storage-unavailable").TextContent.ShouldContain("will not be remembered");
    }

    [Fact]
    public async Task PreferencesPanel_ClosesWithEscape_AndTheButton()
    {
        var shell = RenderShell();
        await Id(shell, "preferences-open").ClickAsync(new MouseEventArgs());

        await Id(shell, "preferences-panel").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        shell.FindAll("[data-testid=preferences-panel]").ShouldBeEmpty();

        await Id(shell, "preferences-open").ClickAsync(new MouseEventArgs());
        await Id(shell, "preferences-panel").KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });
        Id(shell, "preferences-panel").ShouldNotBeNull();
        await Id(shell, "preferences-open").ClickAsync(new MouseEventArgs());
        shell.FindAll("[data-testid=preferences-panel]").ShouldBeEmpty();
    }

    [Fact]
    public void ChangesFromAnotherTab_ReRenderTheShell()
    {
        var shell = RenderShell();

        shell.InvokeAsync(() => Interop.PreferencesChanged!("""{"schemaVersion":1,"level":"advanced"}"""));

        shell.WaitForAssertion(() => Id(shell, "app-shell").GetAttribute("data-level").ShouldBe("advanced"));
    }

    [Fact]
    public void ExperienceContext_IsCascadedToTheBody()
    {
        ExperienceContext? seen = null;
        Store.Snapshot = new(UserPreferences.Default with { Level = ExperienceLevel.Advanced }, true);

        Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b =>
        {
            b.OpenComponent<CascadingProbe>(0);
            b.AddAttribute(1, nameof(CascadingProbe.Seen), (Action<ExperienceContext?>)(c => seen = c));
            b.CloseComponent();
        })));

        seen.ShouldBe(new ExperienceContext(ExperienceLevel.Advanced, DensityPreference.Comfortable));
    }

    private sealed class CascadingProbe : ComponentBase
    {
        [CascadingParameter]
        public ExperienceContext? Experience { get; set; }

        [Parameter]
        public Action<ExperienceContext?> Seen { get; set; } = _ => { };

        protected override void OnParametersSet() => Seen(Experience);
    }
}
