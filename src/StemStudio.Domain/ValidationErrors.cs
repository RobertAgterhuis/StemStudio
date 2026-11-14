using System.Globalization;

namespace StemStudio.Domain;

/// <summary>Inclusive numeric range with a user-facing message.</summary>
/// <param name="Min">Lowest allowed value.</param>
/// <param name="Max">Highest allowed value.</param>
internal readonly record struct Bounds(double Min, double Max)
{
    /// <summary>Range 0..1 (volumes, confidence).</summary>
    public static readonly Bounds Unit = new(0, 1);

    /// <summary>The user-facing message.</summary>
    public string Message => string.Create(CultureInfo.InvariantCulture, $"Must be between {Min} and {Max}.");

    /// <summary>True when the value lies within the range (NaN is outside).</summary>
    /// <param name="value">Value to test.</param>
    /// <returns>True when valid.</returns>
    public bool Contains(double value) => value >= Min && value <= Max;
}

/// <summary>Collects <see cref="DomainError"/>s with JSON-style field paths.</summary>
internal sealed class ValidationErrors
{
    private readonly List<DomainError> _errors;
    private readonly string _prefix;

    /// <summary>Creates an empty collector.</summary>
    /// <param name="prefix">Path prefix for every field, or empty.</param>
    public ValidationErrors(string prefix = "")
        : this([], prefix)
    {
    }

    private ValidationErrors(List<DomainError> errors, string prefix)
    {
        _errors = errors;
        _prefix = prefix;
    }

    /// <summary>The collected errors.</summary>
    public IReadOnlyList<DomainError> Errors => _errors;

    /// <summary>A collector writing into the same list under a nested path.</summary>
    /// <param name="field">Nested field name, e.g. <c>metronome</c> or <c>channels[0]</c>.</param>
    /// <returns>The nested collector.</returns>
    public ValidationErrors Nested(string field) => new(_errors, Path(field));

    /// <summary>Adds an error unless the value is within the bounds.</summary>
    /// <param name="field">Field name.</param>
    /// <param name="value">Value.</param>
    /// <param name="bounds">Allowed range.</param>
    public void Range(string field, double value, Bounds bounds) => Check(bounds.Contains(value), field, bounds.Message);

    /// <summary>Adds an error unless the condition holds.</summary>
    /// <param name="valid">The rule outcome.</param>
    /// <param name="field">Field name.</param>
    /// <param name="message">Message when the rule is violated.</param>
    public void Check(bool valid, string field, string message)
    {
        if (!valid)
        {
            _errors.Add(new DomainError(Path(field), message));
        }
    }

    /// <summary>Throws a <see cref="DomainValidationException"/> when any error was collected.</summary>
    public void ThrowIfAny()
    {
        if (_errors.Count > 0)
        {
            throw new DomainValidationException(_errors.ToList());
        }
    }

    private string Path(string field) => _prefix.Length == 0 ? field : $"{_prefix}.{field}";
}
