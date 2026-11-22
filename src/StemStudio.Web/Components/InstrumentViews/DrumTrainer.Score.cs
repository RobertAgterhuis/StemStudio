using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Api;
using StemStudio.Web.Scores;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>The drum notation of the trainer (FR-034): the 014 score view of a drum score item, or "Generate drum notation".</summary>
public sealed partial class DrumTrainer
{
    private ChannelScoreMaker? _maker;

    /// <summary>The 014 score view of the drum item, shown when one exists (the mixer passes a <c>ScorePanel</c>).</summary>
    [Parameter]
    public RenderFragment? ScoreView { get; set; }

    private IReadOnlyList<TrackScoreDto>? Scores { get; set; }

    private bool HasDrumScore => Scores?.Any(s => s.Profile == ScoreProfileDto.Drums) == true;

    private bool Making { get; set; }

    private int MakingProgress => _maker?.Progress ?? 0;

    private string? ScoreError { get; set; }

    private ChannelScoreMaker Maker => _maker ??= new ChannelScoreMaker(Api, Timer, "The drum notation could not be made.");

    private async Task LoadScoresAsync()
    {
        try
        {
            Scores = await Api.ListTrackScoresAsync(Track.Id, _stop.Token);
        }
        catch (ApiException ex)
        {
            (Scores, ScoreError) = ([], ex.Error.UserMessage);
        }
    }

    // The click handler returns at once; the item is made in the background (the button shows the progress).
    private void StartGenerate()
    {
        (Making, ScoreError) = (true, null);
        _ = GenerateAsync();
    }

    private async Task GenerateAsync()
    {
        if (await TryGenerateAsync())
        {
            Making = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // False when the trainer was closed while it waited.
    private async Task<bool> TryGenerateAsync()
    {
        try
        {
            var item = await Maker.CreateAsync(Track, StemNames.Drums, () => InvokeAsync(StateHasChanged), _stop.Token);
            ScoreError = Maker.Error;
            await (item is null ? Task.CompletedTask : LoadScoresAsync());
            return true;
        }
        catch (ApiException ex)
        {
            ScoreError = ex.Error.UserMessage;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
