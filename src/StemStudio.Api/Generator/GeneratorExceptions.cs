using System.Net;

namespace StemStudio.Api.Generator;

/// <summary>The generator cannot be reached, timed out, answered 5xx or sent an unreadable response.</summary>
public class GeneratorUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public GeneratorUnavailableException()
        : base("The generator service is unavailable.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public GeneratorUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public GeneratorUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>No base URL is configured for the service (<c>StemStudio:GeneratorBaseUrl</c> unset).</summary>
public sealed class GeneratorNotInstalledException : GeneratorUnavailableException
{
    /// <summary>Creates the exception.</summary>
    public GeneratorNotInstalledException()
        : base("The generator service is not installed.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public GeneratorNotInstalledException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public GeneratorNotInstalledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A downloaded file does not have the SHA-256 the generator reported.</summary>
public sealed class GeneratorIntegrityException : GeneratorUnavailableException
{
    /// <summary>Creates the exception.</summary>
    public GeneratorIntegrityException()
        : base("The downloaded take is damaged (SHA-256 mismatch).")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public GeneratorIntegrityException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public GeneratorIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The generator refused a request with a 4xx problem.</summary>
public sealed class GeneratorRequestException : Exception
{
    /// <summary>Creates the exception (400).</summary>
    public GeneratorRequestException()
        : this(HttpStatusCode.BadRequest, null, "The generator service rejected the request.")
    {
    }

    /// <summary>Creates the exception (400).</summary>
    /// <param name="detail">Problem detail.</param>
    public GeneratorRequestException(string detail)
        : this(HttpStatusCode.BadRequest, null, detail)
    {
    }

    /// <summary>Creates the exception (400).</summary>
    /// <param name="detail">Problem detail.</param>
    /// <param name="innerException">Cause.</param>
    public GeneratorRequestException(string detail, Exception innerException)
        : base(detail, innerException) => (StatusCode, Detail) = (HttpStatusCode.BadRequest, detail);

    /// <summary>Creates the exception.</summary>
    /// <param name="statusCode">HTTP status.</param>
    /// <param name="code">The problem's <c>code</c>, if known.</param>
    /// <param name="detail">Problem detail (or title).</param>
    public GeneratorRequestException(HttpStatusCode statusCode, GeneratorProblemCode? code, string detail)
        : base(detail) => (StatusCode, Code, Detail) = (statusCode, code, detail);

    /// <summary>HTTP status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The problem code, if the generator sent a known one.</summary>
    public GeneratorProblemCode? Code { get; }

    /// <summary>Readable detail.</summary>
    public string Detail { get; }
}
