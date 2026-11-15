namespace StemStudio.Api.Sync;

/// <summary>One synchronisation pass over a list of items where one broken item never stops the others.</summary>
internal static class TrackPass
{
    /// <summary>Runs <paramref name="sync"/> for each item; an unexpected failure is logged and the pass continues.</summary>
    /// <typeparam name="T">Item type (track or library item).</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="idOf">The item's ID for the log.</param>
    /// <param name="sync">Synchronises one item.</param>
    /// <param name="logFailure">Logs an unexpected failure of one item.</param>
    /// <returns>A task.</returns>
    public static async Task RunAsync<T>(IEnumerable<T> items, Func<T, Guid> idOf, Func<T, Task> sync, Action<Guid, Exception> logFailure)
    {
        foreach (var item in items)
        {
            try
            {
                await sync(item);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logFailure(idOf(item), exception);
            }
        }
    }
}
