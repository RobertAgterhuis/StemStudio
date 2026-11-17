namespace StemStudio.Domain.Practice;

/// <summary>Kind of a practised section: the nine section kinds or <see cref="None"/> (time outside any section).</summary>
public enum PracticeSectionKind
{
    /// <summary>Intro.</summary>
    Intro,

    /// <summary>Verse.</summary>
    Verse,

    /// <summary>Pre-chorus.</summary>
    PreChorus,

    /// <summary>Chorus.</summary>
    Chorus,

    /// <summary>Bridge.</summary>
    Bridge,

    /// <summary>Solo.</summary>
    Solo,

    /// <summary>Instrumental part.</summary>
    Instrumental,

    /// <summary>Outro.</summary>
    Outro,

    /// <summary>Anything else.</summary>
    Other,

    /// <summary>No section.</summary>
    None,
}

/// <summary>Practice time of one section within a session, as the browser sends it.</summary>
public sealed record PracticeSectionInput
{
    /// <summary>Section id at recording time, or null.</summary>
    public Guid? SectionId { get; init; }

    /// <summary>Kind snapshot.</summary>
    public PracticeSectionKind Kind { get; init; }

    /// <summary>Label snapshot (0..40).</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Played seconds in this section.</summary>
    public double PlayedSeconds { get; init; }

    /// <summary>Completed loop passes of this section.</summary>
    public int LoopPasses { get; init; }

    /// <summary>Highest speed of a completed trainer pass of this section, or null.</summary>
    public double? TrainerReachedPercent { get; init; }
}

/// <summary>A whole session with cumulative values, as the browser sends it (<c>PUT sessions/{id}</c>).</summary>
public sealed record PracticeSessionInput
{
    /// <summary>The track.</summary>
    public Guid TrackId { get; init; }

    /// <summary>The mix in use, or null.</summary>
    public Guid? MixId { get; init; }

    /// <summary>Start (UTC).</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>Last update / end (UTC).</summary>
    public DateTime EndedAt { get; init; }

    /// <summary>Played seconds (count-in included, pauses excluded).</summary>
    public double PlayedSeconds { get; init; }

    /// <summary>Completed loop passes.</summary>
    public int LoopPasses { get; init; }

    /// <summary>Highest playback speed, or null.</summary>
    public double? MaxSpeedPercent { get; init; }

    /// <summary>Time per section.</summary>
    public IReadOnlyList<PracticeSectionInput> Sections { get; init; } = [];
}
