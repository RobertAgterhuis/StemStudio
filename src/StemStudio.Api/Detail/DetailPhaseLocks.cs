using System.Collections.Concurrent;

namespace StemStudio.Api.Detail;

/// <summary>
/// One lock per track around every read-change-save of its detail phase (singleton): the <see cref="DetailSynchronizer"/>
/// step of a track and the start and cancel requests of <see cref="DetailService"/> never interleave, so a cancel that
/// arrives while a pending phase is submitted to the worker is neither lost nor leaves the worker job running.
/// </summary>
/// <remarks>A lock per track that ever had a detail phase stays in memory; a local library has few tracks.</remarks>
public sealed class DetailPhaseLocks
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <summary>Runs <paramref name="body"/> while holding the lock of <paramref name="trackId"/>.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="trackId">The track.</param>
    /// <param name="body">Reads, changes and saves the track.</param>
    /// <param name="cancellationToken">Cancellation of the wait.</param>
    /// <returns>The result of the body.</returns>
    public async Task<T> RunAsync<T>(Guid trackId, Func<Task<T>> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var gate = _locks.GetOrAdd(trackId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await body();
        }
        finally
        {
            gate.Release();
        }
    }
}
