namespace StemStudio.Domain.Styles;

/// <summary>Whether a style is a curated preset or made by the user (feature 009, data-model 3.1).</summary>
public enum StyleKind
{
    /// <summary>Curated and read-only (embedded resource).</summary>
    Preset,

    /// <summary>Created by the user.</summary>
    User,
}

/// <summary>Genre family of a style.</summary>
public enum GenreFamily
{
    /// <summary>Rock.</summary>
    Rock,

    /// <summary>Pop.</summary>
    Pop,

    /// <summary>Jazz.</summary>
    Jazz,

    /// <summary>Blues.</summary>
    Blues,

    /// <summary>Electronic.</summary>
    Electronic,

    /// <summary>Hip hop.</summary>
    HipHop,

    /// <summary>Latin.</summary>
    Latin,

    /// <summary>Folk.</summary>
    Folk,

    /// <summary>Classical.</summary>
    Classical,

    /// <summary>World.</summary>
    World,

    /// <summary>Country.</summary>
    Country,

    /// <summary>Metal.</summary>
    Metal,

    /// <summary>Soul.</summary>
    Soul,

    /// <summary>Ambient.</summary>
    Ambient,
}

/// <summary>Tempo feel; <see cref="Keep"/> follows the source, the others steer the caption.</summary>
public enum TempoFeel
{
    /// <summary>Follow the source.</summary>
    Keep,

    /// <summary>Laid back.</summary>
    Laidback,

    /// <summary>Driving.</summary>
    Driving,

    /// <summary>Half time.</summary>
    HalfTime,

    /// <summary>Swing.</summary>
    Swing,
}

/// <summary>What happens to the vocals of the source.</summary>
public enum VocalHandling
{
    /// <summary>The vocals stay in the source mix.</summary>
    Keep,

    /// <summary>The vocal stem is excluded and the caption says "instrumental".</summary>
    Instrumental,
}

/// <summary>How far a take may move from the source (5-step scale, ADR-018 D7).</summary>
public enum Strength
{
    /// <summary>Cover strength 0.85.</summary>
    Subtle,

    /// <summary>Cover strength 0.70.</summary>
    Light,

    /// <summary>Cover strength 0.55.</summary>
    Balanced,

    /// <summary>Cover strength 0.40.</summary>
    Strong,

    /// <summary>Cover strength 0.25.</summary>
    Free,
}

/// <summary>A preset from the embedded resource.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Key">Stable key, e.g. <c>jazz-trio</c>.</param>
/// <param name="Fields">The style fields.</param>
public sealed record PresetDefinition(Guid Id, string Key, StyleFields Fields);

/// <summary>A preset cannot be changed or deleted.</summary>
public sealed class StyleReadOnlyException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public StyleReadOnlyException()
        : base("Presets cannot be changed. Duplicate the preset to make your own version.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public StyleReadOnlyException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public StyleReadOnlyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
