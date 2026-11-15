namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Base of endpoint tests: a started <see cref="ApiFactory"/> (fake worker, fake clock) and an HTTP client.</summary>
public abstract class ApiTestBase : IDisposable
{
    private protected ApiTestBase() => Client = Factory.CreateClient();

    private protected ApiFactory Factory { get; } = new();

    private protected HttpClient Client { get; }

    private protected FakeWorkerClient Worker => Factory.Worker;

    private protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the client and the factory.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }
}
