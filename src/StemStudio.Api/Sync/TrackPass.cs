using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Sync;

/// <summary>One synchronisation pass over a list of tracks where one broken track never stops the others.</summary>
internal static class TrackPass
{
    /// <summary>Runs <paramref name="sync"/> for each track; an unexpected failure is logged and the pass continues.</summary>
    /// <param name="tracks">The tracks.</param>
    /// <param name="sync">Synchronises one track.</param>
    /// <param name="logFailure">Logs an unexpected failure of one track.</param>
    /// <returns>A task.</returns>
    public static async Task RunAsync(IEnumerable<Track> tracks, Func<Track, Task> sync, Action<Guid, Exception> logFailure)
    {
        foreach (var track in tracks)
        {
            try
            {
                await sync(track);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logFailure(track.Id, exception);
            }
        }
    }
}
