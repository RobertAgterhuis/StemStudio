using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Export;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Export;

/// <summary>Loop region and start/end range of an export (007 T056, FR-006, FR-014).</summary>
public sealed class RangeExportTests : MixerTestContext
{
    public RangeExportTests()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns(AssetData.Asset(status: AssetStatusDto.Queued));
    }

    private async Task<IRenderedComponent<MixerPage>> OpenAsync(Guid? mixId = null)
    {
        var page = RenderMixer(mixId);
        await page.Find("[data-testid=mixer-export]").ClickAsync(new());
        return page;
    }

    private static void Range(IRenderedComponent<MixerPage> page, string start, string end)
    {
        page.Find("[data-testid=export-range-start]").Input(start);
        page.Find("[data-testid=export-range-end]").Input(end);
    }

    private static Task SubmitAsync(IRenderedComponent<MixerPage> page) => page.Find("[data-testid=export-submit]").ClickAsync(new());

    [Fact]
    public async Task WithoutALoopRegion_ThereIsNoLoopCheckbox_ButStartAndEnd()
    {
        var page = await OpenAsync();

        page.FindAll("[data-testid=export-range-loop]").ShouldBeEmpty();
        var start = page.Find("[data-testid=export-range-start]");
        (start.GetAttribute("type"), start.GetAttribute("data-help-id")).ShouldBe(("number", "export-range"));
        page.Find("[data-testid=export-range-end]").GetAttribute("type").ShouldBe("number");
    }

    [Fact]
    public async Task LoopRegion_FillsStartAndEnd_AndIsSent()
    {
        var page = await OpenAsync(TestData.MixId);

        await page.Find("[data-testid=export-range-loop]").ChangeAsync(new ChangeEventArgs { Value = true });

        page.Find("[data-testid=export-range-start]").GetAttribute("value").ShouldBe("10");
        page.Find("[data-testid=export-range-end]").GetAttribute("value").ShouldBe("20");
        await SubmitAsync(page);
        await Api.Received(1).CreateAudioExportAsync(Arg.Is<CreateAudioExportRequest>(r => r.Range == new RangeDto(10, 20)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UntickingTheLoop_ClearsTheRange()
    {
        var page = await OpenAsync(TestData.MixId);
        await page.Find("[data-testid=export-range-loop]").ChangeAsync(new ChangeEventArgs { Value = true });

        await page.Find("[data-testid=export-range-loop]").ChangeAsync(new ChangeEventArgs { Value = false });
        await SubmitAsync(page);

        page.Find("[data-testid=export-range-start]").GetAttribute("value").ShouldBeNullOrEmpty();
        await Api.Received(1).CreateAudioExportAsync(Arg.Is<CreateAudioExportRequest>(r => r.Range == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TypedRange_IsSent()
    {
        var page = await OpenAsync();

        Range(page, "30", "75.5");
        await SubmitAsync(page);

        await Api.Received(1).CreateAudioExportAsync(Arg.Is<CreateAudioExportRequest>(r => r.Range == new RangeDto(30, 75.5)), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("10", "", RangeInput.IncompleteMessage)]
    [InlineData("abc", "20", RangeInput.IncompleteMessage)]
    [InlineData("10", "10.4", RangeInput.TooShortMessage)]
    [InlineData("20", "10", RangeInput.TooShortMessage)]
    [InlineData("-1", "10", "The range must lie inside the song (0 to 3:33).")]
    [InlineData("200", "300", "The range must lie inside the song (0 to 3:33).")]
    public async Task InvalidRanges_AreExplainedBeforeAnyRequest(string start, string end, string message)
    {
        var page = await OpenAsync();

        Range(page, start, end);
        await SubmitAsync(page);

        page.Find("[data-testid=export-error]").TextContent.ShouldBe(message);
        await Api.DidNotReceive().CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExactlyHalfASecond_IsAccepted()
    {
        var page = await OpenAsync();

        Range(page, "0", "0.5");
        await SubmitAsync(page);

        await Api.Received(1).CreateAudioExportAsync(Arg.Is<CreateAudioExportRequest>(r => r.Range == new RangeDto(0, 0.5)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RangeHelpTopic_Exists() =>
        StemStudio.Web.Help.HelpCatalog.FromAssembly(typeof(StemStudio.Web.Help.HelpCatalog).Assembly).TopicForAnchor("export-range")!.Id.ShouldBe("export-range");
}
