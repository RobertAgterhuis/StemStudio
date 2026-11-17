using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Recording;

/// <summary>A mixer page as seen by the recording UI: track id, practice settings and a re-render counter.</summary>
public sealed class FakeRecordingHost : IRecordingHost
{
    public Guid TrackId { get; set; } = RecordingData.TrackId;

    public Guid? MixId { get; set; }

    public bool TrackReady { get; set; } = true;

    public PracticeSettingsDto Practice { get; set; } = PracticeSettingsDto.Default;

    public int Refreshes { get; private set; }

    public List<double> Seeks { get; } = [];

    public int NormalSpeedCalls { get; private set; }

    public Task SetNormalSpeedAsync()
    {
        NormalSpeedCalls++;
        Practice = PracticeSettingsDto.Default;
        return Task.CompletedTask;
    }

    public Task SeekAsync(double seconds)
    {
        Seeks.Add(seconds);
        return Task.CompletedTask;
    }

    public Task RefreshAsync()
    {
        Refreshes++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// bUnit fixture of the recording components: a real <see cref="RecordingSession"/> over a substituted
/// <see cref="IRecordingEngine"/> (real callback hub), the substituted API and audio engine and the manual poll timer.
/// </summary>
public abstract class RecordingTestContext : PageTestContext
{
    protected RecordingTestContext()
    {
        RecordingEngine.Callbacks.Returns(Callbacks);
        RecordingEngine.IsAvailable.Returns(true);
        Supported(MicrophoneAccess.Prompt);
        RecordingEngine.ListInputDevicesAsync().Returns([new InputDevice("d1", "USB Mic", "g1"), new InputDevice("d2", "Interface 1-2", "g2")]);
        RecordingEngine.OpenInputAsync(Arg.Any<OpenInputRequest>()).Returns(RecordingData.Input());
        RecordingEngine.StopRecordingAsync().Returns([]);
        Api.ListTakesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(RecordingData.List());
        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        Session = new RecordingSession(
            new RecordingServices(RecordingEngine, Engine, Api, new RecordingPreferences(JSInterop.JSRuntime), Timer),
            Host,
            url => $"http://api.test{url}");
    }

    protected IRecordingEngine RecordingEngine { get; } = Substitute.For<IRecordingEngine>();

    protected RecordingCallbacks Callbacks { get; } = new();

    protected FakeRecordingHost Host { get; } = new();

    protected RecordingSession Session { get; }

    protected void Supported(MicrophoneAccess permission) =>
        RecordingEngine.GetSupportAsync().Returns(new RecordingSupport(true, null, null, permission));

    protected void Unsupported(string code, string reason) =>
        RecordingEngine.GetSupportAsync().Returns(new RecordingSupport(false, code, reason, MicrophoneAccess.Unknown));

    protected void StoredSettings(string json) =>
        JSInterop.Setup<string?>("localStorage.getItem", RecordingPreferences.StorageKey).SetResult(json);

    /// <summary>Opens the panel and allows the microphone (the common start of a recording test).</summary>
    protected async Task OpenWithInputAsync()
    {
        Supported(MicrophoneAccess.Granted);
        await Session.TogglePanelAsync();
    }

    protected IRenderedComponent<CascadingValue<ExperienceContext>> RenderPanel(ExperienceLevel level = ExperienceLevel.Advanced) =>
        RenderAtLevel<StemStudio.Web.Components.Recording.RecordingPanel>(level, p => p.Add(c => c.Session, Session));

    /// <summary>Releases one poll of saving takes and waits until it was handled (next wait or nothing left to poll).</summary>
    protected async Task TickAsync()
    {
        var before = Timer.Waits;
        Timer.Tick();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Timer.Waits == before && Session.Takes.SavingOnServer().Count > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        await Task.Delay(20);
    }

    /// <summary>Re-renders the panel inside its cascading level (as the mixer does after a callback).</summary>
    protected static void Refresh(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        page.FindComponent<StemStudio.Web.Components.Recording.RecordingPanel>().Render();

    protected static IJSStreamReference Wav(int bytes = 1024)
    {
        var reference = Substitute.For<IJSStreamReference>();
        reference.OpenReadStreamAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream(new byte[bytes]));
        return reference;
    }
}
