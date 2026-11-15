namespace StemStudio.Api.Worker;

/// <summary>Per-request attempt timeouts of the worker client.</summary>
public static class WorkerTimeouts
{
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
