namespace StemStudio.Domain;

/// <summary>Thrown when an aggregate operation receives values that violate domain rules.</summary>
public sealed class DomainValidationException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="errors">The violated rules (at least one).</param>
    public DomainValidationException(IReadOnlyList<DomainError> errors)
        : base(Describe(errors))
    {
        Errors = errors;
    }

    /// <summary>Creates the exception with a single generic error.</summary>
    public DomainValidationException()
        : this([new DomainError(string.Empty, "Invalid value.")])
    {
    }

    /// <summary>Creates the exception with a single error without field.</summary>
    /// <param name="message">The message.</param>
    public DomainValidationException(string message)
        : this([new DomainError(string.Empty, message)])
    {
    }

    /// <summary>Creates the exception with a single error without field.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [new DomainError(string.Empty, message)];
    }

    /// <summary>The violated rules.</summary>
    public IReadOnlyList<DomainError> Errors { get; }

    private static string Describe(IReadOnlyList<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return string.Join("; ", errors.Select(error => $"{error.Field}: {error.Message}"));
    }
}
