using Microsoft.AspNetCore.Components;
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

    private Task ToggleDrumlessAsync() => DrumlessOn ? RestoreDrumsAsync() : ApplyDrumlessAsync();

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
    }

    private async Task SetGuideAsync(DrumPieceDto? piece)
    {
        await (DrumlessOn ? Task.CompletedTask : ApplyDrumlessAsync());
        GuidePiece = piece;
        foreach (var channel in Drumless.Guide(State!, piece))
        {
            await SendChannelAsync(channel);
        }
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
