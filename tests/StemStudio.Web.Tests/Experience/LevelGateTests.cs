using Bunit;
using Microsoft.AspNetCore.Components;
using StemStudio.Web.Components.Experience;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Experience;

public sealed class LevelGateTests : BunitContext
{
    private static RenderFragment Child => b => b.AddMarkupContent(0, "<span data-testid=\"gated\">pan</span>");

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, string feature) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<LevelGate>(g => g.Add(x => x.Feature, feature).Add(x => x.ChildContent, Child)));

    [Theory]
    [InlineData(ExperienceLevel.Beginner, false)]
    [InlineData(ExperienceLevel.Advanced, true)]
    [InlineData(ExperienceLevel.Expert, true)]
    public void RendersChild_OnlyWhereTheFeatureIsVisible(ExperienceLevel level, bool visible)
    {
        var cut = RenderAt(level, "mixer.pan");

        cut.FindAll("[data-testid=gated]").Count.ShouldBe(visible ? 1 : 0);
    }

    [Fact]
    public void ReRenders_WhenTheLevelChanges()
    {
        var cut = RenderAt(ExperienceLevel.Advanced, "mixer.pan");

        cut.Render(p => p.Add(c => c.Value, new ExperienceContext(ExperienceLevel.Beginner, DensityPreference.Comfortable)));

        cut.FindAll("[data-testid=gated]").ShouldBeEmpty();
    }

    [Fact]
    public void WithoutAContext_RendersTheAdvancedSet()
    {
        var shown = Render<LevelGate>(p => p.Add(x => x.Feature, "mixer.pan").Add(x => x.ChildContent, Child));
        var hidden = Render<LevelGate>(p => p.Add(x => x.Feature, "journey.rail").Add(x => x.ChildContent, Child));

        shown.FindAll("[data-testid=gated]").Count.ShouldBe(1);
        hidden.FindAll("[data-testid=gated]").ShouldBeEmpty();
    }

    [Fact]
    public void UnknownFeature_Throws()
    {
        Should.Throw<ArgumentException>(() => RenderAt(ExperienceLevel.Advanced, "mixer.nope"));
    }
}
