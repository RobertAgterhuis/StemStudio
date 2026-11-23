namespace StemStudio.Domain.Insights;

/// <summary>Status of a track's stem quality report (data-model 5.1, feature 020).</summary>
public enum QualityReportStatus
{
    /// <summary>Waiting on the worker's insights lane.</summary>
    Queued,

    /// <summary>Being made.</summary>
    Running,

    /// <summary>Ready.</summary>
    Ready,

    /// <summary>The last run failed; the last ready stems are kept.</summary>
    Failed,
}

/// <summary>Rating of a stem (data-model 4.5).</summary>
public enum QualityRating
{
    /// <summary>Good.</summary>
    Good,

    /// <summary>Fair.</summary>
    Fair,

    /// <summary>Poor.</summary>
    Poor,

    /// <summary>Probably not in this song.</summary>
    NotPresent,

    /// <summary>Not rated.</summary>
    Unknown,
}

/// <summary>Level of one quality dimension (data-model 4.5).</summary>
public enum QualityLevel
{
    /// <summary>Low.</summary>
    Low,

    /// <summary>Medium.</summary>
    Medium,

    /// <summary>High.</summary>
    High,

    /// <summary>Not shown.</summary>
    Unknown,
}

/// <summary>Reliability label of a dimension (gate G1).</summary>
public enum Reliability
{
    /// <summary>Validated on real data.</summary>
    Validated,

    /// <summary>Rough indication.</summary>
    Indicative,
}

/// <summary>Reason code of a rating (data-model 4.6).</summary>
public enum QualityReason
{
    /// <summary>Little bleed and few artefacts.</summary>
    Clean,

    /// <summary>Bleed of another stem.</summary>
    Bleed,

    /// <summary>Bleed, or the two instruments playing together.</summary>
    BleedTogether,

    /// <summary>Separation artefacts.</summary>
    Artefacts,

    /// <summary>Bleed and artefacts.</summary>
    BleedAndArtefacts,

    /// <summary>Probably not in the song.</summary>
    NotPresent,

    /// <summary>Too little audio.</summary>
    TooShort,
}

/// <summary>Kind of separation artefact (data-model 4.4).</summary>
public enum ArtefactKind
{
    /// <summary>Flicker.</summary>
    Flicker,

    /// <summary>Dropouts.</summary>
    Dropouts,

    /// <summary>Model residual of the detail split.</summary>
    ModelResidual,

    /// <summary>Much went to the rest stem.</summary>
    RestShare,
}

/// <summary>Bleed estimate of a stem (worker <c>BleedEstimate</c>).</summary>
/// <param name="Level">Level.</param>
/// <param name="Index">Leakage share 0..1.</param>
/// <param name="MainSource">Standard stem most of the bleed comes from, or null.</param>
/// <param name="CoOccurrence">The co-occurrence guard capped the level.</param>
/// <param name="Reliability">Reliability label.</param>
public sealed record BleedEstimate(QualityLevel Level, double Index, string? MainSource, bool CoOccurrence, Reliability Reliability);

/// <summary>Artefact estimate of a stem (worker <c>ArtefactEstimate</c>).</summary>
/// <param name="Level">Level.</param>
/// <param name="Index">Artefact index 0..1.</param>
/// <param name="Kinds">Kinds found.</param>
/// <param name="Reliability">Reliability label.</param>
public sealed record ArtefactEstimate(QualityLevel Level, double Index, IReadOnlyList<ArtefactKind> Kinds, Reliability Reliability);

/// <summary>The worker's measures of one stem or detail stem (worker <c>StemQuality</c>, data-model 2.4).</summary>
/// <param name="Channel">ChannelId (<c>vocals</c>, <c>drums.kick</c>).</param>
/// <param name="Rating">Rating.</param>
/// <param name="Reason">Reason code.</param>
/// <param name="ActiveSeconds">Active audio in seconds.</param>
/// <param name="ActiveShare">Active share 0..1.</param>
/// <param name="LevelDb">Level relative to the mix.</param>
/// <param name="Bleed">Bleed estimate.</param>
/// <param name="Artefacts">Artefact estimate.</param>
public sealed record StemQuality(
    string Channel, QualityRating Rating, QualityReason Reason, double ActiveSeconds, double ActiveShare, double LevelDb, BleedEstimate Bleed, ArtefactEstimate Artefacts);
