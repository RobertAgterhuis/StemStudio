using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Lyrics;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Mixer.Karaoke;

/// <summary>
/// Loads the track's lyrics (polled while they are searched or aligned), attaches the karaoke banner to the audio engine
/// once the track is loaded, and detaches it when the banner is hidden or the mixer goes away (feature 006).
/// </summary>
public sealed partial class KaraokeBanner : IAsyncDisposable
{
    /// <summary>Element id of the engine-owned host.</summary>
    public const string HostId = "karaoke-host";

    private readonly CancellationTokenSource _stop = new();
    private PollLoop? _poll;
    private KaraokeLyrics? _attached;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IAudioEngine Engine { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    /// <summary>The track of the mixer.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>True once the track is loaded into the engine; the banner is attached only then (engine contract 2.1.0).</summary>
    [Parameter]
    public bool EngineReady { get; set; }

    private LyricsDto? Lyrics { get; set; }

    private KaraokeLyrics? Karaoke { get; set; }

    private string? LoadError { get; set; }

    private bool ShowsBanner => Visible && Karaoke is not null;

    private string UnavailableStatus => Lyrics is not null ? LyricsText.Value(Lyrics.Status)
        : LyricsText.Value(LoadError is null ? LyricsStatusDto.None : LyricsStatusDto.Failed);

    // The first render happens while the lyrics are still being loaded: no document and no error yet.
    private string UnavailableText => LoadError ?? (Lyrics is null ? "Loading the lyrics..." : LyricsText.MixerNotice(Lyrics));

    private bool NeedsPolling => Lyrics?.Status is LyricsStatusDto.Searching or LyricsStatusDto.Aligning;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        FlushPendingSave();
        await DetachQuietlyAsync();
        _stop.Dispose();
        _save?.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Settings = await Choices.LoadAsync();
        await LoadAsync();
        (_poll ??= new PollLoop(Timer)).Start(() => NeedsPolling, PollAsync, () => Task.CompletedTask, _stop.Token);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => SyncEngineAsync();

    private async Task LoadAsync()
    {
        try
        {
            Apply(await Api.GetLyricsAsync(TrackId, _stop.Token));
            LoadError = null;
        }
        catch (ApiException ex)
        {
            LoadError = ex.Error.UserMessage;
        }
    }

    private void Apply(LyricsDto? lyrics)
    {
        lyrics ??= new LyricsDto { Status = LyricsStatusDto.None };
        (Lyrics, Karaoke, Offset) = (lyrics, KaraokeMapper.ToKaraoke(lyrics), lyrics.OffsetMs);
    }

    private async Task PollAsync(CancellationToken ct)
    {
        await LoadAsync();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Attaches the banner when it should be drawn and differs from the attached one; detaches it when it should not.</summary>
    private Task SyncEngineAsync()
    {
        var wanted = Visible && EngineReady ? Karaoke : null;
        if (ReferenceEquals(wanted, _attached))
        {
            return Task.CompletedTask;
        }

        _attached = wanted;
        return ShowAsync(wanted);
    }

    private async Task ShowAsync(KaraokeLyrics? lyrics)
    {
        try
        {
            await (lyrics is null ? Engine.DetachKaraokeAsync() : Engine.AttachKaraokeAsync(HostId, lyrics, Options()));
        }
        catch (AudioEngineException ex)
        {
            Error = $"The lyrics banner could not be shown: {ex.Message}";
            StateHasChanged();
        }
    }

    private KaraokeOptions Options() => new(Offset, Motion.Current.Motion == MotionPreference.Reduced ? true : null);

    private async Task DetachQuietlyAsync()
    {
        try
        {
            await (_attached is null ? Task.CompletedTask : Engine.DetachKaraokeAsync());
        }
        catch (Exception ex) when (ex is InvalidOperationException or AudioEngineException or JSDisconnectedException)
        {
            // The mixer released the engine first; nothing is drawn any more.
        }
    }
}
