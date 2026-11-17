namespace StemStudio.Contracts.LibraryCare;

/// <summary>Options of a restore (<c>RestoreOptions</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record RestoreOptionsDto
{
    /// <summary><c>replace</c> (also an empty library) or <c>merge</c> (P3).</summary>
    public required RestoreModeDto Mode { get; init; }

    /// <summary>False = no voice profile is restored (converted takes keep the name snapshot); default true.</summary>
    public bool VoiceProfiles { get; init; } = true;

    /// <summary>Must be <see cref="LibraryCareLimits.ReplaceConfirmation"/> for <c>replace</c> on a non-empty library.</summary>
    public string? Confirmation { get; init; }
}
