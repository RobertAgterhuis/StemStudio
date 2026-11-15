using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Services;

namespace StemStudio.Web.Audio;

/// <summary>
/// JS interop wrapper over the audio engine ES module (<c>./js/audio-engine.js</c>). Registered scoped
/// (one per circuit); the mixer page initializes it and disposes it when the page goes away.
/// </summary>
/// <param name="js">The circuit's JS runtime.</param>
/// <param name="options">Web options; <see cref="StemStudioWebOptions.PublicApiBaseUrl"/> resolves stem URLs for the browser.</param>
public sealed class AudioEngineInterop(IJSRuntime js, IOptions<StemStudioWebOptions> options) : IAudioEngine
{
    private IJSObjectReference? _module;
    private IJSObjectReference? _engine;
    private DotNetObjectReference<AudioEngineInterop>? _self;

    /// <inheritdoc />
    public event EventHandler<double>? PositionChanged;

    /// <inheritdoc />
    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    /// <inheritdoc />
    public bool IsInitialized => _engine is not null;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (_engine is not null)
        {
            return;
        }

        _self ??= DotNetObjectReference.Create(this);
        _module ??= await Guard(() => js.InvokeAsync<IJSObjectReference>(EngineMethods.Import, EngineMethods.ModulePath).AsTask());
        _engine = await Guard(() => _module.InvokeAsync<IJSObjectReference>(EngineMethods.CreateEngine, _self).AsTask());
    }

    /// <inheritdoc />
    public Task<double> LoadTrackAsync(TrackDto track)
    {
        var stems = track.Stems.Select(s => Stem(s.Name, s.Url)).ToList();
        var groups = DetailPhaseText.SplitParents(track)
            .Select(p => new EngineGroup(p.Parent, DetailPhaseText.OrderedStems(p).Select(s => Stem(s.Name, s.Url)).ToList()))
            .ToList();
        return Guard(() => Engine.InvokeAsync<double>(EngineMethods.LoadTrack, new EngineLoadRequest(stems, track.BeatGrid, groups)).AsTask());
    }

    /// <inheritdoc />
    public Task PlayAsync() => Call(EngineMethods.Play);

    /// <inheritdoc />
    public Task PauseAsync() => Call(EngineMethods.Pause);

    /// <inheritdoc />
    public Task StopAsync() => Call(EngineMethods.Stop);

    /// <inheritdoc />
    public Task SeekAsync(double seconds) => Call(EngineMethods.Seek, seconds);

    /// <inheritdoc />
    public Task SetChannelAsync(ChannelSettingsDto channel) =>
        Call(EngineMethods.SetChannel, channel.Stem, new EngineChannel(channel.Volume, channel.Pan, channel.Mute, channel.Solo));

    /// <inheritdoc />
    public Task SetExpandedAsync(string parent, bool expanded) => Call(EngineMethods.SetExpanded, parent, expanded);

    /// <inheritdoc />
    public Task SetMasterAsync(double volume) => Call(EngineMethods.SetMaster, volume);

    /// <inheritdoc />
    public Task SetLoopAsync(LoopRegionDto region) => Call(EngineMethods.SetLoop, region);

    /// <inheritdoc />
    public Task SetMetronomeAsync(MetronomeSettingsDto settings) => Call(EngineMethods.SetMetronome, settings);

    /// <inheritdoc />
    public Task AttachMeterAsync(string target, string elementId) => Call(EngineMethods.AttachMeter, target, elementId);

    /// <inheritdoc />
    public Task AttachKaraokeAsync(string elementId, KaraokeLyrics lyrics, KaraokeOptions options) =>
        Call(EngineMethods.AttachKaraoke, elementId, lyrics, options);

    /// <inheritdoc />
    public Task SetKaraokeOffsetAsync(int offsetMs) => Call(EngineMethods.SetKaraokeOffset, offsetMs);

    /// <inheritdoc />
    public Task DetachKaraokeAsync() => _engine is null ? Task.CompletedTask : Call(EngineMethods.DetachKaraoke);

    /// <summary>Engine callback <c>OnPositionChanged</c>.</summary>
    /// <param name="seconds">The transport position.</param>
    [JSInvokable]
    public void OnPositionChanged(double seconds) => PositionChanged?.Invoke(this, seconds);

    /// <summary>Engine callback <c>OnPlaybackStateChanged</c>; unknown states are ignored.</summary>
    /// <param name="state"><c>stopped</c>, <c>playing</c> or <c>paused</c>.</param>
    [JSInvokable]
    public void OnPlaybackStateChanged(string state)
    {
        if (Enum.TryParse<PlaybackState>(state, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            PlaybackStateChanged?.Invoke(this, parsed);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var engine = _engine;
        var module = _module;
        _engine = null;
        _module = null;
        await SafeDisposeAsync(engine, module);
        _self?.Dispose();
        _self = null;
    }

    private EngineStem Stem(string name, string url) => new(name, new Uri(options.Value.PublicApiBaseUrl, url).ToString());

    private IJSObjectReference Engine =>
        _engine ?? throw new InvalidOperationException("The audio engine is not initialized.");

    private async Task Call(string method, params object?[] args)
    {
        var engine = Engine;
        await Guard(async () =>
        {
            await engine.InvokeVoidAsync(method, args);
            return true;
        });
    }

    private static async Task<T> Guard<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (JSException ex)
        {
            throw new AudioEngineException(ex.Message, ex);
        }
    }

    private static async Task SafeDisposeAsync(IJSObjectReference? engine, IJSObjectReference? module)
    {
        try
        {
            if (engine is not null)
            {
                await engine.InvokeVoidAsync(EngineMethods.Dispose);
                await engine.DisposeAsync();
            }

            await (module?.DisposeAsync() ?? ValueTask.CompletedTask);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException)
        {
            // The circuit is gone or the engine was already released; nothing left to clean up.
        }
    }

    private sealed record EngineStem(string Name, string Url);

    private sealed record EngineGroup(string Parent, IReadOnlyList<EngineStem> Stems);

    private sealed record EngineLoadRequest(IReadOnlyList<EngineStem> Stems, BeatGridDto? BeatGrid, IReadOnlyList<EngineGroup> Groups);

    private sealed record EngineChannel(double Volume, double Pan, bool Mute, bool Solo);
}
