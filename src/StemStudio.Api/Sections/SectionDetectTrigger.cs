using Microsoft.Extensions.Options;
using StemStudio.Api.Practice;
using StemStudio.Api.Sync;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.SongSections;

/// <summary>
/// Starts section detection right after a separation completes (data-model 3.3) when
/// <c>StemStudio:Practice:SectionDetectOnComplete</c> is on; a failed submission leaves status <c>none</c>.
/// </summary>
/// <param name="service">The section service.</param>
/// <param name="options">Practice options.</param>
public sealed class SectionDetectTrigger(SectionService service, IOptions<PracticeFlowOptions> options) : ITrackCompletionHook
{
    /// <inheritdoc />
    public async Task OnCompletedAsync(Track track, CancellationToken cancellationToken)
    {
        if (options.Value.SectionDetectOnComplete)
        {
            await service.DetectAutomaticallyAsync(track, cancellationToken);
        }
    }
}
