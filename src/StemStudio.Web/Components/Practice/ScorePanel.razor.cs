using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Practice;

/// <summary>
/// Code-behind of the score panel: lists the track's scores, loads the chosen view (starting the view job for older
/// items and polling while it renders), attaches the engine's score follower to <see cref="HostId"/> once the pages are
/// rendered, and detaches on another choice, on close and on dispose.
/// </summary>
public sealed partial class ScorePanel : IAsyncDisposable
{
    /// <summary>Element id of the follower host.</summary>
    public const string HostId = "score-view";

    private bool _attachPending;
    private bool _attached;
    private int _labelSecond = -1;
    private string? _barLabel;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IAudioEngine Engine { get; set; } = default!;

    [Inject]
    private IOptions<StemStudioWebOptions> Options { get; set; } = default!;

    /// <summary>Track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>True when the engine has loaded the track (the follower needs it).</summary>
    [Parameter]
    public bool EngineReady { get; set; }

    /// <summary>Playback position (bar label).</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>Transpose in semitones (the note while not 0, FR-013).</summary>
    [Parameter]
    public int TransposeSemitones { get; set; }

    /// <summary>The loop (Shift+click extends from its start bar).</summary>
    [Parameter]
    public LoopRegionDto? Loop { get; set; }

    /// <summary>Shift+click sets a bar loop (Advanced, FR-012).</summary>
    [Parameter]
    public bool CanLoopBars { get; set; }

    /// <summary>Jump instead of smooth scrolling (the user's motion preference); null = the browser setting.</summary>
    [Parameter]
    public bool? ReducedMotion { get; set; }

    /// <summary>Raised with the start of a clicked bar.</summary>
    [Parameter]
    public EventCallback<double> OnSeek { get; set; }

    /// <summary>Raised with a bar loop.</summary>
    [Parameter]
    public EventCallback<LoopRegionDto> OnLoop { get; set; }

    /// <summary>True while auto-scroll is on.</summary>
    public bool Follow { get; private set; } = true;

    private IReadOnlyList<TrackScoreDto>? Scores { get; set; }

    private TrackScoreDto? Selected { get; set; }

    private ScoreViewDto? View { get; set; }

    private string? Error { get; set; }

    private string? Announcement { get; set; }

    private bool Transposed => TransposeSemitones != 0;

    private bool Preparing => View?.Status is ScoreViewStatusDto.None or ScoreViewStatusDto.Queued or ScoreViewStatusDto.Rendering;

    private string ViewStatus => View is null ? "none" : WireName.Of(View.Status);

    private string? BarLabel
    {
        get
        {
            var second = (int)Math.Floor(Position);
            if (second != _labelSecond)
            {
                (_labelSecond, _barLabel) = (second, CurrentBarText());
            }

            return _barLabel;
        }
    }

    /// <inheritdoc />
    protected override bool NeedsPolling => Preparing && Selected is not null;

    /// <summary>Turns auto-scroll on or off (also <c>mixer.score.follow</c>); turning it on ends a manual-scroll pause.</summary>
    /// <returns>A task that completes when applied.</returns>
    public async Task ToggleFollowAsync()
    {
        Follow = !Follow;
        if (_attached)
        {
            await EngineCallAsync(() => Engine.SetScoreFollowOptionsAsync(FollowOptions()));
        }

        StateHasChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await DetachAsync();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Scores = await Api.ListTrackScoresAsync(TrackId, Stopping);
        }
        catch (ApiException ex)
        {
            (Scores, Error) = ([], ex.Error.UserMessage);
        }

        if (Scores.Count > 0)
        {
            await LoadAsync(Scores[0]);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_attachPending && EngineReady && View is { Status: ScoreViewStatusDto.Ready } view)
        {
            _attachPending = false;
            _attached = await EngineCallAsync(() => Engine.AttachScoreFollowerAsync(HostId, ScoreViewMap.Of(view), FollowOptions()));
            if (!_attached)
            {
                StateHasChanged();
            }
        }
    }

    /// <inheritdoc />
    protected override async Task RefreshAsync(CancellationToken ct)
    {
        View = await Api.GetScoreViewAsync(Selected!.AssetId, ct);
        _attachPending = View.Status == ScoreViewStatusDto.Ready;
    }

    private static string OptionText(TrackScoreDto score)
    {
        var profile = WireName.Of(score.Profile);
        return $"{char.ToUpperInvariant(profile[0])}{profile[1..]} - {score.Name}";
    }

    private static string BarStyle(ScoreBarDto bar) => string.Create(
        CultureInfo.InvariantCulture,
        $"left:{bar.X * 100:0.###}%;top:{bar.Y * 100:0.###}%;width:{bar.Width * 100:0.###}%;height:{bar.Height * 100:0.###}%");

    private ScoreFollowOptions FollowOptions() => new(Follow, ReducedMotion);

    private string PageUrl(ScoreViewPageDto page) => new Uri(Options.Value.PublicApiBaseUrl, page.Url).ToString();

    private string? CurrentBarText()
    {
        if (View is not { Status: ScoreViewStatusDto.Ready } view || view.Bars.Count == 0)
        {
            return null;
        }

        var bar = view.Bars.FirstOrDefault(b => b.StartSeconds <= Position && Position < b.EndSeconds);
        var count = view.Bars[^1].Number;
        return bar is null ? null : string.Create(CultureInfo.InvariantCulture, $"Bar {bar.Number} of {count}");
    }

    private void ReadBar()
    {
        _labelSecond = -1;
        var text = BarLabel ?? "Not in a bar";
        Announcement = Announcement == text ? text + " " : text;
    }

    private async Task SelectAsync(ChangeEventArgs args)
    {
        if (Scores!.FirstOrDefault(s => s.AssetId.ToString() == args.Value?.ToString()) is { } score && score != Selected)
        {
            await DetachAsync();
            await LoadAsync(score);
        }
    }

    private async Task LoadAsync(TrackScoreDto score)
    {
        (Selected, View, Error) = (score, null, null);
        try
        {
            View = await Api.GetScoreViewAsync(score.AssetId, Stopping);
            View = View.Status == ScoreViewStatusDto.None ? await Api.CreateScoreViewAsync(score.AssetId, Stopping) : View;
            Error = View.Status == ScoreViewStatusDto.Failed ? View.Error ?? "The score view could not be made." : null;
            _attachPending = View.Status == ScoreViewStatusDto.Ready;
            StartPolling();
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private Task BarClickAsync(ScoreBarDto bar, bool shift)
    {
        if (!shift || !CanLoopBars)
        {
            return OnSeek.InvokeAsync(bar.StartSeconds);
        }

        var from = Loop is { } loop && loop.Start <= bar.StartSeconds
            ? View!.Bars.FirstOrDefault(b => b.StartSeconds <= loop.Start && loop.Start < b.EndSeconds) ?? bar
            : bar;
        return OnLoop.InvokeAsync(new LoopRegionDto(from.StartSeconds, bar.EndSeconds, true));
    }

    private async Task DetachAsync()
    {
        if (_attached)
        {
            _attached = false;
            await EngineCallAsync(Engine.DetachScoreFollowerAsync);
        }
    }

    private async Task<bool> EngineCallAsync(Func<Task> call)
    {
        try
        {
            await call();
            return true;
        }
        catch (AudioEngineException ex)
        {
            Error = ex.Message;
            return false;
        }
    }
}
