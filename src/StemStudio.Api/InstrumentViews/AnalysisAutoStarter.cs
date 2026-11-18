using Microsoft.Extensions.Options;
using StemStudio.Api.Sync;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// The auto rules of data-model 7.3 after a separation: every ready analysis whose inputs changed becomes stale; chords
/// start after the standard separation and drum hits after a detail split (each behind its switch), and a stale chord or
/// drum-hit chart runs again; the reference melody never starts on its own.
/// </summary>
/// <param name="service">The analysis service.</param>
/// <param name="options">Instrument view options.</param>
public sealed class AnalysisAutoStarter(TrackAnalysisService service, IOptions<InstrumentViewsOptions> options) : ITrackCompletionHook, IDetailCompletionHook
{
    /// <inheritdoc />
    public Task OnCompletedAsync(Track track, CancellationToken cancellationToken) => RunAsync(track, AnalysisTrigger.StandardCompleted, cancellationToken);

    /// <inheritdoc />
    public Task OnDetailCompletedAsync(Track track, CancellationToken cancellationToken) => RunAsync(track, AnalysisTrigger.DetailCompleted, cancellationToken);

    private async Task RunAsync(Track track, AnalysisTrigger trigger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        var rules = options.Value.AutoRules;
        foreach (var kind in Enum.GetValues<AnalysisKind>())
        {
            var stale = await service.RefreshStalenessAsync(track, kind, cancellationToken);
            if (AnalysisRules.FollowsTrigger(kind, trigger, rules) || (stale && AnalysisRules.RerunsWhenStale(kind, rules)))
            {
                await service.StartAutomaticallyAsync(track, kind, cancellationToken);
            }
        }
    }
}
