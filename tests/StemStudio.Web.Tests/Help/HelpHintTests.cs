using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Components.Help;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Help;

/// <summary>Help hints, tooltips and inline hints (T048, FR-022).</summary>
public sealed class HelpHintTests : HelpTestContext
{
    private static RenderFragment Control => b => b.AddMarkupContent(0, "<input data-testid=\"pan-vocals\" data-help-id=\"mixer-pan\" />");

    [Fact]
    public async Task HelpHint_OpensThePanelAtItsTopic()
    {
        var hint = Render<HelpHint>(p => p.Add(h => h.HelpId, "mixer-pan"));

        var button = hint.Find("[data-testid=help-hint-mixer-pan]");
        button.GetAttribute("aria-label").ShouldBe("Help: Pan");
        await button.ClickAsync(new MouseEventArgs());

        Help.IsOpen.ShouldBeTrue();
        Help.TopicId.ShouldBe("mixer-pan");
    }

    [Fact]
    public void HelpHint_ForAPatternAnchor_UsesTheTopicId()
    {
        var hint = Render<HelpHint>(p => p.Add(h => h.HelpId, "strip-bass"));

        hint.Find("[data-testid=help-hint-mixer-screen]").ShouldNotBeNull();
    }

    [Fact]
    public void HelpHint_WithoutTopic_RendersNothing()
    {
        Render<HelpHint>(p => p.Add(h => h.HelpId, "unknown-marker")).Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public async Task HelpTip_ShowsTheSummary_OnFocus_AndHover_WithLearnMore()
    {
        var tip = Render<HelpTip>(p => p.Add(t => t.HelpId, "mixer-pan").Add(t => t.ChildContent, Control));
        tip.FindAll("[data-testid=help-tooltip-mixer-pan]").ShouldBeEmpty();

        await tip.Find(".ss-helptip").TriggerEventAsync("onfocusin", new FocusEventArgs());

        var tooltip = tip.Find("[data-testid=help-tooltip-mixer-pan]");
        tooltip.GetAttribute("role").ShouldBe("tooltip");
        tooltip.TextContent.ShouldContain("Moves a stem between the left and right speaker.");
        await tip.Find("[data-testid=help-learn-more]").ClickAsync(new MouseEventArgs());
        Help.TopicId.ShouldBe("mixer-pan");
        tip.FindAll("[data-testid=help-tooltip-mixer-pan]").ShouldBeEmpty();

        await tip.Find(".ss-helptip").TriggerEventAsync("onmouseenter", new MouseEventArgs());
        tip.Find("[data-testid=help-tooltip-mixer-pan]").ShouldNotBeNull();
        await tip.Find(".ss-helptip").TriggerEventAsync("onmouseleave", new MouseEventArgs());
        tip.FindAll("[data-testid=help-tooltip-mixer-pan]").ShouldBeEmpty();
    }

    [Fact]
    public async Task HelpTip_HidesOnEscape_AndOnFocusOut()
    {
        var tip = Render<HelpTip>(p => p.Add(t => t.HelpId, "mixer-pan").Add(t => t.ChildContent, Control));
        await tip.Find(".ss-helptip").TriggerEventAsync("onfocusin", new FocusEventArgs());

        await tip.Find(".ss-helptip").KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });
        tip.Find("[data-testid=help-tooltip-mixer-pan]").ShouldNotBeNull();
        await tip.Find(".ss-helptip").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        tip.FindAll("[data-testid=help-tooltip-mixer-pan]").ShouldBeEmpty();

        await tip.Find(".ss-helptip").TriggerEventAsync("onfocusin", new FocusEventArgs());
        await tip.Find(".ss-helptip").TriggerEventAsync("onfocusout", new FocusEventArgs());
        tip.FindAll("[data-testid=help-tooltip-mixer-pan]").ShouldBeEmpty();
    }

    [Fact]
    public async Task HelpTip_WithoutTopic_OnlyRendersTheControl()
    {
        var tip = Render<HelpTip>(p => p.Add(t => t.HelpId, "unknown-marker").Add(t => t.ChildContent, Control));

        await tip.Find(".ss-helptip").TriggerEventAsync("onfocusin", new FocusEventArgs());

        tip.Find("[data-testid=pan-vocals]").ShouldNotBeNull();
        tip.FindAll("[role=tooltip]").ShouldBeEmpty();
    }

    [Fact]
    public async Task InlineHint_IsBeginnerOnly_AndCanBeDismissed()
    {
        await Preferences.LoadAsync();
        var beginner = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(ExperienceLevel.Beginner, DensityPreference.Comfortable))
            .AddChildContent<InlineHint>(h => h.Add(x => x.TopicId, "mixer-pan")));
        var advanced = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(ExperienceLevel.Advanced, DensityPreference.Comfortable))
            .AddChildContent<InlineHint>(h => h.Add(x => x.TopicId, "mixer-pan")));

        advanced.FindAll("[data-testid=hint-mixer-pan]").ShouldBeEmpty();
        beginner.Find("[data-testid=hint-mixer-pan]").TextContent.ShouldContain("Moves a stem");
        await beginner.Find("[data-testid=hint-mixer-pan] button").ClickAsync(new MouseEventArgs());

        Preferences.Current.Help.HintsDismissed.ShouldContain("mixer-pan");
        beginner.FindAll("[data-testid=hint-mixer-pan]").ShouldBeEmpty();
    }

    [Fact]
    public async Task InlineHint_UnknownTopic_RendersNothing()
    {
        await Preferences.LoadAsync();

        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(ExperienceLevel.Beginner, DensityPreference.Comfortable))
            .AddChildContent<InlineHint>(h => h.Add(x => x.TopicId, "nope"))).FindAll("[data-testid^=hint-]").ShouldBeEmpty();
    }
}
