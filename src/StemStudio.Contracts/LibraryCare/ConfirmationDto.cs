namespace StemStudio.Contracts.LibraryCare;

/// <summary>A typed confirmation (<c>Confirmation</c>), e.g. <see cref="LibraryCareLimits.DiscardConfirmation"/> (backend-api 1.12.0, feature 013).</summary>
public sealed record ConfirmationDto
{
    /// <summary>The typed word (&lt;= 16 characters).</summary>
    public required string Confirmation { get; init; }
}
