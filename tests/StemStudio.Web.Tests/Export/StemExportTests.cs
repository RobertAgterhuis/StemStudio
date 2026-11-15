using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Export;

/// <summary>Raw stem export from the track page (007 T037, FR-001, FR-014, ui-test-ids 1.5.0).</summary>
public sealed class StemExportTests : PageTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, TrackDto? track = null)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(track ?? TestData.Track());
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<TrackDetail>(d => d.Add(x => x.Id, TestData.TrackId)));
    }

    private static async Task OpenAsync(IRenderedComponent<CascadingValue<ExperienceContext>> page, string trigger = "export-stem-bass") =>
        await page.Find($"[data-testid={trigger}]").ClickAsync(new());

    private void ExportReturns(AssetDto asset) =>
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns(asset);

    [Fact]
    public void EveryStandardStem_HasAnExportButton()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        foreach (var stem in StemStudio.Contracts.StemNames.SixStem)
        {
            var button = page.Find($"[data-testid=export-stem-{stem}]");
            button.TagName.ShouldBe("BUTTON");
            button.GetAttribute("aria-label").ShouldBe($"Save {stem} to the library");
        }
    }

    [Fact]
    public void DetailButtons_ExistOnlyForKeptDetailStemsOfSplitParents()
    {
        var page = RenderAt(ExperienceLevel.Advanced, TestData.DetailTrack());

        foreach (var key in new[] { "drums-kick", "drums-snare", "drums-rest", "vocals-lead", "vocals-backing", "vocals-rest" })
        {
            page.FindAll($"[data-testid=export-detail-{key}]").Count.ShouldBe(1, key);
        }

        page.FindAll("[data-testid^=export-detail-bass-], [data-testid^=export-detail-guitar-], [data-testid^=export-detail-other-]").ShouldBeEmpty();
    }

    [Fact]
    public void ProcessingTrack_HasNoExportButtons() =>
        RenderAt(ExperienceLevel.Advanced, TestData.Track(TrackStatusDto.Separating)).FindAll("[data-testid^=export-]").ShouldBeEmpty();

    [Fact]
    public async Task Dialog_ForAStem_PreselectsFlac_AndCarriesItsHelpMarkers()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        await OpenAsync(page);

        var dialog = page.Find("[data-testid=export-dialog]");
        dialog.GetAttribute("data-mode").ShouldBe("stem");
        dialog.GetAttribute("role").ShouldBe("dialog");
        dialog.GetAttribute("aria-modal").ShouldBe("true");
        dialog.GetAttribute("data-help-id").ShouldBe("export-dialog");
        var format = page.Find("[data-testid=export-format]");
        format.TagName.ShouldBe("SELECT");
        format.GetAttribute("value").ShouldBe("flac");
        format.GetAttribute("data-help-id").ShouldBe("export-formats");
        dialog.TextContent.ShouldContain("bass");
        page.FindAll("[data-testid=export-source-audible]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Beginner_SeesOnlyTheFormat()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        await OpenAsync(page);

        page.FindAll("[data-testid=export-format]").Count.ShouldBe(1);
        page.FindAll("[data-testid=export-bit-depth], [data-testid=export-mp3-bitrate], [data-testid=export-range-start]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("flac", true, false)]
    [InlineData("wav", true, false)]
    [InlineData("mp3", false, true)]
    public async Task Advanced_ShowsTheDetailOfTheChosenFormat(string format, bool bitDepth, bool bitrate)
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);

        await page.Find("[data-testid=export-format]").ChangeAsync(new ChangeEventArgs { Value = format });

        (page.FindAll("[data-testid=export-bit-depth]").Count == 1).ShouldBe(bitDepth);
        (page.FindAll("[data-testid=export-mp3-bitrate]").Count == 1).ShouldBe(bitrate);
    }

    [Fact]
    public async Task Submit_SendsTheRawRequest_AndConfirmsWithALinkToTheItem()
    {
        ExportReturns(AssetData.Asset(status: AssetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Beginner);
        await OpenAsync(page);

        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        await Api.Received(1).CreateAudioExportAsync(
            Arg.Is<CreateAudioExportRequest>(r => r.TrackId == TestData.TrackId && r.Mode == ExportModeDto.Raw && r.Channels!.SequenceEqual(new[] { "bass" })
                && r.Format == new ExportFormatDto(AudioFileFormatDto.Flac, null, null) && r.Name == null && r.Range == null && r.Mix == null),
            Arg.Any<CancellationToken>());
        var saved = page.Find("[data-testid=export-saved]");
        saved.TextContent.ShouldContain("Never Gonna Give You Up - bass");
        page.Find("[data-testid=export-saved-open]").GetAttribute("href").ShouldBe($"library?asset={AssetData.AssetId}");
        page.FindAll("[data-testid=export-submit]").ShouldBeEmpty();
    }

    [Fact]
    public async Task ChosenFormatAndName_AreSent()
    {
        ExportReturns(AssetData.Asset(status: AssetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page, "export-stem-drums");

        await page.Find("[data-testid=export-format]").ChangeAsync(new ChangeEventArgs { Value = "mp3" });
        await page.Find("[data-testid=export-mp3-bitrate]").ChangeAsync(new ChangeEventArgs { Value = "192" });
        page.Find("[data-testid=export-name]").Input("  Drums only ");
        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        await Api.Received(1).CreateAudioExportAsync(
            Arg.Is<CreateAudioExportRequest>(r => r.Channels!.SequenceEqual(new[] { "drums" }) && r.Format == new ExportFormatDto(AudioFileFormatDto.Mp3, null, 192) && r.Name == "Drums only"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BitDepth_IsSentForFlac()
    {
        ExportReturns(AssetData.Asset(status: AssetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);

        await page.Find("[data-testid=export-bit-depth]").ChangeAsync(new ChangeEventArgs { Value = "24" });
        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        await Api.Received(1).CreateAudioExportAsync(Arg.Is<CreateAudioExportRequest>(r => r.Format == new ExportFormatDto(AudioFileFormatDto.Flac, 24, null)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetailStem_ExportsItsChannelId()
    {
        ExportReturns(AssetData.Asset(status: AssetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Advanced, TestData.DetailTrack());
        await OpenAsync(page, "export-detail-drums-snare");

        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        await Api.Received(1).CreateAudioExportAsync(Arg.Is<CreateAudioExportRequest>(r => r.Channels!.SequenceEqual(new[] { "drums.snare" })), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "Less than 1 GiB of free disk space would remain.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "The processing service is unavailable.")]
    public async Task Refusal_ShowsTheProblemDetail_AndKeepsTheForm(HttpStatusCode status, string detail)
    {
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns<AssetDto>(_ => throw Problem(status, "Refused", detail));
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);

        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        page.Find("[data-testid=export-error]").TextContent.ShouldBe(detail);
        page.FindAll("[data-testid=export-submit]").Count.ShouldBe(1);
        page.FindAll("[data-testid=export-saved]").ShouldBeEmpty();
    }

    [Fact]
    public async Task TooLongName_IsRefusedBeforeTheRequest()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);

        page.Find("[data-testid=export-name]").Input(new string('x', 121));
        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        page.Find("[data-testid=export-error]").TextContent.ShouldBe("Enter a name of at most 120 characters, or leave it empty.");
        await Api.DidNotReceive().CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAndEscape_CloseTheDialog()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);
        await page.Find("[data-testid=export-cancel]").ClickAsync(new());
        page.FindAll("[data-testid=export-dialog]").ShouldBeEmpty();

        await OpenAsync(page);
        await page.Find("[data-testid=export-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        page.FindAll("[data-testid=export-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task AfterSaving_CloseReturnsToThePage()
    {
        ExportReturns(AssetData.Asset(status: AssetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Advanced);
        await OpenAsync(page);
        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        await page.Find("[data-testid=export-close]").ClickAsync(new());

        page.FindAll("[data-testid=export-dialog]").ShouldBeEmpty();
    }
}
