using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Lyrics;

/// <summary>Loads the track's lyrics, polls them while they are searched or aligned and runs the card's actions (feature 006).</summary>
public sealed partial class LyricsSection
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The completed track.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    private LyricsDto? Lyrics { get; set; }

    private string? LoadError { get; set; }

    private string? ActionError { get; set; }

    private bool Busy { get; set; }

    private bool HasText => Lyrics!.Lines.Count > 0;

    private bool IsBusyState => Lyrics!.Status is LyricsStatusDto.Searching or LyricsStatusDto.Aligning;

    private bool CanPaste => !IsBusyState;

    private bool ShowsPreview => Lyrics!.Status == LyricsStatusDto.AlignPending || (Lyrics.Status == LyricsStatusDto.Failed && HasText);

    private bool ShowsSplitHint => Lyrics!.Status == LyricsStatusDto.Failed && HasText && !VocalsAreSplit;

    private bool VocalsAreSplit => DetailPhaseText.SplitParents(Track).Any(p => p.Parent == "vocals");

    private string? Reason => Lyrics!.Status is LyricsStatusDto.Failed or LyricsStatusDto.AlignPending ? null : Lyrics.Reason;

    private string? ErrorText => LoadError ?? ActionError ?? (Lyrics?.Status == LyricsStatusDto.Failed ? Lyrics.Reason : null);

    /// <inheritdoc />
    protected override bool NeedsPolling => Lyrics is not null && (IsBusyState || AwaitsAlignDecision);

    /// <summary>Pending without an estimate: the backend has not yet decided between aligning now (GPU) and the CPU estimate.</summary>
    private bool AwaitsAlignDecision => Lyrics!.Status == LyricsStatusDto.AlignPending && StemStudio.Web.Lyrics.LyricsText.Estimate(Lyrics) is null;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync(Stopping);
        StartPolling();
    }

    /// <inheritdoc />
    protected override async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Lyrics = await Api.GetLyricsAsync(Track.Id, ct) ?? new LyricsDto { Status = LyricsStatusDto.None };
            LoadError = null;
        }
        catch (ApiException ex)
        {
            LoadError = ex.Error.UserMessage;
        }
    }
}
