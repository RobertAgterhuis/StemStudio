using StemStudio.Api.Tracks;
using StemStudio.Contracts.Styles;
using StemStudio.Domain.Styles;
using StyleFields = StemStudio.Domain.Styles.StyleFields;

namespace StemStudio.Api.Styles;

/// <summary>Maps styles between the domain and the contract (enum members share their names).</summary>
public static class StyleMapper
{
    /// <summary>A library style.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The DTO.</returns>
    public static StyleDto ToDto(Style style)
    {
        ArgumentNullException.ThrowIfNull(style);
        var header = new StyleDto
        {
            Id = style.Id,
            Kind = Map<StyleKindDto>(style.Kind),
            PresetKey = style.PresetKey,
            CreatedAt = TrackMapper.Utc(style.CreatedAt),
            UpdatedAt = TrackMapper.Utc(style.UpdatedAt),
            Name = string.Empty,
            GenreFamily = default,
            TempoFeel = default,
            VocalHandling = default,
            DefaultStrength = default,
        };
        return Fill(header, style.Fields);
    }

    /// <summary>A style snapshot of a generation.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The DTO.</returns>
    public static StyleSnapshotDto ToDto(StyleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var header = new StyleSnapshotDto
        {
            StyleId = snapshot.StyleId,
            Kind = Map<StyleKindDto>(snapshot.Kind),
            PresetKey = snapshot.PresetKey,
            Name = string.Empty,
            GenreFamily = default,
            TempoFeel = default,
            VocalHandling = default,
            DefaultStrength = default,
        };
        return Fill(header, snapshot.Fields);
    }

    /// <summary>The domain fields of an editor input.</summary>
    /// <param name="input">The input.</param>
    /// <returns>The fields.</returns>
    public static StyleFields ToFields(StyleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new StyleFields
        {
            Name = input.Name ?? string.Empty,
            GenreFamily = Map<GenreFamily>(input.GenreFamily),
            SubGenre = input.SubGenre,
            TempoFeel = Map<TempoFeel>(input.TempoFeel),
            Instrumentation = input.Instrumentation ?? [],
            Moods = input.Moods ?? [],
            VocalHandling = Map<VocalHandling>(input.VocalHandling),
            DefaultStrength = Map<Strength>(input.DefaultStrength),
            Description = input.Description,
        };
    }

    /// <summary>Maps an enum to the enum with the same member name.</summary>
    /// <typeparam name="T">The target enum.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The mapped value.</returns>
    public static T Map<T>(Enum value)
        where T : struct, Enum => Scores.ScoreMapper.Map<T>(value);

    private static T Fill<T>(T dto, StyleFields fields)
        where T : StyleDefinition => (T)(dto with
        {
            Name = fields.Name,
            GenreFamily = Map<GenreFamilyDto>(fields.GenreFamily),
            SubGenre = fields.SubGenre,
            TempoFeel = Map<TempoFeelDto>(fields.TempoFeel),
            Instrumentation = fields.Instrumentation,
            Moods = fields.Moods,
            VocalHandling = Map<VocalHandlingDto>(fields.VocalHandling),
            DefaultStrength = Map<StrengthDto>(fields.DefaultStrength),
            Description = fields.Description,
        });
}
