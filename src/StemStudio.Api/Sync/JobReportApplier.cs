using StemStudio.Api.Artwork;
using StemStudio.Api.Metadata;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Sync;

/// <summary>
/// Applies what a standard job reports with any state: the device (feature 003), title and artist, and the source
/// picture and the scheduling of the online lookup (feature 005). Never fails the track.
/// </summary>
/// <param name="devices">Copies the job's device.</param>
/// <param name="artwork">Downloads and offers the source picture.</param>
/// <param name="clock">Clock.</param>
public sealed class JobReportApplier(JobDeviceApplier devices, ArtworkDownloader artwork, TimeProvider clock)
{
    /// <summary>Applies the reported data to the track (not saved).</summary>
    /// <param name="track">The track.</param>
    /// <param name="job">The worker job.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task ApplyAsync(Track track, WorkerJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(job);
        devices.ApplyStandard(track, job.Device);
        if (job.Metadata is { } metadata)
        {
            ApplyMetadata(track, metadata);
        }

        if (job.Artwork is { } info)
        {
            await artwork.OfferAsync(track, new ArtworkOffer(WorkerArtworkOwner.Job, job.JobId, info), cancellationToken);
        }
    }

    /// <summary>Copies title and artist (an upload's tag artist when none is reported); once the duration is known the online lookup (FR-003, ADR-014 D3) and the lyrics search (006 FR-001) are scheduled.</summary>
    private void ApplyMetadata(Track track, WorkerSourceMetadata metadata)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        track.ApplyMetadata(MetadataMapper.ToSource(track, metadata), now);
        if (metadata.DurationSeconds is not null)
        {
            track.ScheduleLookup(MetadataMapper.ToQuery(track, metadata), now);
            track.ScheduleLyricsLookup(now);
        }
    }
}
