using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Voices;
using StemStudio.Web.Interop;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Voices;

/// <summary>
/// Voice profiles with recording, upload and consent (010 T050, US3, FR-005, FR-006, FR-009, FR-014; ui-test-ids 1.9.0
/// voice-*). Ethics: no profile without the ticked, versioned statement and the typed name; own voice only.
/// </summary>
public sealed class VoiceProfilesTests : PageTestContext
{
    private readonly IVoiceRecording _recording = Substitute.For<IVoiceRecording>();

    public VoiceProfilesTests()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(VoiceData.VoiceStatus(GenerationAvailabilityDto.Ready));
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([VoiceData.Profile()]);
        Api.CreateVoiceProfileAsync(Arg.Any<VoiceProfileUpload>(), Arg.Any<CancellationToken>()).Returns(VoiceData.Profile("Studio voice", 0) with { Id = Guid.NewGuid() });
        Recorder.StartAsync(60).Returns(_recording);
        _recording.StopAsync().Returns(new RecordedSample([1, 2, 3], "audio/webm;codecs=opus", "blob:recording"));
    }

    private VoiceProfileUpload SentUpload() =>
        (VoiceProfileUpload)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.CreateVoiceProfileAsync)).GetArguments()[0]!;

    private async Task<IRenderedComponent<VoiceProfilesPage>> OpenNewAsync()
    {
        var page = Render<VoiceProfilesPage>();
        await page.Find("[data-testid=voice-new]").ClickAsync(new());
        return page;
    }

    private static async Task FillConsentAsync(IRenderedComponent<VoiceProfilesPage> page, bool tick = true)
    {
        await page.Find("[data-testid=voice-display-name]").InputAsync(new() { Value = "Studio voice" });
        await page.Find("[data-testid=voice-person-name]").InputAsync(new() { Value = "Sam Example" });
        await page.Find("[data-testid=voice-consent-checkbox]").ChangeAsync(new() { Value = tick });
    }

    private async Task RecordAsync(IRenderedComponent<VoiceProfilesPage> page)
    {
        await page.Find("[data-testid=voice-record-start]").ClickAsync(new());
        await page.Find("[data-testid=voice-record-stop]").ClickAsync(new());
    }

    [Fact]
    public void Page_ListsTheProfiles_WithSampleAndConsentRecord()
    {
        var page = Render<VoiceProfilesPage>();

        page.Find("h1").TextContent.ShouldBe("Voice profiles");
        page.Find("[data-help-id=voice-profiles]").ShouldNotBeNull();
        var row = page.Find($"[data-testid=voice-row-{VoiceData.ProfileId}]");
        row.TextContent.ShouldContain("My voice");
        row.TextContent.ShouldContain("Recorded");
        var player = page.Find($"[data-testid=voice-play-{VoiceData.ProfileId}]");
        (player.TagName, player.GetAttribute("src")).ShouldBe(("AUDIO", $"http://localhost:8081/api/voice-profiles/{VoiceData.ProfileId}/sample"));
        var consent = page.Find($"[data-testid=voice-consent-{VoiceData.ProfileId}]").TextContent;
        foreach (var text in new[] { "consent.v1", "Sam Example", "14 Nov 2026" })
        {
            consent.ShouldContain(text);
        }

        page.Find("[data-testid=generation-status]").GetAttribute("data-voice-state").ShouldBe("ready");
    }

    [Fact]
    public async Task ConsentPanel_ShowsTheVersionedStatement_InEnglishAndDutch()
    {
        var page = await OpenNewAsync();

        var statement = page.Find("[data-testid=voice-consent-text]");
        statement.GetAttribute("data-version").ShouldBe("consent.v1");
        statement.GetAttribute("data-help-id").ShouldBe("voice-consent");
        statement.TextContent.ShouldContain(VoiceData.StatementEn);
        statement.TextContent.ShouldContain(VoiceData.StatementNl);
        page.Find("[data-testid=voice-consent-checkbox]").GetAttribute("aria-describedby").ShouldBe(statement.Id);
        page.Find("[data-testid=voice-person-name]").GetAttribute("maxlength").ShouldBe("80");
        page.Find("[data-testid=voice-display-name]").GetAttribute("maxlength").ShouldBe("60");
    }

    [Fact]
    public async Task Save_StaysDisabled_UntilSampleTickedStatementAndBothNames()
    {
        var page = await OpenNewAsync();
        bool Disabled() => page.Find("[data-testid=voice-save]").HasAttribute("disabled");

        Disabled().ShouldBeTrue();
        await FillConsentAsync(page);
        Disabled().ShouldBeTrue();
        await RecordAsync(page);
        Disabled().ShouldBeFalse();
        await page.Find("[data-testid=voice-consent-checkbox]").ChangeAsync(new() { Value = false });
        Disabled().ShouldBeTrue();
        await page.Find("[data-testid=voice-consent-checkbox]").ChangeAsync(new() { Value = true });
        await page.Find("[data-testid=voice-person-name]").InputAsync(new() { Value = "  " });
        Disabled().ShouldBeTrue();
    }

    [Fact]
    public async Task ARecordedProfile_IsSavedWithItsConsent_AndListed()
    {
        var page = await OpenNewAsync();

        await RecordAsync(page);
        page.Find("[data-testid=voice-preview]").GetAttribute("src").ShouldBe("blob:recording");
        await FillConsentAsync(page);
        await page.Find("[data-testid=voice-save]").ClickAsync(new());

        var upload = SentUpload();
        (upload.DisplayName, upload.PersonName, upload.ConsentConfirmed, upload.ConsentStatementVersion, upload.Source)
            .ShouldBe(("Studio voice", "Sam Example", true, "consent.v1", VoiceSampleSourceDto.Recorded));
        (upload.Sample.FileName, upload.Sample.ContentType).ShouldBe(("recording.webm", "audio/webm;codecs=opus"));
        page.FindAll("[data-testid=voice-consent-text]").ShouldBeEmpty();
        page.FindAll("[data-testid^=voice-row-]").Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnUploadedSample_IsPreviewed_AndSavedAsUploaded()
    {
        Recorder.FileUrlAsync(Arg.Any<Microsoft.AspNetCore.Components.ElementReference>()).Returns("blob:file");
        var page = await OpenNewAsync();

        page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[4096], "own-voice.wav", contentType: "audio/wav"));
        page.WaitForAssertion(() => page.Find("[data-testid=voice-preview]").GetAttribute("src").ShouldBe("blob:file"));
        await FillConsentAsync(page);
        await page.Find("[data-testid=voice-save]").ClickAsync(new());

        var upload = SentUpload();
        (upload.Sample.FileName, upload.Sample.ContentType, upload.Source).ShouldBe(("own-voice.wav", "audio/wav", VoiceSampleSourceDto.Uploaded));
        page.Find("[data-testid=voice-upload]").GetAttribute("accept").ShouldBe(".wav,.flac,.mp3,.webm,audio/*");
    }

    [Theory]
    [InlineData("notes.txt", 100, "Choose a WAV, FLAC, MP3 or WebM recording.")]
    [InlineData("long.wav", 21 * 1024 * 1024, "A recording can be at most 20 MB.")]
    public async Task UnsuitableFiles_AreRefusedBeforeUploading(string name, int size, string message)
    {
        var page = await OpenNewAsync();

        page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[size], name));

        page.WaitForAssertion(() => page.Find("[data-testid=voice-error]").TextContent.ShouldBe(message));
        page.FindAll("[data-testid=voice-preview]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Not enough singing", "The recording contains too little singing (12 % voiced, at least 50 % is needed).")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Not an own recording", "This file is audio from your StemStudio library. Record or upload your own voice instead.")]
    [InlineData(HttpStatusCode.Conflict, "Voice name taken", "A voice profile named \"Studio voice\" already exists.")]
    public async Task Refusals_AreShownWithTheReason_AndNothingIsListed(HttpStatusCode status, string title, string detail)
    {
        Api.CreateVoiceProfileAsync(Arg.Any<VoiceProfileUpload>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(status, title, detail));
        var page = await OpenNewAsync();

        await RecordAsync(page);
        await FillConsentAsync(page);
        await page.Find("[data-testid=voice-save]").ClickAsync(new());

        page.Find("[data-testid=voice-error]").TextContent.ShouldBe(detail);
        page.FindAll("[data-testid^=voice-row-]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_AsksWhetherTheConvertedTakesGoToo()
    {
        var page = Render<VoiceProfilesPage>();

        await page.Find($"[data-testid=voice-delete-{VoiceData.ProfileId}]").ClickAsync(new());
        var dialog = page.Find("[role=dialog]");
        dialog.TextContent.ShouldContain("sample");
        var takes = page.Find($"[data-testid=voice-delete-takes-{VoiceData.ProfileId}]");
        (takes.GetAttribute("type"), takes.HasAttribute("checked")).ShouldBe(("checkbox", false));
        page.Find($"label[for={takes.Id}]").TextContent.ShouldContain("1 take converted");
        await takes.ChangeAsync(new() { Value = true });
        await page.Find("[data-testid=voice-delete-confirm]").ClickAsync(new());

        await Api.Received(1).DeleteVoiceProfileAsync(VoiceData.ProfileId, true, Arg.Any<CancellationToken>());
        page.FindAll($"[data-testid=voice-row-{VoiceData.ProfileId}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_KeepsTheTakesByDefault_AndShowsFailures()
    {
        Api.DeleteVoiceProfileAsync(VoiceData.ProfileId, false, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.NotFound, "Voice profile not found"));
        var page = Render<VoiceProfilesPage>();

        await page.Find($"[data-testid=voice-delete-{VoiceData.ProfileId}]").ClickAsync(new());
        await page.Find("[data-testid=voice-delete-confirm]").ClickAsync(new());

        page.Find("[data-testid=voices-action-error]").TextContent.ShouldBe("Voice profile not found");
    }

    [Fact]
    public async Task WithoutTheSinger_SavingIsDisabledWithTheReason_ButRecordingWorks()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(VoiceData.VoiceStatus(GenerationAvailabilityDto.NotInstalled, "The singer service is not installed."));
        var page = await OpenNewAsync();

        await RecordAsync(page);
        await FillConsentAsync(page);

        var save = page.Find("[data-testid=voice-save]");
        (save.HasAttribute("disabled"), save.GetAttribute("aria-describedby")).ShouldBe((true, "generation-voice-reason"));
        page.Find("#generation-voice-reason").TextContent.ShouldBe("The singer service is not installed.");
    }

    [Fact]
    public void LoadFailures_AreShown()
    {
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));

        Render<VoiceProfilesPage>().Find("[data-testid=voices-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
    }

    [Fact]
    public async Task AMissingStatement_BlocksTheProfile()
    {
        Api.GetVoiceConsentAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        var page = await OpenNewAsync();

        page.Find("[data-testid=voice-error]").TextContent.ShouldContain("consent statement could not be loaded");
        page.FindAll("[data-testid=voice-consent-checkbox]").ShouldBeEmpty();
        page.Find("[data-testid=voice-save]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Navigation_LinksToVoices()
    {
        Render<StemStudio.Web.Components.Layout.NavMenu>().Find("[data-testid=nav-voices]").GetAttribute("href").ShouldBe("voices");
    }
}
