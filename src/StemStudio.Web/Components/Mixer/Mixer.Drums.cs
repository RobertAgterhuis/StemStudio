using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// "Drumless" and the drum trainer of the mixer page (016 US3, FR-032, FR-033): the toggle stores what it changed in the
/// browser (data-model 8) so a second click, also after a reload, restores it; the guide piece keeps one drum detail
/// channel audible on top of the drumless mix.
/// </summary>
public sealed partial class Mixer
{
    private DrumlessSnapshot? _drumless;
    private bool _guideExpanded;
    private bool _drumBusy;

    [Inject]
    private ViewPreferences ViewPreferences { get; set; } = default!;

    /// <summary>The open family view of the <c>instrument-view</c> region, or null.</summary>
    private InstrumentViewDto? OpenedView { get; set; }

    private DrumPieceDto? GuidePiece { get; set; }

    private bool DrumlessOn => _drumless is not null;

    private IReadOnlyList<DrumPieceDto> DrumGuidePieces => Drumless.GuidePieces(State!);

    // One engine score follower: while the trainer is open its drum score takes the place of the mixer's score panel.
    private bool MixerScoreShown => ShowScore && OpenedView is not (InstrumentViewDto.DrumTrainer or InstrumentViewDto.Tablature or InstrumentViewDto.Part);

    private string DrumlessKey => ViewPreferences.DrumlessKey(Id);

    /// <summary>Takes over a stored Drumless while the drums are still muted (a saved mix); drops it otherwise.</summary>
    private async Task LoadDrumlessAsync()
    {
        var stored = await ViewPreferences.GetAsync<DrumlessSnapshot>(DrumlessKey);
        if (stored is not { Flags: not null })
        {
            return;
        }

        _drumless = Drumless.IsApplied(State!) ? stored : null;
        await (_drumless is null ? ViewPreferences.RemoveAsync(DrumlessKey) : Task.CompletedTask);
    }

    private Task ToggleDrumlessAsync() => OneDrumChangeAsync(() => DrumlessOn ? RestoreDrumsAsync() : ApplyDrumlessAsync());

    private Task SetGuideAsync(DrumPieceDto? piece) => OneDrumChangeAsync(() => ApplyGuideAsync(piece));

    // One Drumless or guide change at a time (review L2): a click while the engine is still receiving the previous change
    // is ignored, so the engine never ends half-muted against the state.
    private async Task OneDrumChangeAsync(Func<Task> change)
    {
        if (!_drumBusy)
        {
            _drumBusy = true;
            try
            {
                await change();
            }
            finally
            {
                _drumBusy = false;
            }
        }
    }

    private async Task ApplyDrumlessAsync()
    {
        _drumless = Drumless.Capture(State!);
        await ViewPreferences.SetAsync(DrumlessKey, _drumless);
        await SendDrumlessAsync(Drumless.Apply(State!, TrackBpm is not null));
        OpenedView = InstrumentViewDto.DrumTrainer;
    }

    private async Task RestoreDrumsAsync()
    {
        var change = Drumless.Restore(State!, _drumless!);
        (_drumless, GuidePiece) = (null, null);
        await ViewPreferences.RemoveAsync(DrumlessKey);
        await SendDrumlessAsync(change);
        await CollapseGuideAsync();
    }

    // A guide piece is a detail channel: the drums group is expanded while a guide is active, so the piece is on the
    // audible level (FR-033), and collapsed again when the guide is cleared or Drumless ends.
    private async Task ApplyGuideAsync(DrumPieceDto? piece)
    {
        await (DrumlessOn ? Task.CompletedTask : ApplyDrumlessAsync());
        GuidePiece = piece;
        _guideExpanded |= piece is not null && await ExpandForViewAsync(StemNames.Drums);
        foreach (var channel in Drumless.Guide(State!, piece))
        {
            await SendChannelAsync(channel);
        }

        await (piece is null ? CollapseGuideAsync() : Task.CompletedTask);
    }

    private async Task CollapseGuideAsync()
    {
        await CollapseAfterViewAsync(StemNames.Drums, _guideExpanded);
        _guideExpanded = false;
    }

    private async Task SendDrumlessAsync(DrumlessChange change)
    {
        foreach (var channel in change.Channels)
        {
            await SendChannelAsync(channel);
        }

        await SendMetronomeAsync(change.Metronome);
        await (change.Practice is { } practice ? SubmitPracticeAsync(practice) : Task.CompletedTask);
    }
}
