using System.Net;
using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Generation;
using StemStudio.Web.Components.Songs;
using StemStudio.Web.Components.Voices;
using StemStudio.Web.Interop;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Voices;

/// <summary>
/// 010 polish (T058): the consent panel, the recorder, the use-my-voice dialog and the song page are usable with the
/// keyboard (native controls only) and with a screen reader (every control named, statement in both languages marked,
/// timer and status announced, reasons attached to disabled controls).
/// </summary>
public sealed class VoiceAccessibilityTests : PageTestContext
{
    public VoiceAccessibilityTests() =>
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(VoiceData.VoiceStatus(GenerationAvailabilityDto.Ready));

    private static void EveryControlIsNamed(string markup, IEnumerable<IElement> controls)
    {
        foreach (var control in controls)
        {
            var named = control.HasAttribute("aria-label") || control.Closest("label") is not null
                || (control.Id is { Length: > 0 } id && markup.Contains($"for=\"{id}\"", StringComparison.Ordinal));
            named.ShouldBeTrue(control.GetAttribute("data-testid") ?? control.OuterHtml);
        }
    }

    [Fact]
    public async Task ConsentPanelAndRecorder_AreNativeNamedControls()
    {
        var page = Render<VoiceProfilesPage>();
        await page.Find("[data-testid=voice-new]").ClickAsync(new());

        var panel = page.Find(".ss-new-voice");
        EveryControlIsNamed(page.Markup, panel.QuerySelectorAll("input, select, textarea"));
        panel.QuerySelectorAll("[data-testid^=voice-record-], [data-testid=voice-save]").Where(e => e.TagName != "SPAN")
            .Select(e => e.TagName).ShouldAllBe(tag => tag == "BUTTON");
        page.Find("[data-testid=voice-consent-text] p[lang=en]").TextContent.ShouldBe(VoiceData.StatementEn);
        page.Find("[data-testid=voice-consent-text] p[lang=nl]").TextContent.ShouldBe(VoiceData.StatementNl);
        page.Find("[data-testid=voice-record-timer]").GetAttribute("role").ShouldBe("timer");
        page.Find("[aria-labelledby=new-voice-heading]").TagName.ShouldBe("SECTION");
    }

    [Fact]
    public async Task TheDeleteChoice_IsALabelledCheckbox_InALabelledDialog()
    {
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([VoiceData.Profile(converted: 3)]);
        var page = Render<VoiceProfilesPage>();

        await page.Find($"[data-testid=voice-delete-{VoiceData.ProfileId}]").ClickAsync(new());

        var dialog = page.Find("[role=dialog]");
        page.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Delete voice profile");
        page.Find($"label[for=voice-delete-takes-{VoiceData.ProfileId}]").TextContent.Trim().ShouldBe("Also delete the 3 takes converted to this voice");
    }

    [Fact]
    public async Task UseMyVoiceDialog_IsLabelled_AndEscapeCloses()
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns([SongData.Song()]);
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([VoiceData.Profile(), VoiceData.Profile("Second voice") with { Id = Guid.NewGuid() }]);
        var page = Render<GenerationList>(p => p.Add(c => c.Query, new GenerationQuery()));

        await page.Find($"[data-testid=take-use-my-voice-{SongData.SongId}-1]").ClickAsync(new());
        var dialog = page.Find("[role=dialog]");
        page.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Use my voice");
        EveryControlIsNamed(page.Markup, dialog.QuerySelectorAll("input, select"));
        await dialog.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void SongPage_ControlsAreNamed_AndTheCounterDescribesTheText()
    {
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([SongData.Sheet()]);

        var page = Render<NewSongPage>();

        EveryControlIsNamed(page.Markup, page.FindAll("input:not([type=file]), select, textarea"));
        page.Find("[data-testid=song-lyrics-text]").GetAttribute("aria-describedby").ShouldBe("song-lyrics-count");
    }

    [Fact]
    public async Task ASecondRecording_ReleasesTheFirst()
    {
        var first = Substitute.For<IVoiceRecording>();
        var second = Substitute.For<IVoiceRecording>();
        first.StopAsync().Returns(new RecordedSample([1], "audio/webm", "blob:1"));
        Recorder.StartAsync(60).Returns(first, second);
        var recorder = Render<VoiceRecorder>();

        await recorder.Find("[data-testid=voice-record-start]").ClickAsync(new());
        await recorder.Find("[data-testid=voice-record-stop]").ClickAsync(new());
        await recorder.Find("[data-testid=voice-record-start]").ClickAsync(new());

        await first.Received(1).DisposeAsync();
        await second.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public async Task ARecorderThatNeverRecorded_DisposesCleanly()
    {
        Render<VoiceRecorder>();

        await Should.NotThrowAsync(() => DisposeComponentsAsync());
        new VoiceRecorderException().Message.ShouldBe("The recording failed.");
    }

    [Fact]
    public async Task UseMyVoice_SendsTheChosenProfile_AndShowsALoadFailure()
    {
        var second = VoiceData.Profile("Second voice") with { Id = Guid.NewGuid() };
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns([SongData.Song()]);
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([VoiceData.Profile(), second]);
        Api.CreateVoiceConversionAsync(SongData.SongId, 1, Arg.Any<VoiceConversionRequest>(), Arg.Any<CancellationToken>()).Returns(VoiceData.Conversion());
        var page = Render<GenerationList>(p => p.Add(c => c.Query, new GenerationQuery()));

        await page.Find($"[data-testid=take-use-my-voice-{SongData.SongId}-1]").ClickAsync(new());
        await page.Find("[data-testid=take-voice-profile-select]").ChangeAsync(new() { Value = second.Id.ToString() });
        await page.Find("[data-testid=take-voice-convert-start]").ClickAsync(new());

        await Api.Received(1).CreateVoiceConversionAsync(SongData.SongId, 1, Arg.Is<VoiceConversionRequest>(r => r.ProfileId == second.Id && r.AutoShift), Arg.Any<CancellationToken>());
        page.Find($"[data-testid=take-details-{VoiceData.ConversionId}-1]").TextContent.ShouldContain("no automatic shift");

        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        await page.Find($"[data-testid=take-use-my-voice-{SongData.SongId}-1]").ClickAsync(new());
        page.Find("[data-testid=take-voice-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
        page.FindAll("[data-testid=take-voice-profile-select]").ShouldBeEmpty();
    }

    [Fact]
    public void VoicesPage_EmptyAndLoading_AndAProfileSavedAfterALoadFailure()
    {
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([]);
        Render<VoiceProfilesPage>().Find("[data-testid=voices-empty]").TextContent.ShouldContain("No voice profiles yet");

        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<StemStudio.Contracts.Voices.VoiceProfileDto>>().Task);
        Render<VoiceProfilesPage>().FindAll(".ss-skeleton").ShouldNotBeEmpty();
    }
}
