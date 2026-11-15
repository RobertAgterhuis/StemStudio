using StemStudio.Contracts;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;

namespace StemStudio.Web.Styles;

/// <summary>
/// The fields of the style editor (009 FR-004, FR-005, data-model 3.1): name 1-60, sub-genre at most 40, description at
/// most 300, at most 8 tags of at most 30 characters per group. The API checks everything again, including the imitation
/// guard.
/// </summary>
public sealed class StyleEditorForm
{
    /// <summary>Field name of the mood tags.</summary>
    public const string MoodsField = "moods";

    /// <summary>Field name of the instrument tags.</summary>
    public const string InstrumentsField = "instrumentation";

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Genre family.</summary>
    public GenreFamilyDto Genre { get; set; } = GenreFamilyDto.Rock;

    /// <summary>Sub-genre ("" for none).</summary>
    public string SubGenre { get; set; } = string.Empty;

    /// <summary>Tempo feel.</summary>
    public TempoFeelDto Tempo { get; set; } = TempoFeelDto.Keep;

    /// <summary>Mood tags.</summary>
    public List<string> Moods { get; } = [];

    /// <summary>Instrument tags.</summary>
    public List<string> Instruments { get; } = [];

    /// <summary>Vocal handling.</summary>
    public VocalHandlingDto Vocals { get; set; } = VocalHandlingDto.Keep;

    /// <summary>Default strength.</summary>
    public StrengthDto Strength { get; set; } = StrengthDto.Balanced;

    /// <summary>Description ("" for none).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>A form filled with a style's fields.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The form.</returns>
    public static StyleEditorForm From(StyleFields style)
    {
        var form = new StyleEditorForm
        {
            Name = style.Name,
            Genre = style.GenreFamily,
            SubGenre = style.SubGenre ?? string.Empty,
            Tempo = style.TempoFeel,
            Vocals = style.VocalHandling,
            Strength = style.DefaultStrength,
            Description = style.Description ?? string.Empty,
        };
        form.Moods.AddRange(style.Moods);
        form.Instruments.AddRange(style.Instrumentation);
        return form;
    }

    /// <summary>Adds a tag to a group; a tag that is already there (any case) is ignored.</summary>
    /// <param name="tags">The group (<see cref="Moods"/> or <see cref="Instruments"/>).</param>
    /// <param name="field">The group's field name for the problem.</param>
    /// <param name="text">The typed tag.</param>
    /// <returns>Why the tag was refused, or null.</returns>
    public static FieldProblem? AddTag(List<string> tags, string field, string text)
    {
        var tag = text.Trim();
        var problem = TagProblem(tags, tag);
        if (problem is null && !tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(tag);
        }

        return problem is null ? null : new FieldProblem(field, problem);
    }

    /// <summary>Checks the name (the other limits are enforced by the inputs and the API).</summary>
    /// <returns>The problem, or null.</returns>
    public FieldProblem? Validate() =>
        Name.Trim().Length is 0 or > ContractLimits.StyleNameMaxLength ? new FieldProblem("name", "Enter a name of 1 to 60 characters.") : null;

    /// <summary>The request body.</summary>
    /// <returns>The input with trimmed texts; empty optional texts become null.</returns>
    public StyleInput ToInput() => new()
    {
        Name = Name.Trim(),
        GenreFamily = Genre,
        SubGenre = OrNull(SubGenre),
        TempoFeel = Tempo,
        Instrumentation = [.. Instruments],
        Moods = [.. Moods],
        VocalHandling = Vocals,
        DefaultStrength = Strength,
        Description = OrNull(Description),
    };

    private static string? TagProblem(List<string> tags, string tag) =>
        tag.Length == 0 ? "Enter a tag first."
        : tag.Length > ContractLimits.StyleTagMaxLength ? "Tags can be at most 30 characters."
        : tags.Count >= ContractLimits.StyleMaxTags ? "At most 8 tags."
        : null;

    private static string? OrNull(string text) => text.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
