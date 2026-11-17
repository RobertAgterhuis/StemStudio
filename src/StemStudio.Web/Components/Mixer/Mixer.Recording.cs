using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Mixer;
using StemStudio.Web.Recording;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Play-along recording in the mixer (feature 015, owned by the 015 frontend lane): the page is the
/// <see cref="IRecordingHost"/> of one <see cref="RecordingSession"/>, created on first use by the recording slots and
/// disposed by <c>RecordingArea</c> when the page goes away. Without the recording services (tests of other features)
/// <see cref="Recording"/> is null and the slots render nothing.
/// </summary>
public sealed partial class Mixer : IRecordingHost
{
    private RecordingSession? _recording;
    private bool _recordingResolved;

    [Inject]
    private IServiceProvider PageServices { get; set; } = default!;

    /// <summary>The recording session of this page, or null when recording is not available.</summary>
    internal RecordingSession? Recording
    {
        get
        {
            if (!_recordingResolved)
            {
                _recordingResolved = true;
                _recording = CreateRecording();
            }

            return _recording;
        }
    }

    /// <summary>False when another channel is soloed and the take is not (the take strip is shown dimmed).</summary>
    internal bool TakeAudible => State is not { } state || !state.AnySolo || _recording?.Takes.Strip.Solo == true;

    /// <inheritdoc />
    Guid IRecordingHost.TrackId => Id;

    /// <inheritdoc />
    Guid? IRecordingHost.MixId => State?.MixId;

    /// <inheritdoc />
    bool IRecordingHost.TrackReady => EngineReady;

    /// <inheritdoc />
    PracticeSettingsDto IRecordingHost.Practice => State?.Practice ?? PracticeSettingsDto.Default;

    /// <inheritdoc />
    Task IRecordingHost.SetNormalSpeedAsync() => ResetPracticeAsync();

    /// <inheritdoc />
    Task IRecordingHost.SeekAsync(double seconds) => SeekAsync(seconds);

    /// <inheritdoc />
    Task IRecordingHost.RefreshAsync() => InvokeAsync(StateHasChanged);

    /// <summary>Disposes the recording session (called by the recording area slot when the page goes away).</summary>
    /// <returns>A task that completes when the microphone is released.</returns>
    internal async Task ReleaseRecordingAsync()
    {
        var session = _recording;
        _recording = null;
        await (session?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    partial void RegisterRecordingCommands(MixerCommandRegistry commands)
    {
        commands.Add("mixer.record.toggle", RecordShortcutAsync);
        commands.Add("mixer.compare.cycle", CompareShortcutAsync);
    }

    // R: opens the panel the first time (so the input can be chosen), then Record/Stop.
    private Task RecordShortcutAsync() => Recording switch
    {
        null => Task.CompletedTask,
        { PanelOpen: false } session => RefreshAfter(session.TogglePanelAsync()),
        var session => RefreshAfter(session.ToggleRecordAsync()),
    };

    private Task CompareShortcutAsync() => Recording is { } session ? RefreshAfter(session.CycleCompareAsync()) : Task.CompletedTask;

    private async Task RefreshAfter(Task action)
    {
        await action;
        StateHasChanged();
    }

    private RecordingSession? CreateRecording()
    {
        if (PageServices.GetService(typeof(IRecordingEngine)) is not IRecordingEngine engine
            || PageServices.GetService(typeof(RecordingPreferences)) is not RecordingPreferences preferences)
        {
            return null;
        }

        var timer = (IPollingTimer)PageServices.GetService(typeof(IPollingTimer))!;
        var options = (IOptions<StemStudioWebOptions>)PageServices.GetService(typeof(IOptions<StemStudioWebOptions>))!;
        var limits = PageServices.GetService(typeof(IOptions<RecordingUiOptions>)) as IOptions<RecordingUiOptions>;
        var services = new RecordingServices(engine, Engine, Api, preferences, timer)
        {
            MaxStreamBytes = limits?.Value.MaxStreamBytes ?? RecordingLimits.DefaultMaxUploadBytes,
        };
        var session = new RecordingSession(services, this, url => new Uri(options.Value.PublicApiBaseUrl, url).ToString());
        session.Takes.StripChanged += SyncTakeChannel;
        return session;
    }

    // The mix keeps the strip of a saved take (FR-012); a soloed take silences the stems like a stem solo (engine gate rule).
    private void SyncTakeChannel()
    {
        if (State is not { } state || _recording is not { } session)
        {
            return;
        }

        state.ExternalSolo = session.Takes.Loaded is not null && session.Takes.Strip.Solo;
        var take = session.Takes.MixSettings();
        if (take != state.Take)
        {
            state.SetTake(take);
        }
    }

    partial void OnMixLoaded(MixDto mix)
    {
        if (mix.Settings.Take is { } take && Recording is { } session)
        {
            _ = InvokeAsync(() => RestoreTakeAsync(session, take));
        }
    }

    private async Task RestoreTakeAsync(RecordingSession session, TakeChannelSettingsDto take)
    {
        await session.Takes.LoadAsync(Id);
        await session.Takes.RestoreAsync(take);
        StateHasChanged();
    }
}
