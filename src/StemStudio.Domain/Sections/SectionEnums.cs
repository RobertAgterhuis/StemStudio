namespace StemStudio.Domain.Sections;

/// <summary>The kind of a song section (014 FR-002).</summary>
public enum SectionKind
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

    /// <summary>Solo (may name the leading instrument).</summary>
    Solo,

    /// <summary>Instrumental part.</summary>
    Instrumental,

    /// <summary>Outro.</summary>
    Outro,

    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>Where one section comes from.</summary>
public enum SectionOrigin
{
    /// <summary>Found by the worker's analyser and not changed since.</summary>
    Detected,

    /// <summary>Added or changed by the user.</summary>
    User,
}

/// <summary>Where the whole section set comes from.</summary>
public enum SectionSetOrigin
{
    /// <summary>The last replace came from detection.</summary>
    Detected,

    /// <summary>The user saved the set at least once since the last detection.</summary>
    Edited,
}

/// <summary>Detection status of a section set (data-model 3.1).</summary>
public enum SectionSetStatus
{
    /// <summary>Never detected (or the submission failed).</summary>
    None,

    /// <summary>Submitted to the worker.</summary>
    Queued,

    /// <summary>The worker is analysing.</summary>
    Detecting,

    /// <summary>Sections are available.</summary>
    Ready,

    /// <summary>Detection failed; <see cref="TrackSections.Error"/> says why.</summary>
    Failed,
}

/// <summary>How boundaries snap (014 FR-003).</summary>
public enum SectionSnap
{
    /// <summary>To downbeats (reliable grid).</summary>
    Downbeat,

    /// <summary>To beats (unreliable grid).</summary>
    Beat,

    /// <summary>To 0.1 s (no grid).</summary>
    Time,
}

/// <summary>Why a detection request is refused.</summary>
public enum DetectRefusal
{
    /// <summary>Not refused.</summary>
    None,

    /// <summary>A detection is already queued or running.</summary>
    DetectionRunning,

    /// <summary>The user edited the sections and did not ask to overwrite them.</summary>
    SectionsEdited,
}
