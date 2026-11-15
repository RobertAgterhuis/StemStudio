using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Generation;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Voices;

/// <summary>"Use my voice" on song takes with an AI voice (010 T050, US3, FR-008, FR-011; ui-test-ids 1.9.0 take-voice-*).</summary>
public sealed class UseMyVoiceTests : PageTestContext
{
    private static readonly Guid Song = SongData.SongId;

    public UseMyVoiceTests()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(VoiceData.VoiceStatus(GenerationAvailabilityDto.Ready));
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([VoiceData.Profile()]);
        Api.CreateVoiceConversionAsync(Song, 1, Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>())
            .Returns(VoiceData.Conversion() with { Status = GenerationStateDto.WaitingForStems, Takes = [] });
    }

    private IRenderedComponent<GenerationList> RenderList(params GenerationDto[] generations)
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns(generations);
        return Render<GenerationList>(p => p.Add(c => c.Query, new GenerationQuery()));
    }

    [Fact]
    public void OnlySongTakesWithAnAiVoice_OfferUseMyVoice()
    {
        var page = RenderList(SongData.Song(), SongData.Song(instrumental: true) with { Id = GenerationData.OtherGenerationId }, GenerationData.Generation(), VoiceData.Conversion());

        page.FindAll("[data-testid^=take-use-my-voice-]").Select(b => b.GetAttribute("data-testid")).ShouldBe([$"take-use-my-voice-{Song}-1"]);
    }

    [Fact]
    public async Task Convert_SendsTheProfile_ClosesTheDialog_AndListsTheConversion()
    {
        var page = RenderList(SongData.Song());

        await page.Find($"[data-testid=take-use-my-voice-{Song}-1]").ClickAsync(new());
        var dialog = page.Find("[role=dialog]");
        dialog.GetAttribute("data-help-id").ShouldBe("voice-conversion");
        page.Find("[data-testid=take-voice-profile-select]").QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["My voice"]);
        await page.Find("[data-testid=take-voice-pitch-shift]").ChangeAsync(new() { Value = "-2" });
        await page.Find("[data-testid=take-voice-auto-shift]").ChangeAsync(new() { Value = false });
        await page.Find("[data-testid=take-voice-convert-start]").ClickAsync(new());

        await Api.Received(1).CreateVoiceConversionAsync(Song, 1, new VoiceConversionRequest { ProfileId = VoiceData.ProfileId, PitchShift = -2, AutoShift = false }, Arg.Any<CancellationToken>());
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        page.Find($"[data-testid=generation-row-{VoiceData.ConversionId}]").GetAttribute("data-status").ShouldBe("waitingForStems");
        page.Find($"[data-testid=generation-stage-{VoiceData.ConversionId}]").TextContent.ShouldBe("Preparing the stems");
    }

    [Fact]
    public async Task WithoutProfiles_TheDialogLinksToTheVoicesPage()
    {
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([]);
        var page = RenderList(SongData.Song());

        await page.Find($"[data-testid=take-use-my-voice-{Song}-1]").ClickAsync(new());

        page.Find("[data-testid=take-voice-profile-select]").GetAttribute("href").ShouldBe("voices");
        page.Find("[data-testid=take-voice-convert-start]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task WithoutAReadySinger_ConvertIsDisabledWithTheReason()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(VoiceData.VoiceStatus(GenerationAvailabilityDto.Unavailable, "No NVIDIA GPU is available to the singer."));
        var page = RenderList(SongData.Song());

        await page.Find($"[data-testid=take-use-my-voice-{Song}-1]").ClickAsync(new());

        var start = page.Find("[data-testid=take-voice-convert-start]");
        start.HasAttribute("disabled").ShouldBeTrue();
        page.Find($"#{start.GetAttribute("aria-describedby")}").TextContent.ShouldBe("No NVIDIA GPU is available to the singer.");
    }

    [Fact]
    public async Task ARefusal_IsShownInTheDialog_AndCancelCloses()
    {
        Api.CreateVoiceConversionAsync(Song, 1, Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "No AI vocal to convert", "Only completed song takes with an AI voice can be converted."));
        var page = RenderList(SongData.Song());

        await page.Find($"[data-testid=take-use-my-voice-{Song}-1]").ClickAsync(new());
        await page.Find("[data-testid=take-voice-convert-start]").ClickAsync(new());
        page.Find("[data-testid=take-voice-error]").TextContent.ShouldBe("Only completed song takes with an AI voice can be converted.");
        await page.Find("[data-testid=take-voice-convert-cancel]").ClickAsync(new());

        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Beginner_DoesNotSeeThePitchOptions()
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns([SongData.Song()]);
        var page = RenderAtLevel<GenerationList>(ExperienceLevel.Beginner, p => p.Add(c => c.Query, new GenerationQuery()));

        await page.Find($"[data-testid=take-use-my-voice-{Song}-1]").ClickAsync(new());

        page.FindAll("[data-testid=take-voice-pitch-shift]").ShouldBeEmpty();
        page.FindAll("[data-testid=take-voice-auto-shift]").ShouldBeEmpty();
        page.FindAll("[data-testid=take-voice-profile-select]").Count.ShouldBe(1);
    }

    [Fact]
    public void ConvertedTakes_ShowTheProfile_AndPlayTheConvertedVocal()
    {
        var page = RenderList(VoiceData.Conversion());

        var badge = page.Find($"[data-testid=take-vocal-badge-{VoiceData.ConversionId}-1]");
        (badge.GetAttribute("data-vocal"), badge.TextContent).ShouldBe(("converted", "AI voice: My voice"));
        var vocal = page.Find($"[data-testid=take-vocal-play-{VoiceData.ConversionId}-1]");
        (vocal.TagName, vocal.GetAttribute("src")).ShouldBe(("AUDIO", $"http://localhost:8081/api/generations/{VoiceData.ConversionId}/takes/1/vocal"));
        page.Find($"[data-testid=generation-row-{VoiceData.ConversionId}]").TextContent.ShouldContain("In the voice My voice");
        page.Find($"[data-testid=take-details-{VoiceData.ConversionId}-1]").TextContent.ShouldContain("-2 semitones");
    }
}
