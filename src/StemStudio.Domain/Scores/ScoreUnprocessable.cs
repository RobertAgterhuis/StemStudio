namespace StemStudio.Domain.Scores;

/// <summary>Why the instrument settings of a score request cannot be used (the API answers a typed 422; data-model 4.1-4.3).</summary>
public enum ScoreUnprocessable
{
    /// <summary>Tablature for a profile other than guitar or bass.</summary>
    TablatureNotAllowed,

    /// <summary>A tuning of the other instrument (a bass tuning for guitar or the reverse).</summary>
    TuningMismatch,

    /// <summary>A capo with a bass tuning.</summary>
    CapoNotAllowed,

    /// <summary>A clef for a grand-staff profile (keys, piano).</summary>
    ClefNotAllowed,

    /// <summary>A part for the drums profile.</summary>
    PartNotAllowed,
}

/// <summary>The instrument settings of a score request break a rule that has its own problem type.</summary>
public sealed class ScoreUnprocessableException : ArgumentException
{
    /// <summary>Creates the exception for a code.</summary>
    /// <param name="code">The code.</param>
    /// <param name="detail">The text shown to the user.</param>
    public ScoreUnprocessableException(ScoreUnprocessable code, string detail)
        : base(detail)
    {
        Code = code;
    }

    /// <summary>Creates a <see cref="ScoreUnprocessable.TablatureNotAllowed"/> exception.</summary>
    public ScoreUnprocessableException()
        : this(ScoreUnprocessable.TablatureNotAllowed, ScoreTablatureRules.TablatureNotAllowedText)
    {
    }

    /// <summary>Creates a <see cref="ScoreUnprocessable.TablatureNotAllowed"/> exception with a message.</summary>
    /// <param name="message">The message.</param>
    public ScoreUnprocessableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ScoreUnprocessableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The code.</summary>
    public ScoreUnprocessable Code { get; }
}

/// <summary>The tablature rules of a score request (data-model 4.1; FR-020, FR-021).</summary>
public static class ScoreTablatureRules
{
    /// <summary>Highest capo fret.</summary>
    public const int MaxCapo = 12;

    /// <summary>Text of <see cref="ScoreUnprocessable.TablatureNotAllowed"/>.</summary>
    public const string TablatureNotAllowedText = "Tablature is available for the guitar and bass profiles only.";

    /// <summary>Text of the capo field error.</summary>
    public const string CapoRangeText = "Must be 0 to 12.";

    /// <summary>The instrument of a profile's tablature (<c>guitar</c> or <c>bass</c>), else null.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The instrument or null.</returns>
    public static string? InstrumentOf(ScoreProfile profile) => profile switch
    {
        ScoreProfile.Guitar => InstrumentViews.InstrumentCatalogue.GuitarInstrument,
        ScoreProfile.Bass => InstrumentViews.InstrumentCatalogue.BassInstrument,
        _ => null,
    };

    /// <summary>Checks tablature against the profile, in the order profile, tuning, capo; the capo range is left to the caller.</summary>
    /// <param name="tablature">The settings, or null.</param>
    /// <param name="profile">The resolved profile.</param>
    /// <exception cref="ScoreUnprocessableException">A rule is broken.</exception>
    public static void Check(TablatureSettings? tablature, ScoreProfile profile)
    {
        if (tablature is not null)
        {
            CheckTuning(InstrumentViews.InstrumentCatalogue.TuningOf(tablature.Tuning), tablature.Capo, profile);
        }
    }

    /// <summary>True when the capo lies in 0..12 (or there is no tablature).</summary>
    /// <param name="tablature">The settings, or null.</param>
    /// <returns>Whether the capo is valid.</returns>
    public static bool CapoInRange(TablatureSettings? tablature) => tablature is null || tablature.Capo is >= 0 and <= MaxCapo;

    private static void CheckTuning(InstrumentViews.Tuning tuning, int capo, ScoreProfile profile)
    {
        var instrument = InstrumentOf(profile) ?? throw new ScoreUnprocessableException(ScoreUnprocessable.TablatureNotAllowed, TablatureNotAllowedText);
        if (tuning.Instrument != instrument)
        {
            throw new ScoreUnprocessableException(ScoreUnprocessable.TuningMismatch, $"The tuning {tuning.Label} is not a {instrument} tuning.");
        }

        if (!tuning.IsGuitar && capo > 0)
        {
            throw new ScoreUnprocessableException(ScoreUnprocessable.CapoNotAllowed, "A capo is available for guitar tunings only.");
        }
    }
}

/// <summary>The part rules of a score request (data-model 4.2, 4.3; FR-051, FR-052).</summary>
public static class ScorePartRules
{
    /// <summary>True for the profiles written on a grand staff, which keep their own clefs.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Whether the clef is fixed.</returns>
    public static bool IsGrandStaff(ScoreProfile profile) => profile is ScoreProfile.Keys or ScoreProfile.Piano;

    /// <summary>True when a part (written instrument) can be chosen: every pitched profile, not drums.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Whether a part is allowed.</returns>
    public static bool PartAllowed(ScoreProfile profile) => profile != ScoreProfile.Drums;

    /// <summary>Checks a part against the profile: drums take none, grand-staff profiles take no clef but <c>auto</c>.</summary>
    /// <param name="part">The part, or null.</param>
    /// <param name="profile">The resolved profile.</param>
    /// <exception cref="ScoreUnprocessableException">A rule is broken.</exception>
    public static void Check(PartSettings? part, ScoreProfile profile)
    {
        if (part is not null && !PartAllowed(profile))
        {
            throw new ScoreUnprocessableException(ScoreUnprocessable.PartNotAllowed, "Drum notation has no written instrument or clef.");
        }

        if (part is { Clef: not InstrumentViews.ClefChoice.Auto } && IsGrandStaff(profile))
        {
            throw new ScoreUnprocessableException(ScoreUnprocessable.ClefNotAllowed, "Keys and piano scores keep their two staves; choose the automatic clef.");
        }
    }
}
