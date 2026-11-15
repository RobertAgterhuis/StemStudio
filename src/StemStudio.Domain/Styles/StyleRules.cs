namespace StemStudio.Domain.Styles;

/// <summary>Field limits of a style (data-model 3.1); the imitation guard is checked separately (<see cref="StyleTextGuard"/>).</summary>
public static class StyleRules
{
    /// <summary>Longest name.</summary>
    public const int MaxNameLength = 60;

    /// <summary>Longest sub-genre.</summary>
    public const int MaxSubGenreLength = 40;

    /// <summary>Most tags per list.</summary>
    public const int MaxTags = 8;

    /// <summary>Longest tag.</summary>
    public const int MaxTagLength = 30;

    /// <summary>Longest description.</summary>
    public const int MaxDescriptionLength = 300;

    /// <summary>Checks the limits of normalised fields.</summary>
    /// <param name="fields">The fields (see <see cref="StyleFields.Normalized"/>).</param>
    /// <returns>The errors; empty when valid.</returns>
    public static IReadOnlyList<DomainError> Validate(StyleFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var errors = new ValidationErrors();
        errors.Check(fields.Name.Length is > 0 and <= MaxNameLength, "name", "The name must be 1 to 60 characters.");
        errors.Check((fields.SubGenre?.Length ?? 0) <= MaxSubGenreLength, "subGenre", "The sub-genre must be at most 40 characters.");
        errors.Check(ValidTags(fields.Instrumentation), "instrumentation", TagMessage);
        errors.Check(ValidTags(fields.Moods), "moods", TagMessage);
        errors.Check((fields.Description?.Length ?? 0) <= MaxDescriptionLength, "description", "The description must be at most 300 characters.");
        return errors.Errors;
    }

    private const string TagMessage = "Use at most 8 tags of 1 to 30 characters each.";

    private static bool ValidTags(IReadOnlyList<string> tags) => tags.Count <= MaxTags && tags.All(tag => tag.Length is > 0 and <= MaxTagLength);
}
