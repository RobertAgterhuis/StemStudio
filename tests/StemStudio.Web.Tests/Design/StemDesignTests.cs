using Bunit;
using Microsoft.AspNetCore.Components;
using StemStudio.Web.Components.Design;

namespace StemStudio.Web.Tests.Design;

public sealed class StemDesignTests : BunitContext
{
    [Theory]
    [InlineData("vocals", "var(--ss-stem-vocals)")]
    [InlineData("drums", "var(--ss-stem-drums)")]
    [InlineData("bass", "var(--ss-stem-bass)")]
    [InlineData("guitar", "var(--ss-stem-guitar)")]
    [InlineData("piano", "var(--ss-stem-piano)")]
    [InlineData("other", "var(--ss-stem-other)")]
    [InlineData("drums.snare", "var(--ss-stem-drums)")]
    [InlineData("vocals-lead", "var(--ss-stem-vocals)")]
    [InlineData("kazoo", "var(--ss-stem-other)")]
    [InlineData("", "var(--ss-stem-other)")]
    public void StemColor_IsTheStemOrParentToken_OtherForUnknown(string stem, string expected) =>
        StemColor.Var(stem).ShouldBe(expected);

    [Theory]
    [InlineData(0, "solid")]
    [InlineData(1, "dashed")]
    [InlineData(2, "dotted")]
    [InlineData(3, "double")]
    [InlineData(4, "solid")]
    [InlineData(-1, "solid")]
    public void StemColor_PatternCyclesPerDetailIndex(int index, string pattern) => StemColor.Pattern(index).ShouldBe(pattern);

    [Fact]
    public void StemColor_Parent_OfAChannel() => StemColor.Parent("drums.kick").ShouldBe("drums");

    [Fact]
    public void StemTab_ShowsTheName_AndCarriesTheColourVariable()
    {
        var tab = Render<StemTab>(p => p.Add(t => t.Stem, "guitar"));

        var span = tab.Find(".ss-stem-tab");
        span.TextContent.Trim().ShouldBe("guitar");
        span.GetAttribute("style").ShouldBe("--stem-color: var(--ss-stem-guitar)");
        span.GetAttribute("data-stem").ShouldBe("guitar");
        span.ClassList.ShouldContain("ss-pattern-solid");
    }

    [Fact]
    public void StemTab_ForADetailStem_UsesTheParentColourAndAPattern_AndAnOptionalLabel()
    {
        var tab = Render<StemTab>(p => p.Add(t => t.Stem, "drums.snare").Add(t => t.Pattern, 1).Add(t => t.Label, "Snare"));

        var span = tab.Find(".ss-stem-tab");
        span.TextContent.Trim().ShouldBe("Snare");
        span.GetAttribute("style").ShouldBe("--stem-color: var(--ss-stem-drums)");
        span.ClassList.ShouldContain("ss-pattern-dashed");
    }

    [Fact]
    public void StemRibbon_ListsStemsInDisplayOrder_AndIsDecorative()
    {
        var ribbon = Render<StemRibbon>(p => p.Add(r => r.Stems, ["other", "vocals", "bass", "drums"]).Add(r => r.TestId, "track-ribbon"));

        var svg = ribbon.Find("[data-testid=track-ribbon]");
        svg.GetAttribute("data-stems").ShouldBe("vocals,drums,bass,other");
        svg.GetAttribute("aria-hidden").ShouldBe("true");
        svg.GetAttribute("focusable").ShouldBe("false");
        ribbon.FindAll(".ss-ribbon-strand").Count.ShouldBe(6);
        ribbon.FindAll(".ss-ribbon-strand.ss-ribbon-missing").Count.ShouldBe(2);
    }

    [Fact]
    public void StemRibbon_WithoutStems_IsOneGreyStrand()
    {
        var ribbon = Render<StemRibbon>(p => p.Add(r => r.Stems, []).Add(r => r.TestId, "x"));

        ribbon.Find("[data-testid=x]").GetAttribute("data-stems").ShouldBe("");
        ribbon.FindAll(".ss-ribbon-strand").ShouldBeEmpty();
        ribbon.FindAll(".ss-ribbon-song").Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(SplitState.Idle, "idle")]
    [InlineData(SplitState.Running, "running")]
    [InlineData(SplitState.Done, "done")]
    public void SplitProgress_ExposesItsState_AndIsDecorative(SplitState state, string text)
    {
        var split = Render<SplitProgress>(p => p.Add(s => s.State, state).Add(s => s.Progress, 42).Add(s => s.TestId, "split-progress"));

        var svg = split.Find("[data-testid=split-progress]");
        svg.GetAttribute("data-state").ShouldBe(text);
        svg.GetAttribute("aria-hidden").ShouldBe("true");
        split.FindAll(".ss-split-strand").Count.ShouldBe(6);
    }

    [Fact]
    public void SplitProgress_DrawsProgressThroughACustomProperty_NotByScript()
    {
        var split = Render<SplitProgress>(p => p.Add(s => s.State, SplitState.Running).Add(s => s.Progress, 42.4).Add(s => s.TestId, "x"));

        split.Find("[data-testid=x]").GetAttribute("style").ShouldBe("--split: 0.424");
    }

    [Theory]
    [InlineData(-5, "0")]
    [InlineData(250, "1")]
    public void SplitProgress_ClampsProgress(double progress, string value)
    {
        var split = Render<SplitProgress>(p => p.Add(s => s.State, SplitState.Running).Add(s => s.Progress, progress).Add(s => s.TestId, "x"));

        split.Find("[data-testid=x]").GetAttribute("style").ShouldBe($"--split: {value}");
    }

    [Fact]
    public void SplitProgress_Done_RevealsOnce_WhichReducedMotionDisablesInCss()
    {
        var done = Render<SplitProgress>(p => p.Add(s => s.State, SplitState.Done).Add(s => s.Progress, 100).Add(s => s.TestId, "x"));
        var idle = Render<SplitProgress>(p => p.Add(s => s.State, SplitState.Idle).Add(s => s.Progress, 0).Add(s => s.TestId, "y"));

        done.Find("svg").ClassList.ShouldContain("ss-split-reveal");
        idle.Find("svg").ClassList.ShouldNotContain("ss-split-reveal");
        Fakes.RepoPaths.ReadWeb("wwwroot/css/components/design.css").ShouldContain("html[data-motion=\"reduced\"] .ss-split-reveal");
    }

    [Fact]
    public void EmptyState_HasTheSentenceAndTheAction()
    {
        var empty = Render<EmptyState>(p => p
            .Add(e => e.TestId, "tracks-empty")
            .Add(e => e.Text, "No tracks yet.")
            .Add(e => e.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<a href=\"\">Import a song</a>"))));

        var element = empty.Find("[data-testid=tracks-empty]");
        element.TextContent.ShouldContain("No tracks yet.");
        element.QuerySelector("a")!.TextContent.ShouldBe("Import a song");
    }

    [Fact]
    public void ErrorState_KeepsTheTestIdOnTheTextElement_WithAHintAndAnOptionalAction()
    {
        var error = Render<ErrorState>(p => p
            .Add(e => e.TestId, "tracks-error")
            .Add(e => e.Message, "The API is not reachable.")
            .Add(e => e.Hint, "Check that the stack is running.")
            .Add(e => e.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<button>Try again</button>"))));

        var text = error.Find("[data-testid=tracks-error]");
        text.TextContent.ShouldBe("The API is not reachable.");
        text.GetAttribute("role").ShouldBe("alert");
        error.Find(".ss-state-hint").TextContent.ShouldBe("Check that the stack is running.");
        error.Find("button").TextContent.ShouldBe("Try again");
    }

    [Fact]
    public void ErrorState_WithoutHint_RendersNoHint()
    {
        var error = Render<ErrorState>(p => p.Add(e => e.TestId, "x").Add(e => e.Message, "Broken."));

        error.FindAll(".ss-state-hint").ShouldBeEmpty();
    }

    [Fact]
    public void Skeleton_IsAStatusWithLoadingText_AndDecorativeBars()
    {
        var skeleton = Render<StemStudio.Web.Components.Design.Skeleton>(p => p.Add(s => s.Lines, 4));

        var status = skeleton.Find("[role=status]");
        status.TextContent.ShouldContain("Loading...");
        skeleton.FindAll(".ss-skeleton-bar[aria-hidden=true]").Count.ShouldBe(4);
    }

    [Fact]
    public void Skeleton_AcceptsACustomLabel()
    {
        var skeleton = Render<StemStudio.Web.Components.Design.Skeleton>(p => p.Add(s => s.Label, "Loading stems...").Add(s => s.TestId, "mixer-loading"));

        skeleton.Find("[data-testid=mixer-loading]").TextContent.Trim().ShouldBe("Loading stems...");
    }
}
