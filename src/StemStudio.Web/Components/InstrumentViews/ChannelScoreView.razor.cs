using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Scores;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>Lists the track's scores and shows <see cref="ScoreView"/> when one has a matching profile, else offers to make one.</summary>
public sealed partial class ChannelScoreView : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private ChannelScoreMaker? _maker;

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>The channel a new item is made from.</summary>
    [Parameter, EditorRequired]
    public string Channel { get; set; } = string.Empty;

    /// <summary>The profiles that count as a match.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ScoreProfileDto> Profiles { get; set; } = [];

    /// <summary>What the item is called in texts, e.g. "drum notation".</summary>
    [Parameter, EditorRequired]
    public string Noun { get; set; } = "sheet music";

    /// <summary>The button text, e.g. "Generate drum notation".</summary>
    [Parameter, EditorRequired]
    public string GenerateText { get; set; } = string.Empty;

    /// <summary>Test id of the region (ui-test-ids, e.g. <c>drum-score</c>), or null.</summary>
    [Parameter]
    public string? RegionTestId { get; set; }

    /// <summary>Test id of the button (ui-test-ids, e.g. <c>drum-score-generate</c>), or null.</summary>
    [Parameter]
    public string? GenerateTestId { get; set; }

    /// <summary>Adjusts the request (tablature, part); null sends no tablature.</summary>
    [Parameter]
    public Action<ScoreForm>? Configure { get; set; }

    /// <summary>The 014 score view, shown when a matching item exists.</summary>
    [Parameter]
    public RenderFragment? ScoreView { get; set; }

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    private IReadOnlyList<TrackScoreDto>? Scores { get; set; }

    private bool HasScore => Scores?.Any(s => Profiles.Contains(s.Profile)) == true;

    private bool Making { get; set; }

    private string? Error { get; set; }

    private string Label => char.ToUpperInvariant(Noun[0]) + Noun[1..];

    private string EmptyText => $"No {Noun} for this song yet.";

    private string ProgressText => string.Create(CultureInfo.InvariantCulture, $"Making the {Noun}: {_maker?.Progress ?? 0} %");

    private ChannelScoreMaker Maker => _maker ??= new ChannelScoreMaker(Api, Timer, $"The {Noun} could not be made.", Configure);

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadScoresAsync();

    private async Task LoadScoresAsync()
    {
        try
        {
            Scores = await Api.ListTrackScoresAsync(Track.Id, _stop.Token);
        }
        catch (ApiException ex)
        {
            (Scores, Error) = ([], ex.Error.UserMessage);
        }
    }

    // The click handler returns at once; the item is made in the background (the status shows the progress).
    private void StartGenerate()
    {
        (Making, Error) = (true, null);
        _ = GenerateAsync();
    }

    private async Task GenerateAsync()
    {
        try
        {
            await MakeAsync();
        }
        catch (OperationCanceledException)
        {
            return; // The view was closed while it waited.
        }

        Making = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task MakeAsync()
    {
        try
        {
            var item = await Maker.CreateAsync(Track, Channel, () => InvokeAsync(StateHasChanged), _stop.Token);
            Error = Maker.Error;
            await (item is null ? Task.CompletedTask : LoadScoresAsync());
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
