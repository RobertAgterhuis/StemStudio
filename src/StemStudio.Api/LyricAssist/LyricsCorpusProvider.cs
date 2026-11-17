using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Keeps the in-memory corpus index of the library's 006 lyrics (research R-07): built at first use and rebuilt whenever the lyrics
/// rows change (their count or newest change time), so it follows lyrics lookups, pastes and track deletions.
/// </summary>
/// <param name="scopes">Scope factory (the source is scoped).</param>
public sealed class LyricsCorpusProvider(IServiceScopeFactory scopes) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private (int Count, DateTime? Newest)? _stamp;

    /// <summary>The last built index (empty before the first refresh).</summary>
    public ILyricsCorpus Current { get; private set; } = LyricsCorpusIndex.Empty;

    /// <summary>The index of the current library lyrics.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The index.</returns>
    public async Task<ILyricsCorpus> RefreshAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<ILyricsCorpusSource>();
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var stamp = await source.StampAsync(cancellationToken);
            if (stamp != _stamp)
            {
                Current = new LyricsCorpusIndex(await source.LoadAsync(cancellationToken));
                _stamp = stamp;
            }

            return Current;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();
}
