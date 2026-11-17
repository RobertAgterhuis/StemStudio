using StemStudio.Domain.Library;

namespace StemStudio.Domain.Tracks;

/// <summary>Library info of a track: the user's names used to file all its items (feature 022, data-model 2.9).</summary>
public sealed partial class Track
{
    /// <summary>The user's names for every item of the track (default all automatic).</summary>
    public LibraryInfo LibraryInfo { get; private set; } = LibraryInfo.Automatic;

    /// <summary>Sets the user's names (validated, trimmed); the caller re-files the track's items.</summary>
    /// <param name="info">Names (null members = automatic).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">A name breaks a rule.</exception>
    public void SetLibraryInfo(LibraryInfo info, DateTime nowUtc) => (LibraryInfo, UpdatedAt) = (LibraryInfoRules.Validate(info), nowUtc);
}
