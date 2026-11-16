using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Mixer;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>Mixer page fixture for the practice controls (012): level rendering, engine status and the 50 ms sender.</summary>
public abstract class PracticeTestContext : MixerTestContext
{
    /// <summary>Renders the mixer under a cascaded level and waits until the engine is loaded.</summary>
    protected IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level = ExperienceLevel.Advanced, bool withMix = false)
    {
        if (withMix)
        {
            Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer?mixId={TestData.MixId}");
        }

        var page = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));
        page.WaitForAssertion(() => page.FindAll("[data-testid=mixer-loading]").ShouldBeEmpty());
        return page;
    }

    /// <summary>The engine reports a practice snapshot (the callback arrives off the renderer).</summary>
    protected void RaiseStatus(PracticeStatus status) =>
        Engine.PracticeChanged += Raise.Event<EventHandler<PracticeStatus>>(Engine, status);

    /// <summary>Lets the practice sender's 50 ms interval pass.</summary>
    protected void NextInterval() => Time.Advance(PracticeSender.Interval);

    /// <summary>A mix that carries practice settings.</summary>
    protected void MixWith(PracticeSettingsDto practice) =>
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>())
            .Returns(TestData.Mix() with { Settings = TestData.Settings() with { Practice = practice } });

    /// <summary>Settings at the defaults except the given speed and transpose.</summary>
    protected static PracticeSettingsDto At(int speed, int transpose = 0) =>
        PracticeSettingsDto.Default with { SpeedPercent = speed, TransposeSemitones = transpose };

    /// <summary>The engine's snapshot while the trainer plays a pass.</summary>
    protected static PracticeStatus Running(int speed, int pass = 1) =>
        PracticeStatus.Initial with { Stretch = StretchState.Active, Trainer = new TrainerStatus(TrainerState.Running, pass, speed) };
}
