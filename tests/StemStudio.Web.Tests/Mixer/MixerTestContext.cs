using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>Mixer page fixture: a completed six-stem track and an engine that loads it in 213.04 s.</summary>
public abstract class MixerTestContext : PageTestContext
{
    protected MixerTestContext()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        Engine.LoadTrackAsync(Arg.Any<TrackDto>()).Returns(213.04);
    }

    protected IRenderedComponent<MixerPage> RenderMixer(Guid? mixId = null)
    {
        if (mixId is { } id)
        {
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
                .NavigateTo($"tracks/{TestData.TrackId}/mixer?mixId={id}");
        }

        return Render<MixerPage>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    protected static void AssertEveryControlIsNamed(IRenderedComponent<MixerPage> page)
    {
        foreach (var control in page.FindAll("input, button"))
        {
            var name = control.GetAttribute("aria-label") ?? control.TextContent.Trim();
            name.ShouldNotBeNullOrWhiteSpace($"{control.GetAttribute("data-testid")} has no accessible name");
        }
    }

    protected static void SetValue(AngleSharp.Dom.IElement input, double value) =>
        input.Input(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
