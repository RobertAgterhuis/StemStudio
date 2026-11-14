namespace StemStudio.Web.Api;

/// <summary>Thrown by <see cref="IStemStudioApiClient"/> when a backend call fails.</summary>
public sealed class ApiException : Exception
{
    /// <summary>Creates an exception for an unspecified server error.</summary>
    public ApiException()
        : this(ApiError.FromStatus(System.Net.HttpStatusCode.InternalServerError))
    {
    }

    /// <summary>Creates an exception with a custom message.</summary>
    /// <param name="message">The message.</param>
    public ApiException(string message)
        : this(message, null)
    {
    }

    /// <summary>Creates an exception with a custom message and cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ApiException(string message, Exception? innerException)
        : base(message, innerException) =>
        Error = ApiError.FromStatus(System.Net.HttpStatusCode.InternalServerError) with { Title = message };

    /// <summary>Creates an exception for a mapped API error.</summary>
    /// <param name="error">The mapped error.</param>
    /// <param name="innerException">The transport failure, if any.</param>
    public ApiException(ApiError error, Exception? innerException = null)
        : base(error.UserMessage, innerException) =>
        Error = error;

    /// <summary>The mapped error.</summary>
    public ApiError Error { get; }
}
