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

/// <summary>Save all stems as a stem pack (007 T061, FR-013).</summary>
public sealed class StemPackExportTests : PageTestContext
{
    private static readonly StemPackDto Pack = new([.. Enumerable.Range(0, 6).Select(i => AssetData.Asset(Guid.NewGuid(), AssetStatusDto.Queued))]);

    public StemPackExportTests() =>
        Api.CreateStemPackAsync(Arg.Any<CreateStemPackRequest>(), Arg.Any<CancellationToken>()).Returns(Pack);

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, TrackDto track)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(track);
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<TrackDetail>(d => d.Add(x => x.Id, TestData.TrackId)));
    }

    [Fact]
    public void Beginner_HasNoStemPack() =>
        RenderAt(ExperienceLevel.Beginner, TestData.Track()).FindAll("[data-testid=export-all-stems]").ShouldBeEmpty();

    [Fact]
    public async Task StemPack_WithoutSplitParents_HasNoDetailOption_AndSubmitsThePack()
    {
        var page = RenderAt(ExperienceLevel.Advanced, TestData.Track());

        await page.Find("[data-testid=export-all-stems]").ClickAsync(new());

        page.Find("[data-testid=export-dialog]").GetAttribute("data-mode").ShouldBe("stem-pack");
        page.FindAll("[data-testid=export-include-detail], [data-testid=export-name], [data-testid=export-range-start]").ShouldBeEmpty();
        await page.Find("[data-testid=export-submit]").ClickAsync(new());
        await Api.Received(1).CreateStemPackAsync(new CreateStemPackRequest(TestData.TrackId, new ExportFormatDto(AudioFileFormatDto.Flac), false), Arg.Any<CancellationToken>());
        page.Find("[data-testid=export-saved]").TextContent.ShouldContain("Saved 6 items to the library");
        page.Find("[data-testid=export-saved-open]").GetAttribute("href").ShouldBe($"library?asset={Pack.Items[0].Id}");
    }

    [Fact]
    public async Task StemPack_WithSplitParents_CanIncludeTheDetailStems()
    {
        var page = RenderAt(ExperienceLevel.Expert, TestData.DetailTrack());
        await page.Find("[data-testid=export-all-stems]").ClickAsync(new());

        await page.Find("[data-testid=export-include-detail]").ChangeAsync(new ChangeEventArgs { Value = true });
        await page.Find("[data-testid=export-format]").ChangeAsync(new ChangeEventArgs { Value = "wav" });
        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        await Api.Received(1).CreateStemPackAsync(new CreateStemPackRequest(TestData.TrackId, new ExportFormatDto(AudioFileFormatDto.Wav), true), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StemPack_IsANamedButtonNextToTheStems()
    {
        var button = RenderAt(ExperienceLevel.Advanced, TestData.Track()).Find("[data-testid=export-all-stems]");

        button.TextContent.ShouldContain("Save all stems");
        button.GetAttribute("data-help-id").ShouldBe("export-actions");
    }
}
