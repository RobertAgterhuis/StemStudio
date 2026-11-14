namespace StemStudio.Api.Sync;

/// <summary>One synchronisation pass over every active track (FR-008).</summary>
public interface ITrackSynchronizer
{
    /// <summary>Polls the worker for each non-terminal track with a job and applies the reported state.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SyncAllAsync(CancellationToken cancellationToken);
}
