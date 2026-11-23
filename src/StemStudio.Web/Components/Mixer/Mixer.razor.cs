using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
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

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IAudioEngine Engine { get; set; } = default!;

    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    /// <summary>The cascaded level (Advanced without a shell).</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The track id from the route.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>Optional mix to load (<c>?mixId=</c>).</summary>
    [SupplyParameterFromQuery]
    public Guid? MixId { get; set; }

    /// <summary>The mix to load when the mixer is hosted by setlist play mode (014 US5); wins over <see cref="MixId"/>.</summary>
    [Parameter]
    public Guid? SetlistMixId { get; set; }

    private MixerState? State { get; set; }

    private string Title => _track?.Title ?? string.Empty;

    private bool IsBeginner => (Experience ?? ExperienceContext.Default).Is(ExperienceLevel.Beginner);

    private IReadOnlyList<PracticePreset> Presets => PracticePresets.Available(State!);

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
        ReleasePractice();
        ReleaseAgentTools();
        await CloseRecorderAsync();
        await ReleaseKeysAsync();
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
            ApplyMuteLink(State, MuteChannel);
            await LoadQualityAsync();
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
        await RegisterKeysAsync();
        SubscribeEngine();
        await StartEngineAsync(State);
        await LoadDrumlessAsync();
        await LoadOfferAsync();
        await ApplyExpandLinkAsync();
        OnEngineStarted();
        await StartRecorderAsync();
        RegisterAgentTools();
        StateHasChanged();
    }

    /// <summary>Hook of feature 015 (<c>Mixer.Recording.cs</c>): runs once after the engine has loaded the track.</summary>
    partial void OnEngineStarted();

    private async Task StartEngineAsync(MixerState state)
    {
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

    private void SubscribeEngine()
    {
        Engine.PositionChanged += OnPositionChanged;
        Engine.PlaybackStateChanged += OnPlaybackStateChanged;
        Engine.PracticeChanged += OnPracticeChanged;
    }

    private async Task AttachMetersAsync(MixerState state)
    {
        foreach (var channel in state.AllChannels)
        {
            await Engine.AttachMeterAsync(channel.Stem, $"meter-{channel.Key}");
        }

        await Engine.AttachMeterAsync("master", "meter-master");
    }

    private async Task PushStateAsync(MixerState state)
    {
        await PushExpansionAsync(state);
        foreach (var channel in state.AllChannels)
        {
            await Engine.SetChannelAsync(channel.ToDto());
        }

        await Engine.SetMasterAsync(state.MasterVolume);
        await Engine.SetMetronomeAsync(state.Metronome);
        await (state.Loop is { } loop ? Engine.SetLoopAsync(loop) : Task.CompletedTask);
        await PracticeSender.SubmitAsync(state.Practice);
    }

    private void OnPositionChanged(object? sender, double seconds) =>
        _ = InvokeAsync(() =>
        {
            Position = seconds;
            RecordContext();
            RecordPosition(seconds);
            StateHasChanged();
        });

    private void OnPlaybackStateChanged(object? sender, PlaybackState state) =>
        _ = InvokeAsync(() =>
        {
            Playback = state;
            RecordPlayback(state);
            StateHasChanged();
        });

    private async Task ConfirmLeaveAsync(LocationChangingContext context)
    {
        if (State?.IsDirty != true || HelpLinks.TopicFromUri(context.TargetLocation) is not null)
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
