namespace StemStudio.Domain.Sections;

/// <summary>A warning of the analyser (contract <c>SectionWarning</c>).</summary>
/// <param name="Code">Warning code (for example <c>grid-unreliable</c>).</param>
/// <param name="Message">Readable text.</param>
public sealed record SectionWarning(string Code, string Message);

/// <summary>One section as the user sends it (<c>PUT sections</c>).</summary>
/// <param name="Id">Id of a kept section; null for a new one.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Label">Label (trimmed, 1..40).</param>
/// <param name="StartSeconds">Start in track seconds.</param>
/// <param name="EndSeconds">End in track seconds.</param>
/// <param name="Instrument">Channel id of the leading instrument (solo only).</param>
public sealed record SectionInput(Guid? Id, SectionKind Kind, string Label, double StartSeconds, double EndSeconds, string? Instrument = null);

/// <summary>One section found by the analyser.</summary>
/// <param name="Kind">Kind.</param>
/// <param name="Label">Label as the worker wrote it.</param>
/// <param name="StartSeconds">Start in track seconds.</param>
/// <param name="EndSeconds">End in track seconds.</param>
/// <param name="Instrument">Leading instrument of a solo, or null.</param>
/// <param name="Confidence">Confidence 0..1, or null.</param>
public sealed record DetectedSection(SectionKind Kind, string Label, double StartSeconds, double EndSeconds, string? Instrument, double? Confidence);

/// <summary>A completed detection job.</summary>
/// <param name="Analyzer">Analyser id and version (<c>stems-novelty/1.0.0</c>).</param>
/// <param name="Snap">How the analyser snapped the boundaries.</param>
/// <param name="Warnings">Analyser warnings.</param>
/// <param name="Sections">The sections in order.</param>
public sealed record DetectionResult(string Analyzer, SectionSnap? Snap, IReadOnlyList<SectionWarning> Warnings, IReadOnlyList<DetectedSection> Sections);

/// <summary>A replace request: the stored revision the user edited and the whole new list.</summary>
/// <param name="Revision">Revision the edit is based on.</param>
/// <param name="Sections">The new ordered list.</param>
public sealed record SectionSetUpdate(int Revision, IReadOnlyList<SectionInput> Sections);

/// <summary>What the rules need to know about the track.</summary>
/// <param name="DurationSeconds">Track length.</param>
/// <param name="Channels">The track's channel ids (standard stems and split detail stems).</param>
public sealed record SectionContext(double DurationSeconds, IReadOnlySet<string> Channels);

/// <summary>The section set was saved by someone else since the edit started (409 <c>sections-changed</c>).</summary>
public sealed class SectionsChangedException : Exception
{
    /// <summary>Creates the exception with the default message.</summary>
    public SectionsChangedException()
        : base("The sections were changed in another window.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    public SectionsChangedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Cause.</param>
    public SectionsChangedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
