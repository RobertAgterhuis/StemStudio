using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Singing feedback in the mixer page (016 US4, FR-042, FR-044): the lead vocal is muted while the feedback runs and
/// restored afterwards; the last unit score stands next to the karaoke banner (<c>karaoke-score</c>).
/// </summary>
public sealed partial class Mixer
{
    private LeadMute? _leadMute;
    private bool _leadExpanded;

    /// <summary>The last unit score of singing feedback, or null.</summary>
    private ScoredUnit? LastScore { get; set; }

    /// <summary>What "Score this take" needs from this page.</summary>
    private SingingSource? SingingSource => _track is null ? null : new SingingSource(_track, State?.SectionSet?.Sections, State?.Duration ?? 0, State?.Practice.TransposeSemitones ?? 0);

    /// <summary>Opens a family view in the <c>instrument-view</c> region ("Practise as" of US6, Drumless).</summary>
    /// <param name="view">The view.</param>
    internal void OpenView(InstrumentViewDto view)
    {
        OpenedView = view;
        StateHasChanged();
    }

    private Task OnSingingRunningAsync(bool running) => running ? MuteLeadAsync() : RestoreLeadAsync();

    // The vocals group is expanded first, so only the lead detail is muted (FR-044); when the engine cannot expand it the
    // parent is muted instead. Both are undone when the feedback stops.
    private async Task MuteLeadAsync()
    {
        if (_leadMute is null)
        {
            _leadExpanded = await ExpandForViewAsync(StemNames.Vocals);
            _leadMute = SingingMix.MuteLead(State!);
        }

        await SendChannelAsync(_leadMute.Channel);
    }

    private async Task RestoreLeadAsync()
    {
        if (_leadMute is { } mute)
        {
            _leadMute = null;
            foreach (var channel in SingingMix.Restore(State!, mute.Flags))
            {
                await SendChannelAsync(channel);
            }

            await CollapseAfterViewAsync(StemNames.Vocals, _leadExpanded);
            _leadExpanded = false;
        }
    }

    private void OnUnitScored(ScoredUnit? score) => LastScore = score;

    private async Task OpenInputSettingsAsync()
    {
        if (Recording is { PanelOpen: false } session)
        {
            await session.TogglePanelAsync();
        }
    }
}
