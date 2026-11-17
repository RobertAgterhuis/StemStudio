using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T081: timing adjustment (FR-016, FR-020): Expert only, 0.1 ms steps, live shift, apply/reset, note.</summary>
public sealed class TakeAdjustTests : RecordingTestContext
{
    public TakeAdjustTests()
    {
        var wav = Wav();
        RecordingEngine.CaptureWavAsync(TakeId.ToString("D")).Returns(wav);
        Api.ReplaceTakeAudioAsync(TakeId, Arg.Any<Stream>(), Arg.Any<ReplaceTakeAudioMetadataDto>(), Arg.Any<CancellationToken>())
            .Returns(Take(status: TakeStatusDto.Saving));
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, false)]
    [InlineData(ExperienceLevel.Advanced, false)]
    [InlineData(ExperienceLevel.Expert, true)]
    public async Task Controls_AreExpertOnly(ExperienceLevel level, bool shown)
    {
        var adjust = await RenderAdjustAsync(level);

        foreach (var id in new[] { "take-adjust", "take-adjust-ms", "take-adjust-apply", "take-adjust-reset" })
        {
            adjust.FindAll($"[data-testid={id}]").Count.ShouldBe(shown ? 1 : 0, id);
        }
    }

    [Fact]
    public async Task Slider_ShiftsThePlaybackLive_InTenthsOfAMillisecond()
    {
        var adjust = await RenderAdjustAsync();
        var slider = adjust.Find("[data-testid=take-adjust]");
        (slider.GetAttribute("min"), slider.GetAttribute("max"), slider.GetAttribute("step")).ShouldBe(("-200", "200", "0.1"));

        slider.Input("-10.04");

        await RecordingEngine.Received(1).SetTakeAdjustmentAsync(-441);
        adjust.Find("[data-testid=take-adjust]").GetAttribute("aria-valuenow").ShouldBe("-10.0");
        adjust.Find("[data-testid=take-adjust-ms]").GetAttribute("value").ShouldBe("-10.0");
    }

    [Fact]
    public async Task NumberField_ClampsTo200()
    {
        var adjust = await RenderAdjustAsync();

        adjust.Find("[data-testid=take-adjust-ms]").Change("350");

        await RecordingEngine.Received(1).SetTakeAdjustmentAsync(8820);
        adjust.Find("[data-testid=take-adjust-ms]").Change("oops");
        Session.Takes.PendingAdjustmentMs.ShouldBe(200);
    }

    [Fact]
    public async Task Reset_GoesBackToTheStoredTiming()
    {
        var adjust = await RenderAdjustAsync();
        adjust.Find("[data-testid=take-adjust-reset]").HasAttribute("disabled").ShouldBeTrue();
        adjust.Find("[data-testid=take-adjust]").Input("5");

        adjust.Find("[data-testid=take-adjust-reset]").Click();

        await RecordingEngine.Received(1).SetTakeAdjustmentAsync(0);
        Session.Takes.PendingAdjustmentMs.ShouldBe(0);
    }

    [Fact]
    public async Task Apply_StoresTheReencodedTake_WithTheShiftedSpan()
    {
        var changed = 0;
        var adjust = await RenderAdjustAsync(onChanged: () => changed++);
        adjust.Find("[data-testid=take-adjust-apply]").HasAttribute("disabled").ShouldBeTrue();
        adjust.Find("[data-testid=take-adjust]").Input("-10");

        adjust.Find("[data-testid=take-adjust-apply]").Click();

        await Api.Received(1).ReplaceTakeAudioAsync(TakeId, Arg.Any<Stream>(),
            new ReplaceTakeAudioMetadataDto { StartSample = 441_000 - 441, LengthSamples = 352_800, AdjustmentSamples = -441 }, Arg.Any<CancellationToken>());
        Session.Takes.PendingAdjustmentMs.ShouldBe(0);
        Session.Takes.Loaded!.Status.ShouldBe("saving");
        changed.ShouldBe(1);
    }

    [Fact]
    public void Shifted_CutsAtSongSampleZero()
    {
        var early = Take() with { StartSample = 100, LengthSamples = 1000 };

        TakeLibrary.Shifted(early, -441).ShouldBe(new ReplaceTakeAudioMetadataDto { StartSample = 0, LengthSamples = 659, AdjustmentSamples = -441 });
        TakeLibrary.Shifted(early, 441).ShouldBe(new ReplaceTakeAudioMetadataDto { StartSample = 541, LengthSamples = 1000, AdjustmentSamples = 441 });
    }

    [Fact]
    public async Task ApplyRefused_ShowsTheProblem_AndKeepsThePendingShift()
    {
        Api.ReplaceTakeAudioAsync(TakeId, Arg.Any<Stream>(), Arg.Any<ReplaceTakeAudioMetadataDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(System.Net.HttpStatusCode.Conflict, "The take is still being saved"));
        var adjust = await RenderAdjustAsync();
        adjust.Find("[data-testid=take-adjust]").Input("3");

        adjust.Find("[data-testid=take-adjust-apply]").Click();

        Session.Takes.Error.ShouldNotBeNull().ShouldContain("still being saved");
        Session.Takes.PendingAdjustmentMs.ShouldBe(3);
    }

    [Fact]
    public async Task EngineRefusal_OfThePreview_IsShown()
    {
        RecordingEngine.SetTakeAdjustmentAsync(Arg.Any<int>()).ThrowsAsync(new RecordingException("not implemented: setTakeAdjustment"));
        var adjust = await RenderAdjustAsync();

        adjust.Find("[data-testid=take-adjust]").Input("3");

        Session.Takes.Error.ShouldBe("not implemented: setTakeAdjustment");
    }

    [Fact]
    public async Task WhenTheNewAudioIsReady_TheStripReloadsTheSavedTake()
    {
        var adjust = await RenderAdjustAsync();
        adjust.Find("[data-testid=take-adjust]").Input("-10");
        adjust.Find("[data-testid=take-adjust-apply]").Click();
        RecordingEngine.ClearReceivedCalls();

        await Session.Takes.ApplyAsync(Session.Takes.Loaded!, Take() with { StartSample = 440_559, AdjustmentSamples = -441, AlignmentRevision = 1 });

        await RecordingEngine.Received(1).LoadTakeAsync(Arg.Is<LoadTakeRequest>(r => r.Source.StartSample == 440_559 && r.AdjustmentSamples == 0));
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, 0)]
    [InlineData(ExperienceLevel.Advanced, 1)]
    public async Task Note_ShowsTheStoredAdjustmentFromAdvanced(ExperienceLevel level, int count)
    {
        var adjust = await RenderAdjustAsync(level, Take() with { AdjustmentSamples = -441 });

        adjust.FindAll("[data-testid=take-adjust-note]").Count.ShouldBe(count);
        TakeLibrary.AdjustmentNote(Take() with { AdjustmentSamples = -441 }).ShouldBe("Timing adjusted by -10.0 ms");
        TakeLibrary.AdjustmentNote(Take()).ShouldBeNull();
    }

    [Fact]
    public async Task WithoutASavedTake_RendersNothing_AndApplyIsANoOp()
    {
        await OpenWithInputAsync();

        Render<TakeAdjust>(p => p.Add(c => c.Takes, Session.Takes)).Markup.Trim().ShouldBeEmpty();
        await Session.Takes.ApplyAdjustmentAsync();
        await Api.DidNotReceive().ReplaceTakeAudioAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<ReplaceTakeAudioMetadataDto>(), Arg.Any<CancellationToken>());
    }

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> RenderAdjustAsync(
        ExperienceLevel level = ExperienceLevel.Expert, RecordingTakeDto? take = null, Action? onChanged = null)
    {
        await OpenWithInputAsync();
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(take ?? Take()));
        await Session.Takes.LoadAsync(TrackId);
        await Session.Takes.LoadIntoStripAsync(Session.Takes.Items[0]);
        return RenderAtLevel<TakeAdjust>(level, p => p.Add(c => c.Takes, Session.Takes).Add(c => c.OnChanged, () => onChanged?.Invoke()));
    }
}
