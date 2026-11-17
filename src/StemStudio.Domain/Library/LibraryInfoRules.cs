namespace StemStudio.Domain.Library;

/// <summary>Validation of library info (feature 022, FR-031): each name trimmed, 1..200 characters, no control characters.</summary>
public static class LibraryInfoRules
{
    /// <summary>Longest name.</summary>
    public const int MaxNameLength = 200;

    /// <summary>The field message of an invalid name.</summary>
    public const string Message = "Enter a name of 1 to 200 characters without control characters, or leave it automatic.";

    /// <summary>Validates and trims the names.</summary>
    /// <param name="info">Requested names (null members = automatic).</param>
    /// <returns>The trimmed names.</returns>
    /// <exception cref="DomainValidationException">A name is blank, too long or has control characters (fields <c>artist</c>, <c>album</c>, <c>song</c>).</exception>
    public static LibraryInfo Validate(LibraryInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var errors = new ValidationErrors();
        var result = new LibraryInfo
        {
            Artist = Check(errors, "artist", info.Artist),
            Album = Check(errors, "album", info.Album),
            Song = Check(errors, "song", info.Song),
        };
        errors.ThrowIfAny();
        return result;
    }

    private static string? Check(ValidationErrors errors, string field, string? value)
    {
        var trimmed = value?.Trim();
        errors.Check(trimmed is null || (trimmed.Length is > 0 and <= MaxNameLength && !trimmed.Any(char.IsControl)), field, Message);
        return trimmed;
    }
}
