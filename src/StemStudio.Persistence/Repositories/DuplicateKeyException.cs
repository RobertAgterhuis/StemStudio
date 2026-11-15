namespace StemStudio.Persistence.Repositories;

/// <summary>A unique index was violated (track source key, or mix name per track).</summary>
public sealed class DuplicateKeyException : Exception
{
    /// <summary>Creates the exception.</summary>
    public DuplicateKeyException()
        : base("A record with the same unique key already exists.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">The database error.</param>
    public DuplicateKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
