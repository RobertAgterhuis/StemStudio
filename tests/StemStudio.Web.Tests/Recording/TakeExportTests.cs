using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Export;
using StemStudio.Web.Export;
using StemStudio.Web.Interop;
using StemStudio.Web.Recording;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T074: export presets from a take row (take only, take + mix), compare as explicit mutes, export-channel-take (FR-015).</summary>
public sealed class TakeExportTests : MixerTestContext
{
    private readonly IRecordingEngine _recording = Substitute.For<IRecordingEngine>();

    public TakeExportTests()
    {
        _recording.Callbacks.Returns(new RecordingCallbacks());
        _recording.GetSupportAsync().Returns(new RecordingSupport(true, null, null, MicrophoneAccess.Granted));
        _recording.ListInputDevicesAsync().Returns([new InputDevice("d1", "USB Mic", "g1")]);
        _recording.OpenInputAsync(Arg.Any<OpenInputRequest>()).Returns(RecordingData.Input());
        Api.ListTakesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(RecordingData.List(RecordingData.Take()));
        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns(AssetData.Asset());
        Services.AddSingleton(_recording);
        Services.AddSingleton(new RecordingPreferences(JSInterop.JSRuntime));
    }

    private static string Row => $"{RecordingData.TakeId:D}";

    [Fact]
    public void TakeMix_SendsTheMixWithTheTake_AndTheTakeId()
    {
        var page = OpenTakeExport();

        page.Find("[data-testid=export-source-take-mix]").HasAttribute("checked").ShouldBeTrue();
        page.Find("[data-testid=export-submit]").Click();

        var request = Sent();
        (request.Mode, request.TakeId, request.Channels).ShouldBe((ExportModeDto.Mix, RecordingData.TakeId, null));
        request.Mix!.Take.ShouldBe(new TakeChannelSettingsDto { TakeId = RecordingData.TakeId, Volume = 0.8, Pan = 0, Mute = false, Solo = false });
    }

    [Fact]
    public void TakeOnly_SendsTheRawTake()
    {
        var page = OpenTakeExport();

        page.Find("[data-testid=export-source-take-only]").Change(true);
        page.Find("[data-testid=export-submit]").Click();

        var request = Sent();
        (request.Mode, request.TakeId, request.Mix).ShouldBe((ExportModeDto.Raw, RecordingData.TakeId, null));
        request.Channels.ShouldBe(["take"]);
        request.Range.ShouldBeNull();
    }

    [Fact]
    public void MyTake_MutesTheOriginalPartInTheExport()
    {
        var page = OpenTakeExport(TakeCompareMode.Take);

        page.Find("[data-testid=export-submit]").Click();

        var request = Sent();
        request.Mix!.Channels.Single(c => c.Stem == "guitar").Mute.ShouldBeTrue();
        request.Mix.Channels.Single(c => c.Stem == "vocals").Mute.ShouldBeFalse();
        request.Mix.Take!.Mute.ShouldBeFalse();
    }

    [Fact]
    public void Original_MutesTheTakeInTheExport()
    {
        var page = OpenTakeExport(TakeCompareMode.Original);

        page.Find("[data-testid=export-submit]").Click();

        Sent().Mix!.Take!.Mute.ShouldBeTrue();
    }

    [Fact]
    public void SelectedChannels_ListTheTake()
    {
        var page = OpenTakeExport();

        page.Find("[data-testid=export-source-selected]").Change(true);

        page.Find("[data-testid=export-channel-take]").HasAttribute("checked").ShouldBeTrue();
        page.Find("[data-testid=export-submit]").Click();
        Sent().Channels!.ShouldContain("take");
    }

    [Fact]
    public void AnOrdinaryMixerExport_IsUnchanged()
    {
        var page = Ready();

        page.Find("[data-testid=mixer-export]").Click();
        page.FindAll("[data-testid=export-source-take-mix]").ShouldBeEmpty();
        page.Find("[data-testid=export-submit]").Click();

        var request = Sent();
        (request.TakeId, request.Mix!.Take).ShouldBe((null, null));
    }

    [Fact]
    public void ASavingTake_ShowsTheNote()
    {
        var target = ExportTarget.ForTake(new ExportTarget(ExportKind.Mix, TestData.Track()) { Mix = new ExportMixDto(1, []) },
            new TakeExport(RecordingData.TakeId, "Take 1", false, new TakeChannelSettingsDto { TakeId = RecordingData.TakeId, Volume = 1, Pan = 0, Mute = false, Solo = false }),
            new HashSet<string>());

        var dialog = Render<ExportDialog>(p => p.Add(c => c.Target, target));

        dialog.Find("[data-testid=export-take-saving]").TextContent.ShouldContain("still being saved");
    }

    private IRenderedComponent<MixerPage> OpenTakeExport(TakeCompareMode compare = TakeCompareMode.Both)
    {
        var page = Ready();
        page.Find("[data-testid=mixer-record-panel-toggle]").Click();
        page.WaitForAssertion(() => page.Find($"[data-testid=rec-take-select-{Row}]"));
        page.Find($"[data-testid=rec-take-select-{Row}]").Change(true);
        page.WaitForAssertion(() => page.Find("[data-testid=strip-take]"));
        if (compare != TakeCompareMode.Both)
        {
            page.Find(compare == TakeCompareMode.Take ? "[data-testid=take-compare-take]" : "[data-testid=take-compare-original]").Click();
        }

        page.Find($"[data-testid=rec-take-export-{Row}]").Click();
        page.WaitForAssertion(() => page.Find("[data-testid=export-dialog]"));
        return page;
    }

    private CreateAudioExportRequest Sent() =>
        (CreateAudioExportRequest)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(Api.CreateAudioExportAsync)).GetArguments()[0]!;

    private IRenderedComponent<MixerPage> Ready()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeTrue());
        page.WaitForAssertion(() => page.FindAll("[data-testid=mixer-loading]").ShouldBeEmpty());
        return page;
    }
}
