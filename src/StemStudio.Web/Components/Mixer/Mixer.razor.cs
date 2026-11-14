using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Mixer page (<c>/tracks/{id}/mixer</c>): loads the track into the audio engine, keeps <see cref="MixerState"/>
/// and forwards every control change to the engine immediately (US2, FR-011..FR-013, SC-004).
/// </summary>
public sealed partial class Mixer : IAsyncDisposable
{
    private const string NotReadyMessage = "This track is not ready yet. Open the mixer once processing has completed.";
    private TrackDto? _track;
    private bool _engineStarted;
    private IAsyncDisposable? _spaceKey;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IAudioEngine Engine { get; set; } = default!;

    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    [Inject]
    private IKeyboardShortcuts Shortcuts { get; set; } = default!;

    /// <summary>The track id from the route.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>Optional mix to load (<c>?mixId=</c>).</summary>
    [SupplyParameterFromQuery]
    public Guid? MixId { get; set; }

    private MixerState? State { get; set; }

    private string Title => _track?.Title ?? string.Empty;

    private string? LoadError { get; set; }

    private string? EngineError { get; set; }

    private bool EngineReady { get; set; }

    private double Position { get; set; }

    private PlaybackState Playback { get; set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Engine.PositionChanged -= OnPositionChanged;
        Engine.PlaybackStateChanged -= OnPlaybackStateChanged;
        await (_spaceKey?.DisposeAsync() ?? ValueTask.CompletedTask);
        await Engine.DisposeAsync();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _track = await Api.GetTrackAsync(Id);
            LoadError = _track.Status == TrackStatusDto.Completed ? null : NotReadyMessage;
            State = LoadError is null ? new MixerState(_track) : null;
        }
        catch (ApiException ex)
        {
            LoadError = ex.Error.UserMessage;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (State is null || _engineStarted)
        {
            return;
        }

        _engineStarted = true;
        _spaceKey = await Shortcuts.RegisterSpaceAsync(() => InvokeAsync(TogglePlaybackAsync));
        await StartEngineAsync(State);
        StateHasChanged();
    }

    private async Task StartEngineAsync(MixerState state)
    {
        Engine.PositionChanged += OnPositionChanged;
        Engine.PlaybackStateChanged += OnPlaybackStateChanged;
        try
        {
            await Engine.InitializeAsync();
            state.Duration = await Engine.LoadTrackAsync(_track!);
            await AttachMetersAsync(state);
            await PushStateAsync(state);
            EngineReady = true;
        }
        catch (AudioEngineException ex)
        {
            EngineError = ex.Message;
        }
    }

    private async Task AttachMetersAsync(MixerState state)
    {
        foreach (var target in state.Channels.Select(c => c.Stem).Append("master"))
        {
            await Engine.AttachMeterAsync(target, $"meter-{target}");
        }
    }

    private async Task PushStateAsync(MixerState state)
    {
        foreach (var channel in state.Channels)
        {
            await Engine.SetChannelAsync(channel.ToDto());
        }

        await Engine.SetMasterAsync(state.MasterVolume);
        await Engine.SetMetronomeAsync(state.Metronome);
        await (state.Loop is { } loop ? Engine.SetLoopAsync(loop) : Task.CompletedTask);
    }

    private void OnPositionChanged(object? sender, double seconds) =>
        _ = InvokeAsync(() =>
        {
            Position = seconds;
            StateHasChanged();
        });

    private void OnPlaybackStateChanged(object? sender, PlaybackState state) =>
        _ = InvokeAsync(() =>
        {
            Playback = state;
            StateHasChanged();
        });

    private async Task ConfirmLeaveAsync(LocationChangingContext context)
    {
        if (State?.IsDirty != true)
        {
            return;
        }

        var leave = await Confirm.ConfirmAsync("Unsaved changes", "Leave the mixer and discard your unsaved changes?", "Leave");
        if (!leave)
        {
            context.PreventNavigation();
        }
    }
}
