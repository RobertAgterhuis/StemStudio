using System.Net;

namespace StemStudio.Api.Worker;

/// <summary>The worker cannot be reached, timed out, answered 5xx or sent an unreadable response.</summary>
public sealed class WorkerUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public WorkerUnavailableException()
        : base("The processing service is unavailable.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public WorkerUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public WorkerUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The worker rejected a request with a 4xx problem.</summary>
public sealed class WorkerRequestException : Exception
{
    /// <summary>Creates the exception (400).</summary>
    public WorkerRequestException()
        : this(HttpStatusCode.BadRequest, "The processing service rejected the request.")
    {
    }

    /// <summary>Creates the exception (400).</summary>
    /// <param name="detail">Problem detail.</param>
    public WorkerRequestException(string detail)
        : this(HttpStatusCode.BadRequest, detail)
    {
    }

    /// <summary>Creates the exception (400).</summary>
    /// <param name="detail">Problem detail.</param>
    /// <param name="innerException">Cause.</param>
    public WorkerRequestException(string detail, Exception innerException)
        : base(detail, innerException)
    {
        (StatusCode, Detail) = (HttpStatusCode.BadRequest, detail);
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="statusCode">HTTP status of the worker answer.</param>
    /// <param name="detail">Problem detail (or title).</param>
    public WorkerRequestException(HttpStatusCode statusCode, string detail)
        : base(detail)
    {
        (StatusCode, Detail) = (statusCode, detail);
    }

    /// <summary>HTTP status of the worker answer.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Readable problem detail from the worker.</summary>
    public string Detail { get; }
}

/// <summary>
/// A worker file (score view page, setlist PDF) is larger than the API accepts (014 security hardening): the download stops at
/// the cap, so nothing above it is ever buffered or stored.
/// </summary>
public sealed class WorkerResponseTooLargeException : Exception
{
    /// <summary>Creates the exception.</summary>
    public WorkerResponseTooLargeException()
        : base("The processing service sent a file above the size cap.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public WorkerResponseTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public WorkerResponseTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
