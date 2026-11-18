namespace StemStudio.Domain.Sections;

/// <summary>One section of a track (data-model 3.2). Created and changed only through <see cref="TrackSections"/>.</summary>
public sealed class Section
{
    private Section()
    {
        Label = string.Empty;
    }

    /// <summary>Section id (kept across edits).</summary>
    public Guid Id { get; private set; }

    /// <summary>The track.</summary>
    public Guid TrackId { get; private set; }

    /// <summary>Position 0..63 in time order.</summary>
    public int Position { get; private set; }

    /// <summary>Kind.</summary>
    public SectionKind Kind { get; private set; }

    /// <summary>Label (1..40, trimmed).</summary>
    public string Label { get; private set; }

    /// <summary>Start in track seconds.</summary>
    public double StartSeconds { get; private set; }

    /// <summary>End in track seconds.</summary>
    public double EndSeconds { get; private set; }

    /// <summary>Leading instrument of a solo (channel id), or null.</summary>
    public string? Instrument { get; private set; }

    /// <summary>Analyser confidence; null for user sections.</summary>
    public double? Confidence { get; private set; }

    /// <summary>Detected or user.</summary>
    public SectionOrigin Origin { get; private set; }

    internal static Section FromDetection(Guid id, Guid trackId, int position, DetectedSection found) => new()
    {
        Id = id,
        TrackId = trackId,
        Position = position,
        Kind = found.Kind,
        Label = DetectedLabel(found),
        StartSeconds = found.StartSeconds,
        EndSeconds = found.EndSeconds,
        Instrument = found.Instrument,
        Confidence = found.Confidence,
        Origin = SectionOrigin.Detected,
    };

    internal static Section FromUser(Guid id, Guid trackId, int position, SectionInput input)
    {
        var section = new Section { Id = id, TrackId = trackId, Origin = SectionOrigin.User };
        section.Apply(position, input);
        return section;
    }

    internal void Edit(int position, SectionInput input)
    {
        if (!Matches(input))
        {
            (Origin, Confidence) = (SectionOrigin.User, null);
        }

        Apply(position, input);
    }

    private static string DetectedLabel(DetectedSection found)
    {
        var label = SectionRules.CleanLabel(found.Label);
        label = label.Length == 0 ? KindName(found.Kind) : label;
        return label.Length <= SectionRules.MaxLabelLength ? label : label[..SectionRules.MaxLabelLength];
    }

    private static string KindName(SectionKind kind) => kind == SectionKind.PreChorus ? "Pre-chorus" : kind.ToString();

    private bool Matches(SectionInput input) =>
        Kind == input.Kind && Label == SectionRules.CleanLabel(input.Label) && StartSeconds.Equals(input.StartSeconds)
        && EndSeconds.Equals(input.EndSeconds) && Instrument == input.Instrument;

    private void Apply(int position, SectionInput input)
    {
        (Position, Kind, Label) = (position, input.Kind, SectionRules.CleanLabel(input.Label));
        (StartSeconds, EndSeconds, Instrument) = (input.StartSeconds, input.EndSeconds, input.Instrument);
    }
}
