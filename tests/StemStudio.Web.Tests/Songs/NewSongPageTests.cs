using System.Net;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Songs;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Songs;

/// <summary>The song page (010 T031, US1, US5, FR-001, FR-002, FR-004, FR-014, FR-016; ui-test-ids 1.9.0 song-*).</summary>
public sealed class NewSongPageTests : PageTestContext
{
    public NewSongPageTests()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status());
        Api.CreateSongAsync(Arg.Any<SongRequest>(), Arg.Any<CancellationToken>()).Returns(SongData.Song(GenerationStateDto.Queued));
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([SongData.Sheet()]);
        Api.GetLyricsSheetAsync(SongData.SheetId, Arg.Any<CancellationToken>()).Returns(SongData.Sheet());
        Api.ProposeSectionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SongData.Proposal());
    }

    private SongRequest Sent() =>
        (SongRequest)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.CreateSongAsync)).GetArguments()[0]!;

    private static Task Choose(IElement select, string label) =>
        select.ChangeAsync(new() { Value = select.QuerySelectorAll("option").Single(o => o.TextContent == label).GetAttribute("value") });

    private IRenderedComponent<NewSongPage> RenderPage() => Render<NewSongPage>();

    [Fact]
    public void Page_HasTheEditorTheOptionsAndTheNotice()
    {
        var page = RenderPage();

        page.Find("h1").TextContent.ShouldBe("New song");
        page.Find("[data-help-id=song-options]").ShouldNotBeNull();
        page.Find("[data-testid=song-lyrics-text]").TagName.ShouldBe("TEXTAREA");
        page.Find("[data-testid=song-lyrics-text]").GetAttribute("data-help-id").ShouldBe("lyrics-format");
        page.Find("[data-testid=song-lyrics-count]").TextContent.ShouldBe("0 / 4096");
        page.Find("[data-testid=song-lyrics-select]").QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["New lyrics", "Paper boats"]);
        page.Find("[data-testid=song-lyrics-upload]").GetAttribute("accept").ShouldBe(".txt,.lrc,.md");
        page.Find("[data-testid=song-sections-propose]").GetAttribute("data-help-id").ShouldBe("lyrics-sections");
        page.Find("[data-testid=song-language-select]").GetAttribute("data-help-id").ShouldBe("song-language");
        page.Find("[data-testid=song-lead-voice]").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["female", "male", "any"]);
        var duration = page.Find("[data-testid=song-duration]");
        (duration.GetAttribute("type"), duration.GetAttribute("min"), duration.GetAttribute("max"), duration.GetAttribute("aria-valuenow"))
            .ShouldBe(("range", "10", "480", "120"));
        duration.GetAttribute("aria-valuetext").ShouldBe("2:00");
        var notice = page.Find("[data-testid=song-personal-use-notice]");
        foreach (var text in new[] { "personal use", "AI voice", "your own lyrics", "imitate" })
        {
            notice.TextContent.ShouldContain(text);
        }
    }

    [Fact]
    public void Languages_AreNamed_EnglishFirstSelected()
    {
        var page = RenderPage();

        var select = page.Find("[data-testid=song-language-select]");
        select.QuerySelectorAll("option").Length.ShouldBe(51);
        select.QuerySelector("option[value=en]")!.TextContent.ShouldBe("English");
        select.QuerySelector("option[value=nl]")!.TextContent.ShouldBe("Dutch");
        select.QuerySelector("option[value=unknown]")!.TextContent.ShouldBe("Other or unknown");
        select.QuerySelector("option[value=en]")!.HasAttribute("selected").ShouldBeTrue();
    }

    [Fact]
    public async Task TheCounter_AndTheDensityWarning_FollowTheText()
    {
        var page = RenderPage();

        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Words(41) });
        page.Find("[data-testid=song-lyrics-count]").TextContent.ShouldBe("122 / 4096");
        page.FindAll("[data-testid=song-density-warning]").ShouldBeEmpty();
        await page.Find("[data-testid=song-duration]").InputAsync(new() { Value = "10" });

        page.Find("[data-testid=song-density-warning]").TextContent.ShouldContain("4.1 words per second");
        page.Find("[data-testid=song-generate]").HasAttribute("disabled").ShouldBeFalse();
        page.Find("[data-testid=song-duration]").GetAttribute("aria-valuetext").ShouldBe("0:10");
    }

    [Fact]
    public async Task AddSections_ReplacesTheText()
    {
        var page = RenderPage();
        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Lyrics });

        await page.Find("[data-testid=song-sections-propose]").ClickAsync(new());

        await Api.Received(1).ProposeSectionsAsync(SongData.Lyrics, Arg.Any<CancellationToken>());
        page.Find("[data-testid=song-lyrics-text]").GetAttribute("value")!.ShouldStartWith("[verse]");
    }

    [Fact]
    public async Task Generate_SendsTheLyricsAndChoices_AndListsTheSong()
    {
        var page = RenderPage();

        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Lyrics });
        await page.Find("[data-testid=song-title-input]").InputAsync(new() { Value = "Paper boats" });
        await Choose(page.Find("[data-testid=song-language-select]"), "Dutch");
        await Choose(page.Find("[data-testid=song-style-select]"), "Synthwave");
        await page.Find("[data-testid=song-lead-voice]").ChangeAsync(new() { Value = "female" });
        await page.Find("[data-testid=song-duration]").InputAsync(new() { Value = "30" });
        await page.Find("[data-testid=song-generate]").ClickAsync(new());

        var request = Sent();
        (request.LyricsText, request.LyricsId, request.Title, request.Language, request.StyleId, request.LeadVoice, request.DurationSeconds, request.Takes, request.Instrumental)
            .ShouldBe((SongData.Lyrics, null, "Paper boats", "nl", GenerationData.SynthwaveId, LeadVoiceDto.Female, 30d, 1, false));
        page.Find($"[data-testid=song-generations] [data-testid=generation-row-{SongData.SongId}]").GetAttribute("data-kind").ShouldBe("song");
    }

    [Fact]
    public async Task AdvancedOptions_AreSent()
    {
        var page = RenderPage();

        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Lyrics });
        await Choose(page.Find("[data-testid=song-style-select]"), "Free description");
        await page.Find("[data-testid=song-free-caption]").InputAsync(new() { Value = "warm acoustic guitar, brushed drums" });
        await page.Find("[data-testid=song-delivery-input]").InputAsync(new() { Value = "breathy, soft" });
        await page.Find("[data-testid=song-instrumental]").ChangeAsync(new() { Value = true });
        await page.Find("[data-testid=song-bpm]").ChangeAsync(new() { Value = "96" });
        await Choose(page.Find("[data-testid=song-key]"), "E minor");
        await Choose(page.Find("[data-testid=song-time-signature]"), "3/4");
        await page.Find("[data-testid=song-takes]").ChangeAsync(new() { Value = "2" });
        await page.Find("[data-testid=song-generate]").ClickAsync(new());

        var request = Sent();
        (request.StyleId, request.FreeCaption, request.Delivery, request.Instrumental, request.Bpm, request.KeyScale, request.TimeSignature, request.Takes)
            .ShouldBe((null, "warm acoustic guitar, brushed drums", "breathy, soft", true, 96, "E minor", "3", 2));
    }

    [Fact]
    public async Task TheFreeCaption_IsOnlyOfferedForAFreeDescription()
    {
        var page = RenderPage();

        page.Find("[data-testid=song-free-caption]").HasAttribute("disabled").ShouldBeTrue();
        await Choose(page.Find("[data-testid=song-style-select]"), "Free description");
        page.Find("[data-testid=song-free-caption]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "Enter the lyrics first.")]
    [InlineData(null, "The lyrics can be at most 4096 characters.")]
    public async Task InvalidLyrics_AreRefusedBeforeSending(string? text, string message)
    {
        var page = RenderPage();
        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = text ?? SongData.TooLong() });

        await page.Find("[data-testid=song-generate]").ClickAsync(new());

        var error = page.Find("[data-testid=song-error]");
        (error.GetAttribute("data-field"), error.TextContent).ShouldBe(("lyricsText", message));
        await Api.DidNotReceive().CreateSongAsync(Arg.Any<SongRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnImitationRefusal_NamesTheField()
    {
        Api.CreateSongAsync(Arg.Any<SongRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(new ApiError(
            HttpStatusCode.BadRequest, "Styles cannot imitate an artist.", null,
            new Dictionary<string, string[]> { ["delivery"] = ["Describe the voice instead of naming a singer."] })));
        var page = RenderPage();
        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Lyrics });

        await page.Find("[data-testid=song-generate]").ClickAsync(new());

        var error = page.Find("[data-testid=song-error]");
        (error.GetAttribute("data-field"), error.TextContent).ShouldBe(("delivery", "Describe the voice instead of naming a singer."));
    }

    [Fact]
    public void Generate_IsDisabledWithTheReason_WhenNotReady()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.NotInstalled());

        var page = RenderPage();

        var button = page.Find("[data-testid=song-generate]");
        (button.HasAttribute("disabled"), button.GetAttribute("aria-describedby")).ShouldBe((true, "generation-status-reason"));
        page.Find("#generation-status-reason").TextContent.ShouldBe("The generator service is not installed.");
    }

    [Fact]
    public void Beginner_SeesTheEssentials_ButNotTheExpertOptions()
    {
        var page = RenderAtLevel<NewSongPage>(ExperienceLevel.Beginner, _ => { });

        foreach (var id in new[] { "song-lyrics-text", "song-sections-propose", "song-style-select", "song-lead-voice", "song-instrumental", "song-duration", "song-generate", "song-personal-use-notice" })
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }

        foreach (var id in new[] { "song-free-caption", "song-delivery-input", "song-bpm", "song-key", "song-time-signature", "song-takes" })
        {
            page.FindAll($"[data-testid={id}]").ShouldBeEmpty(id);
        }

        page.Find("[data-testid=song-style-select]").QuerySelectorAll("option").Select(o => o.TextContent).ShouldNotContain("Free description");
    }

    [Fact]
    public async Task ASavedSheet_CanBeChosen_AndIsSentById()
    {
        var page = RenderPage();

        await page.Find("[data-testid=song-lyrics-select]").ChangeAsync(new() { Value = SongData.SheetId.ToString() });
        page.Find("[data-testid=song-lyrics-text]").GetAttribute("value").ShouldBe(SongData.Lyrics);
        page.Find("[data-testid=song-title-input]").GetAttribute("value").ShouldBe("Paper boats");
        await page.Find("[data-testid=song-generate]").ClickAsync(new());

        (Sent().LyricsId, Sent().LyricsText).ShouldBe((SongData.SheetId, null));
    }

    [Fact]
    public async Task AnEditedSheet_IsSentAsText()
    {
        var page = RenderPage();

        await page.Find("[data-testid=song-lyrics-select]").ChangeAsync(new() { Value = SongData.SheetId.ToString() });
        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Lyrics + "\nOne more line" });
        await page.Find("[data-testid=song-generate]").ClickAsync(new());

        (Sent().LyricsId, Sent().LyricsText).ShouldBe((null, SongData.Lyrics + "\nOne more line"));
    }

    [Fact]
    public void LyricsIdInTheAddress_PreselectsTheSheet()
    {
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo($"songs/new?lyricsId={SongData.SheetId}");

        var page = RenderPage();

        page.Find("[data-testid=song-lyrics-text]").GetAttribute("value").ShouldBe(SongData.Lyrics);
    }

    [Fact]
    public async Task Save_CreatesASheet_ThenUpdatesIt()
    {
        Api.CreateLyricsSheetAsync(Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(SongData.Sheet());
        Api.UpdateLyricsSheetAsync(SongData.SheetId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(SongData.Sheet());
        var page = RenderPage();
        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Lyrics });

        await page.Find("[data-testid=song-lyrics-save]").ClickAsync(new());
        await page.Find("[data-testid=song-lyrics-save]").ClickAsync(new());

        await Api.Received(1).CreateLyricsSheetAsync(Arg.Is<LyricsInput>(i => i.Title == "Untitled lyrics" && i.Language == "en"), Arg.Any<CancellationToken>());
        await Api.Received(1).UpdateLyricsSheetAsync(SongData.SheetId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>());
        page.Find("[data-testid=song-lyrics-saved]").TextContent.ShouldContain("Saved");
    }

    [Fact]
    public void Upload_SavesTheFileAsASheet_AndShowsTheNormalisedText()
    {
        var uploaded = SongData.Sheet("sample") with { Id = Guid.NewGuid(), Text = "Paper boats along the gutter" };
        Api.UploadLyricsSheetAsync(Arg.Any<UploadFile>(), Arg.Any<string?>(), "en", Arg.Any<CancellationToken>()).Returns(uploaded);
        var page = RenderPage();

        page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromText("[00:01.00]Paper boats along the gutter", "sample.lrc"));

        page.WaitForAssertion(() => page.Find("[data-testid=song-lyrics-text]").GetAttribute("value").ShouldBe("Paper boats along the gutter"));
        page.Find("[data-testid=song-lyrics-select]").QuerySelectorAll("option").Select(o => o.TextContent).ShouldContain("sample");
    }

    [Theory]
    [InlineData("notes.pdf", 10, "Choose a .txt, .lrc or .md file.")]
    [InlineData("big.txt", 70_000, "Lyrics files can be at most 64 KB.")]
    public void Upload_RefusesOtherFilesBeforeSending(string name, int size, string message)
    {
        var page = RenderPage();

        page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromText(new string('a', size), name));

        page.WaitForAssertion(() => page.Find("[data-testid=song-error]").TextContent.ShouldBe(message));
        Api.DidNotReceive().UploadLyricsSheetAsync(Arg.Any<UploadFile>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApiFailures_OfTheHelpers_AreShown()
    {
        Api.ProposeSectionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        var page = RenderPage();
        await page.Find("[data-testid=song-lyrics-text]").InputAsync(new() { Value = SongData.Lyrics });

        await page.Find("[data-testid=song-sections-propose]").ClickAsync(new());

        page.Find("[data-testid=song-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
    }

    [Fact]
    public void SongTakes_ShowTheVocalBadge_AndTheLyricsInTheDetails()
    {
        Api.ListGenerationsAsync(new GenerationQuery(GenerationKindDto.Song), Arg.Any<CancellationToken>())
            .Returns([SongData.Song(), SongData.Song(instrumental: true) with { Id = GenerationData.OtherGenerationId }]);

        var page = RenderPage();

        var badge = page.Find($"[data-testid=take-vocal-badge-{SongData.SongId}-1]");
        (badge.GetAttribute("data-vocal"), badge.TextContent).ShouldBe(("synthetic", "AI voice"));
        var none = page.Find($"[data-testid=take-vocal-badge-{GenerationData.OtherGenerationId}-1]");
        (none.GetAttribute("data-vocal"), none.TextContent).ShouldBe(("none", "No vocals"));
        page.Find($"[data-testid=generation-row-{SongData.SongId}]").TextContent.ShouldContain("Paper boats");
        var details = page.Find($"[data-testid=take-details-{SongData.SongId}-1]").TextContent;
        foreach (var text in new[] { "Paper boats along the gutter", "English", "Jazz trio", "Male", "breathy, soft", "96 BPM", "E minor", "4/4", "0:30" })
        {
            details.ShouldContain(text);
        }
    }
}
