namespace StemStudio.Domain.Tracks.Detail;

/// <summary>What the detail phase did with one standard stem (FR-003..FR-006).</summary>
public enum ParentOutcomeKind
{
    /// <summary>Split into at least two detail stems plus <c>rest</c>.</summary>
    Split,

    /// <summary>Not split: the stem is (almost) silent.</summary>
    SkippedSilent,

    /// <summary>Not split: fewer than two detail stems were audible.</summary>
    NothingFurther,

    /// <summary>Not split: the plausibility or reconstruction check failed.</summary>
    NotReliable,

    /// <summary>Not split: no detail model exists for the stem (bass).</summary>
    NotSupported,
}
