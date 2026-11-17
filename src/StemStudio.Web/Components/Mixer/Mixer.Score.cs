using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Components.Practice;
using StemStudio.Web.Mixer;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Score panel of the mixer page (014 US4, FR-009..FR-013): open/close, follow and bar loops; <c>mixer.score.*</c>.</summary>
public sealed partial class Mixer
{
    private ScorePanel? _score;

    [Inject]
    private PreferencesState PreferencesState { get; set; } = default!;

    private bool ShowScore { get; set; }

    // The user's "Reduced" motion choice makes the follower jump; "System" leaves it to the browser setting.
    private bool? ReducedMotion => PreferencesState.Current.Motion == MotionPreference.Reduced ? true : null;

    private void ToggleScore() => ShowScore = !ShowScore;

    private void RegisterScoreCommands(MixerCommandRegistry commands)
    {
        commands.Add("mixer.score.toggle", () =>
        {
            ToggleScore();
            return Task.CompletedTask;
        });
        commands.Add("mixer.score.follow", () => _score?.ToggleFollowAsync() ?? Task.CompletedTask);
    }

    // Shift+click on bars (Advanced): the loop over the bars, not from a section.
    private async Task LoopBarsAsync(LoopRegionDto region)
    {
        State!.SetLoopStart(region.Start);
        State.SetLoopEnd(region.End);
        await SendLoopAsync(State.LoopEnabled ? State.Loop : State.ToggleLoop());
    }
}
