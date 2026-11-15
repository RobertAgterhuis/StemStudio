using StemStudio.Domain;

namespace StemStudio.Api;

/// <summary>Parsing of optional query values with field errors.</summary>
public static class QueryParsing
{
    /// <summary>An enum by its name (case-insensitive; numbers are refused); null when absent or invalid.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="value">The query value.</param>
    /// <param name="field">The field name for the error.</param>
    /// <param name="errors">Collected errors.</param>
    /// <returns>The value or null.</returns>
    public static T? Enum<T>(string? value, string field, List<DomainError> errors)
        where T : struct, System.Enum
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is null)
        {
            return null;
        }

        var valid = System.Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && !int.TryParse(value, out _);
        if (!valid)
        {
            errors.Add(new DomainError(field, $"'{value}' is not a valid {field}."));
        }

        return valid ? parsed : null;
    }
}
