namespace StemStudio.Api.Worker;

/// <summary>Per-request attempt timeouts of the worker client.</summary>
public static class WorkerTimeouts
{
    /// <summary>Total time allowed for reading the worker's health, retries included (backend contract 1.3.0 <c>GET /api/device</c>).</summary>
    public static readonly TimeSpan Health = TimeSpan.FromSeconds(2);

    /// <summary>Total time allowed for the worker storage report (feature 013; the overview shows a reason instead).</summary>
    public static readonly TimeSpan Storage = TimeSpan.FromSeconds(2);

    /// <summary>Creating a job (POST) may wait for spotDL; everything else is a short poll.</summary>
    /// <param name="request">The outgoing request, if known.</param>
    /// <param name="options">API options.</param>
    /// <returns>The attempt timeout.</returns>
    public static TimeSpan For(HttpRequestMessage? request, StemStudioApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return request?.Method == HttpMethod.Post ? options.WorkerCreateJobTimeout : options.WorkerPollTimeout;
    }
}
