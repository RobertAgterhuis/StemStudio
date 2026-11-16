using System.Text.Json;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Agent;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>
/// Practice settings in save, save as, load and the agent's mixer state; the export dialog's practice note while the
/// export request stays unchanged (012 T039, FR-014, FR-015). backend 012: a PUT without <c>practice</c> resets it.
/// </summary>
public sealed class PracticeSaveExportTests : PracticeTestContext
{
    private static readonly PracticeSettingsDto Trained = new(80, -2, new SpeedTrainerSettingsDto(true, 60, 10, 90, 2), new CountInSettingsDto(1, false));

    public PracticeSaveExportTests()
    {
        Api.UpdateMixAsync(TestData.MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>())
            .Returns(c => TestData.Mix() with { Settings = c.Arg<MixSettingsDto>() });
        Api.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>())
            .Returns(c => TestData.Mix("Slow") with { Settings = c.Arg<CreateMixRequest>().Settings });
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns(AssetData.Asset(status: AssetStatusDto.Queued));
    }

    [Fact]
    public async Task Save_SendsThePracticeSettings_SoTheBackendDoesNotResetThem()
    {
        MixWith(Trained);
        var page = RenderAt(withMix: true);
        page.WaitForAssertion(() => page.Find("[data-testid=practice-panel]").GetAttribute("data-speed").ShouldBe("80"));

        page.Find("[data-testid=practice-transpose-up]").Click();
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
        await page.Find("[data-testid=mix-save]").ClickAsync(new());

        var expected = Trained with { TransposeSemitones = -1 };
        await Api.Received(1).UpdateMixAsync(TestData.MixId, Arg.Is<MixSettingsDto>(s => s.Practice == expected),
            Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveAs_IncludesThePracticeSettings()
    {
        var page = RenderAt();
        page.Find("[data-testid=practice-speed]").Input("65");

        await page.Find("[data-testid=mix-save-as]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").Input("Slow");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        await Api.Received(1).CreateMixAsync(Arg.Is<CreateMixRequest>(r => r.Settings.Practice == At(65)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Load_RestoresThePracticeSettings_AndIsClean()
    {
        MixWith(Trained);

        var page = RenderAt(withMix: true);

        page.WaitForAssertion(() => Engine.Received(1).SetPracticeAsync(Trained));
        page.Find("[data-testid=practice-transpose]").GetAttribute("aria-valuenow").ShouldBe("-2");
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentMixerState_CarriesThePracticeSettings()
    {
        var controls = new FakeMixerControls();
        controls.State.SetSpeed(70);
        controls.State.SetCountInBars(2);
        var tools = new MixerUiTools(controls);

        var result = await tools.ExecuteAsync("mixer_get_state", new UiToolArguments(AgentData.Json("{}")));

        var practice = JsonSerializer.SerializeToElement(result.Result, JsonSerializerOptions.Web).GetProperty("settings").GetProperty("practice");
        practice.GetProperty("speedPercent").GetInt32().ShouldBe(70);
        practice.GetProperty("countIn").GetProperty("bars").GetInt32().ShouldBe(2);
        practice.GetProperty("trainer").GetProperty("targetPercent").GetInt32().ShouldBe(100);
    }

    [Theory]
    [InlineData(80, 0, true)]
    [InlineData(100, 3, true)]
    [InlineData(100, 0, false)]
    public async Task ExportDialog_SaysExportsUseTheOriginalSpeedAndKey_OnlyWhenSlowedOrTransposed(int speed, int transpose, bool shown)
    {
        MixWith(At(speed, transpose));
        var page = RenderAt(ExperienceLevel.Beginner, withMix: true);
        page.WaitForAssertion(() => page.Find("[data-testid=practice-panel]").GetAttribute("data-transpose").ShouldBe(transpose.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        await page.Find("[data-testid=mixer-export]").ClickAsync(new());

        var note = page.FindAll("[data-testid=export-dialog] [data-testid=export-practice-note]");
        note.Count.ShouldBe(shown ? 1 : 0);
        if (shown)
        {
            note[0].TextContent.Trim().ShouldBe("Exports use the original speed and key.");
        }
    }

    [Fact]
    public async Task ExportDialog_ShowsTheNote_WhileTheTrainerIsOn()
    {
        MixWith(PracticeSettingsDto.Default with { Trainer = Trained.Trainer });
        var page = RenderAt(withMix: true);
        page.WaitForAssertion(() => Engine.Received(1).SetPracticeAsync(Arg.Any<PracticeSettingsDto>()));

        await page.Find("[data-testid=mixer-export]").ClickAsync(new());

        page.FindAll("[data-testid=export-practice-note]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExportRequest_IsUnchanged_AtPracticeTempo()
    {
        var page = RenderAt();
        page.Find("[data-testid=practice-speed]").Input("60");

        await page.Find("[data-testid=mixer-export]").ClickAsync(new());
        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        await Api.Received(1).CreateAudioExportAsync(
            Arg.Is<CreateAudioExportRequest>(r => !JsonSerializer.Serialize(r, JsonSerializerOptions.Web).Contains("practice", StringComparison.OrdinalIgnoreCase)
                && !JsonSerializer.Serialize(r, JsonSerializerOptions.Web).Contains("speed", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }
}
