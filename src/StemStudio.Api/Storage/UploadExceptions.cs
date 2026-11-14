namespace StemStudio.Api.Storage;

/// <summary>The upload is larger than 200 MiB (209,715,200 bytes).</summary>
public sealed class UploadTooLargeException : Exception
{
    /// <summary>Creates the exception.</summary>
    public UploadTooLargeException()
        : base("The file is larger than 200 MiB (209,715,200 bytes).")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public UploadTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public UploadTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The upload contains no bytes.</summary>
public sealed class EmptyUploadException : Exception
{
    /// <summary>Creates the exception.</summary>
    public EmptyUploadException()
        : base("The file is empty.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public EmptyUploadException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public EmptyUploadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
