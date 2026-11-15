namespace StemStudio.Agent.Ports;

/// <summary>A 409 case of the agent API (run-active, approval-not-pending, arguments-changed, nothing-to-undo, run-ended).</summary>
public sealed class AgentConflictException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="problemCode">Code from <c>AgentProblemTypes</c>.</param>
    /// <param name="message">Readable message.</param>
    public AgentConflictException(string problemCode, string message)
        : base(message) => ProblemCode = problemCode;

    /// <summary>Creates an exception with the code <c>conflict</c>.</summary>
    public AgentConflictException()
        : this("conflict", "The request conflicts with the current state.")
    {
    }

    /// <summary>Creates an exception with the code <c>conflict</c> and a message.</summary>
    /// <param name="message">Readable message.</param>
    public AgentConflictException(string message)
        : this("conflict", message)
    {
    }

    /// <summary>Creates an exception with the code <c>conflict</c>, a message and the cause.</summary>
    /// <param name="message">Readable message.</param>
    /// <param name="innerException">Cause.</param>
    public AgentConflictException(string message, Exception innerException)
        : base(message, innerException) => ProblemCode = "conflict";

    /// <summary>Problem code from <c>AgentProblemTypes</c>.</summary>
    public string ProblemCode { get; }
}
