using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Export;
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

    /// <summary>Opens the export dialog for a take row (FR-015): presets "Take only" and "Take + mix" as heard.</summary>
    /// <param name="item">The saved take.</param>
    internal void OpenTakeExport(TakeItem item)
    {
        if (State is { } state && item.Take is { } take && _recording is { } session)
        {
            Exporting = ExportTarget.ForTake(state.ToExportTarget(_track!), TakeExportFor(session.Takes, take), MutedForCompare(session.Takes, take));
            StateHasChanged();
        }
    }

    // The compare mode as explicit mutes (data-model 2.7): "Original" mutes the take, "My take" the part's stem and details.
    private static TakeExport TakeExportFor(TakeLibrary takes, RecordingTakeDto take)
    {
        var loaded = takes.Loaded?.Take?.Id == take.Id;
        var strip = takes.Strip;
        var mute = loaded && (strip.Mute || takes.Compare == TakeCompareMode.Original);
        var channel = new TakeChannelSettingsDto { TakeId = take.Id, Volume = strip.Volume, Pan = strip.Pan, Mute = mute, Solo = loaded && strip.Solo };
        return new TakeExport(take.Id, take.Name, take.Status == TakeStatusDto.Ready, channel);
    }

    private HashSet<string> MutedForCompare(TakeLibrary takes, RecordingTakeDto take)
    {
        var mutesPart = takes.Loaded?.Take?.Id == take.Id && takes.Compare == TakeCompareMode.Take && take.Part != RecordingPartDto.None;
        var part = WireName.Of(take.Part);
        return mutesPart ? [.. State!.AllChannels.Select(c => c.Stem).Where(s => s == part || s.StartsWith(part + ".", StringComparison.Ordinal))] : [];
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
