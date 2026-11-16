using StemStudio.Persistence;

namespace StemStudio.Api.Agent.Stores;

/// <summary>
/// Runs one store operation on a fresh <see cref="StemStudioDbContext"/> in its own scope, so the agent ports can be singletons
/// that are used from long-running runs (feature 011).
/// </summary>
/// <param name="scopes">The scope factory.</param>
public sealed class AgentDb(IServiceScopeFactory scopes)
{
    /// <summary>Runs an operation with a context.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <returns>The result.</returns>
    public async Task<T> WithAsync<T>(Func<StemStudioDbContext, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using var scope = scopes.CreateAsyncScope();
        return await operation(scope.ServiceProvider.GetRequiredService<StemStudioDbContext>());
    }

    /// <summary>Runs an operation with a context and saves its changes.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task SaveAsync(Func<StemStudioDbContext, Task> operation, CancellationToken cancellationToken) => WithAsync(async context =>
    {
        await operation(context);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    });
}
