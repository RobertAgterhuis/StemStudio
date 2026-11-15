using StemStudio.Contracts.Styles;

namespace StemStudio.Web.Styles;

/// <summary>Conversions between the style records of the backend contract (1.8.0).</summary>
public static class StyleInputs
{
    /// <summary>The editable fields of a style, snapshot or input as a <see cref="StyleInput"/>.</summary>
    /// <param name="fields">The source.</param>
    /// <returns>An input with the same field values.</returns>
    public static StyleInput ToInput(this StyleFields fields) => new()
    {
        Name = fields.Name,
        GenreFamily = fields.GenreFamily,
        SubGenre = fields.SubGenre,
        TempoFeel = fields.TempoFeel,
        Instrumentation = fields.Instrumentation,
        Moods = fields.Moods,
        VocalHandling = fields.VocalHandling,
        DefaultStrength = fields.DefaultStrength,
        Description = fields.Description,
    };
}
