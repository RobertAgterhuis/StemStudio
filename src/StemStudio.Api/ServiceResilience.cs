using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace StemStudio.Api;

/// <summary>The resilience settings shared by the worker and generator clients.</summary>
public static class ServiceResilience
{
    /// <summary>
    /// Retries (3, exponential from 200 ms) for GET only (never POST, so a job or upload is never sent twice), a long
    /// attempt timeout for POST and a short one for everything else, a total timeout and a circuit breaker that cover
    /// the longest attempt.
    /// </summary>
    /// <param name="options">The standard resilience options.</param>
    /// <param name="createTimeout">Attempt timeout of POST requests.</param>
    /// <param name="pollTimeout">Attempt timeout of every other request.</param>
    public static void Apply(HttpStandardResilienceOptions options, TimeSpan createTimeout, TimeSpan pollTimeout)
    {
        ArgumentNullException.ThrowIfNull(options);
        var longest = createTimeout + pollTimeout;
        options.Retry.DisableForUnsafeHttpMethods();
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.AttemptTimeout.Timeout = longest;
        options.AttemptTimeout.TimeoutGenerator = args =>
            ValueTask.FromResult(args.Context.GetRequestMessage()?.Method == HttpMethod.Post ? createTimeout : pollTimeout);
        options.TotalRequestTimeout.Timeout = longest * 2;
        options.CircuitBreaker.SamplingDuration = longest * 3;
    }
}
