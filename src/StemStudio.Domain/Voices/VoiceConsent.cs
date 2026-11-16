using System.Security.Cryptography;
using System.Text;

namespace StemStudio.Domain.Voices;

/// <summary>The current consent statement (010 data-model 3.3, embedded <c>consent.v1.*.txt</c>).</summary>
public static class ConsentStatement
{
    /// <summary>The current version.</summary>
    public const string Version = "consent.v1";

    /// <summary>The English statement (the text the hash is taken of).</summary>
    public static string Text { get; } = EmbeddedText.Read("consent.v1.en.txt").Trim();

    /// <summary>The Dutch translation shown alongside.</summary>
    public static string TextNl { get; } = EmbeddedText.Read("consent.v1.nl.txt").Trim();

    /// <summary>SHA-256 of the UTF-8 English statement (64 lower-case hex characters).</summary>
    public static string Sha256 { get; } = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));
}

/// <summary>The consent record of a voice profile.</summary>
/// <param name="StatementVersion">The statement version agreed to.</param>
/// <param name="StatementSha256">SHA-256 of that statement text.</param>
/// <param name="PersonName">The name typed by the person (1-80).</param>
/// <param name="ConsentedAt">When (UTC).</param>
public sealed record VoiceConsent(string StatementVersion, string StatementSha256, string PersonName, DateTime ConsentedAt)
{
    /// <summary>Always true: a profile exists only with confirmed consent.</summary>
    public bool Confirmed { get; } = true;
}

/// <summary>Why consent was refused.</summary>
public enum ConsentProblem
{
    /// <summary>Consent given.</summary>
    None,

    /// <summary>Not confirmed.</summary>
    Required,

    /// <summary>An older or unknown statement version.</summary>
    Outdated,

    /// <summary>Other invalid fields (person name).</summary>
    Invalid,
}

/// <summary>The outcome of a consent check.</summary>
/// <param name="Problem">The problem, or none.</param>
/// <param name="Consent">The record when given.</param>
/// <param name="Errors">Field errors.</param>
public sealed record ConsentOutcome(ConsentProblem Problem, VoiceConsent? Consent, IReadOnlyList<DomainError> Errors);

/// <summary>Consent rules (ADR-019 D3): confirmed, the current statement, a person name of 1-80 characters.</summary>
public static class ConsentRules
{
    /// <summary>Longest person name.</summary>
    public const int MaxPersonNameLength = 80;

    /// <summary>Checks the consent fields in order.</summary>
    /// <param name="confirmed">The checkbox.</param>
    /// <param name="statementVersion">The version the form showed.</param>
    /// <param name="personName">The typed name.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The outcome.</returns>
    public static ConsentOutcome Check(bool confirmed, string? statementVersion, string? personName, DateTime nowUtc)
    {
        var name = (personName ?? string.Empty).Trim();
        return (confirmed, statementVersion == ConsentStatement.Version, name.Length is > 0 and <= MaxPersonNameLength) switch
        {
            (false, _, _) => Refused(ConsentProblem.Required, "consentConfirmed", "Confirm that this is your own voice and that you agree to the statement."),
            (_, false, _) => Refused(ConsentProblem.Outdated, "consentStatementVersion", "The consent statement was updated; read and confirm the current version."),
            (_, _, false) => Refused(ConsentProblem.Invalid, "personName", "Type your name (1 to 80 characters)."),
            _ => new ConsentOutcome(ConsentProblem.None, new VoiceConsent(ConsentStatement.Version, ConsentStatement.Sha256, name, nowUtc), []),
        };
    }

    private static ConsentOutcome Refused(ConsentProblem problem, string field, string message) => new(problem, null, [new DomainError(field, message)]);
}
