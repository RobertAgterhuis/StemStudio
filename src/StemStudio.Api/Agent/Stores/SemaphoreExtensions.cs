namespace StemStudio.Api.Agent.Stores;

/// <summary>Runs a body while holding a <see cref="SemaphoreSlim"/> of count 1.</summary>
internal static class SemaphoreExtensions
{
    /// <summary>Waits for the semaphore, runs the body and releases the semaphore, also when the body throws.</summary>
    /// <typeparam name="T">Result of the body.</typeparam>
    /// <param name="gate">The semaphore.</param>
    /// <param name="body">The body.</param>
    /// <param name="cancellationToken">Cancellation of the wait.</param>
    /// <returns>The result of the body.</returns>
    public static async Task<T> RunExclusiveAsync<T>(this SemaphoreSlim gate, Func<Task<T>> body, CancellationToken cancellationToken)
    {
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
