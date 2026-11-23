using System.Text.RegularExpressions;

namespace StemStudio.Domain;

/// <summary>Worker job identifiers: 32 lower-case hex characters.</summary>
public static partial class JobIds
{
    /// <summary>Returns the ID when it is a worker job ID.</summary>
    /// <param name="jobId">The ID.</param>
    /// <param name="parameterName">Parameter name for the exception.</param>
    /// <returns>The ID.</returns>
    /// <exception cref="ArgumentException">The ID is not a worker job ID.</exception>
    public static string Require(string jobId, string parameterName) =>
        Pattern().IsMatch(jobId ?? string.Empty) ? jobId! : throw new ArgumentException($"'{jobId}' is not a worker job ID.", parameterName);

    [GeneratedRegex(@"\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
