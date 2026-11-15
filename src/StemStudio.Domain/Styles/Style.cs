namespace StemStudio.Domain.Styles;

/// <summary>A style of the library: a read-only preset or a user style (feature 009, data-model 3.1).</summary>
public sealed class Style
{
    private List<string> _instrumentation = [];
    private List<string> _moods = [];

    private Style()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    /// <summary>Identifier (stable for presets).</summary>
    public Guid Id { get; private set; }

    /// <summary>Preset or user.</summary>
    public StyleKind Kind { get; private set; }

    /// <summary>Stable key of a preset; null for user styles.</summary>
    public string? PresetKey { get; private set; }

    /// <summary>Name.</summary>
    public string Name { get; private set; }

    /// <summary>Upper-case name for the unique index.</summary>
    public string NormalizedName { get; private set; }

    /// <summary>Genre family.</summary>
    public GenreFamily GenreFamily { get; private set; }

    /// <summary>Sub-genre.</summary>
    public string? SubGenre { get; private set; }

    /// <summary>Tempo feel.</summary>
    public TempoFeel TempoFeel { get; private set; }

    /// <summary>Instrument tags.</summary>
    public IReadOnlyList<string> Instrumentation => _instrumentation;

    /// <summary>Mood tags.</summary>
    public IReadOnlyList<string> Moods => _moods;

    /// <summary>Vocal handling.</summary>
    public VocalHandling VocalHandling { get; private set; }

    /// <summary>Default strength.</summary>
    public Strength DefaultStrength { get; private set; }

    /// <summary>Description.</summary>
    public string? Description { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>True for a preset.</summary>
    public bool IsPreset => Kind == StyleKind.Preset;

    /// <summary>The current fields.</summary>
    public StyleFields Fields => new()
    {
        Name = Name,
        GenreFamily = GenreFamily,
        SubGenre = SubGenre,
        TempoFeel = TempoFeel,
        Instrumentation = _instrumentation.ToList(),
        Moods = _moods.ToList(),
        VocalHandling = VocalHandling,
        DefaultStrength = DefaultStrength,
        Description = Description,
    };

    /// <summary>Creates a user style.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="fields">The fields (normalised and validated).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The style.</returns>
    /// <exception cref="DomainValidationException">A field breaks a limit.</exception>
    public static Style CreateUser(Guid id, StyleFields fields, DateTime nowUtc)
    {
        var style = new Style { Id = id, Kind = StyleKind.User, CreatedAt = nowUtc };
        style.Apply(Valid(fields), nowUtc);
        return style;
    }

    /// <summary>Creates a preset row from the resource.</summary>
    /// <param name="preset">The preset.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The style.</returns>
    public static Style FromPreset(PresetDefinition preset, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var style = new Style { Id = preset.Id, Kind = StyleKind.Preset, PresetKey = preset.Key, CreatedAt = nowUtc };
        style.Apply(Valid(preset.Fields), nowUtc);
        return style;
    }

    /// <summary>Replaces the fields of a user style.</summary>
    /// <param name="fields">The new fields.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="StyleReadOnlyException">The style is a preset.</exception>
    /// <exception cref="DomainValidationException">A field breaks a limit.</exception>
    public void Update(StyleFields fields, DateTime nowUtc)
    {
        RequireUser();
        Apply(Valid(fields), nowUtc);
    }

    /// <summary>Upserts a preset from the resource (start-up seeding).</summary>
    /// <param name="preset">The preset definition.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>True when something changed.</returns>
    /// <exception cref="StyleReadOnlyException">The row is a user style.</exception>
    public bool ApplyPreset(PresetDefinition preset, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var fields = IsPreset ? Valid(preset.Fields) : throw new StyleReadOnlyException($"Style {Id} is not a preset.");
        if (fields.Equals(Fields) && preset.Key == PresetKey)
        {
            return false;
        }

        PresetKey = preset.Key;
        Apply(fields, nowUtc);
        return true;
    }

    /// <summary>The style as it is now, for a generation.</summary>
    /// <returns>The snapshot.</returns>
    public StyleSnapshot Snapshot() => new(Id, Kind, PresetKey, Fields);

    private void RequireUser()
    {
        if (IsPreset)
        {
            throw new StyleReadOnlyException();
        }
    }

    private static StyleFields Valid(StyleFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var normalized = fields.Normalized();
        var errors = StyleRules.Validate(normalized);
        return errors.Count == 0 ? normalized : throw new DomainValidationException(errors);
    }

    private void Apply(StyleFields fields, DateTime nowUtc)
    {
        (Name, NormalizedName, GenreFamily, SubGenre, TempoFeel) = (fields.Name, StyleNaming.Normalize(fields.Name), fields.GenreFamily, fields.SubGenre, fields.TempoFeel);
        (_instrumentation, _moods) = (fields.Instrumentation.ToList(), fields.Moods.ToList());
        (VocalHandling, DefaultStrength, Description, UpdatedAt) = (fields.VocalHandling, fields.DefaultStrength, fields.Description, nowUtc);
    }
}
