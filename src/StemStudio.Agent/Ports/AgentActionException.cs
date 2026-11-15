namespace StemStudio.Agent.Ports;

/// <summary>
/// A StemStudio action failed with a problem response. The message is readable and safe to show to the model and the user:
/// no stack traces, internal URLs, file paths or secrets (T034).
/// </summary>
public sealed class AgentActionException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="statusCode">HTTP status of the problem response (0 when the API could not be reached).</param>
    /// <param name="problemCode">Problem code (last segment of the problem <c>type</c>), when any.</param>
    /// <param name="message">Readable message.</param>
    public AgentActionException(int statusCode, string? problemCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ProblemCode = problemCode;
    }

    /// <summary>Creates an exception with status 0 (API not reachable).</summary>
    public AgentActionException()
        : this(0, null, "StemStudio could not be reached.")
    {
    }

    /// <summary>Creates an exception with status 0 and a message.</summary>
    /// <param name="message">Readable message.</param>
    public AgentActionException(string message)
        : this(0, null, message)
    {
    }

    /// <summary>Creates an exception with status 0, a message and the cause.</summary>
    /// <param name="message">Readable message.</param>
    /// <param name="innerException">Cause (never shown to the model).</param>
    public AgentActionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>HTTP status (0 when the API could not be reached).</summary>
    public int StatusCode { get; }

    /// <summary>Problem code, e.g. <c>not-found</c> or <c>conflict</c>, when the response carried one.</summary>
    public string? ProblemCode { get; }

    /// <summary>True for 404: the id does not exist (provenance of ids, research R-05).</summary>
    public bool IsNotFound => StatusCode == 404;
}
