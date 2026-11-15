using StemStudio.Api.Artwork;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Sync;

/// <summary>
/// Applies what a standard job reports with any state: the device (feature 003), title and artist, and the source
/// picture (feature 005). Never fails the track.
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
            track.ApplyMetadata(new SourceMetadata(metadata.Title, metadata.Artist), clock.GetUtcNow().UtcDateTime);
        }

        if (job.Artwork is { } info)
        {
            await artwork.OfferAsync(track, new ArtworkOffer(WorkerArtworkOwner.Job, job.JobId, info), cancellationToken);
        }
    }
}
