using StemStudio.Api.Artwork;
using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Metadata;

/// <summary>
/// Applies metadata job states to a track (data-model 3.4): a completed job's result upserts the artist (replaced only
/// once it is 30 days old), offers the Cover Art Archive picture and stores the facts; failures and forgotten jobs fail
/// the lookup with a readable reason.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="artists">Artist repository (same unit of work as the tracks).</param>
/// <param name="artwork">Downloads and offers the matched picture.</param>
/// <param name="clock">Clock.</param>
public sealed class MetadataJobApplier(IWorkerClient worker, IArtistRepository artists, ArtworkDownloader artwork, TimeProvider clock)
{
    /// <summary>Prefix of the failure reason when the worker's artist facts break a value limit.</summary>
    public const string InvalidFactsPrefix = "The band information is invalid";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Records the created metadata job (pending -> searching).</summary>
    /// <param name="track">A track whose lookup is pending.</param>
    /// <param name="job">The created job.</param>
    public void ApplySubmission(Track track, WorkerMetadataJob job)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(job);
        track.StartLookup(job.MetadataJobId, Now);
    }

    /// <summary>Fails a lookup the worker rejected.</summary>
    /// <param name="track">A track whose lookup is pending.</param>
    /// <param name="reason">The worker's problem detail.</param>
    public void ApplyRejection(Track track, string reason)
    {
        ArgumentNullException.ThrowIfNull(track);
        track.FailLookup(reason, Now);
    }

    /// <summary>Applies a polled job state (not saved).</summary>
    /// <param name="track">A track whose lookup is searching.</param>
    /// <param name="job">The job, or null when the worker no longer knows it.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerUnavailableException">The worker became unreachable.</exception>
    public Task ApplyAsync(Track track, WorkerMetadataJob? job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        return job?.Status switch
        {
            null => Fail(track, string.Empty),
            WorkerMetadataJobState.Failed => Fail(track, job.Error ?? string.Empty),
            WorkerMetadataJobState.Completed => CompleteAsync(track, job.MetadataJobId, cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>Deletes the artist a track referenced before when no track references it any more (FR-016).</summary>
    /// <param name="previous">The artist before the change.</param>
    /// <param name="track">The saved track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task RemoveReplacedArtistAsync(Guid? previous, Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (previous is { } artistId && artistId != track.ArtistId)
        {
            await artists.RemoveIfUnusedAsync(artistId, cancellationToken);
        }
    }

    private Task Fail(Track track, string reason)
    {
        track.FailLookup(reason, Now);
        return Task.CompletedTask;
    }

    private async Task CompleteAsync(Track track, string metadataJobId, CancellationToken cancellationToken)
    {
        try
        {
            await ApplyResultAsync(track, metadataJobId, await worker.GetMetadataResultAsync(metadataJobId, cancellationToken), cancellationToken);
        }
        catch (WorkerRequestException rejection)
        {
            track.FailLookup($"Reading the band information failed: {rejection.Detail}", Now);
        }
        catch (DomainValidationException invalid)
        {
            track.FailLookup($"{InvalidFactsPrefix}: {invalid.Message}", Now);
        }
    }

    private async Task ApplyResultAsync(Track track, string metadataJobId, WorkerMetadataResult result, CancellationToken cancellationToken)
    {
        if (result.Outcome == WorkerMetadataOutcome.Found && result.Artist is { } artist)
        {
            await UpsertArtistAsync(MetadataMapper.ToFacts(artist), cancellationToken);
        }

        if (result.Outcome == WorkerMetadataOutcome.Found && result.Artwork is { } picture)
        {
            await artwork.OfferAsync(track, new ArtworkOffer(WorkerArtworkOwner.MetadataJob, metadataJobId, picture), cancellationToken);
        }

        track.CompleteLookup(MetadataMapper.ToResult(result), Now);
    }

    private async Task UpsertArtistAsync(ArtistFacts facts, CancellationToken cancellationToken)
    {
        var stored = await artists.FindAsync(facts.Mbid, cancellationToken);
        if (stored is null)
        {
            artists.Add(Artist.Create(facts));
        }
        else if (stored.IsStale(Now))
        {
            stored.Refresh(facts);
        }
    }
}
