using System.Globalization;

namespace StemStudio.Domain.Sections;

/// <summary>Validation of a replaced section list (014 FR-002, FR-006, data-model 3.2).</summary>
public static class SectionRules
{
    /// <summary>Most sections per track.</summary>
    public const int MaxSections = 64;

    /// <summary>Longest label after trimming.</summary>
    public const int MaxLabelLength = 40;

    /// <summary>Shortest section in seconds.</summary>
    public const double MinSectionSeconds = 1.0;

    private const double Tolerance = 1e-9;

    /// <summary>Checks a whole list; field names follow the request (<c>sections[i].startSeconds</c>).</summary>
    /// <param name="sections">The ordered list.</param>
    /// <param name="context">Track length and channels.</param>
    /// <param name="ownIds">Ids of the sections the track has now.</param>
    /// <returns>The violations (empty when valid).</returns>
    public static IReadOnlyList<DomainError> Validate(IReadOnlyList<SectionInput> sections, SectionContext context, IReadOnlySet<Guid> ownIds)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(context);
        var errors = new ValidationErrors();
        errors.Check(sections.Count is >= 1 and <= MaxSections, "sections", $"Between 1 and {MaxSections} sections are needed.");
        var seen = new HashSet<Guid>();
        for (var i = 0; i < sections.Count; i++)
        {
            var itemErrors = errors.Nested(string.Create(CultureInfo.InvariantCulture, $"sections[{i}]"));
            ValidateOne(itemErrors, sections[i], context);
            ValidateOrder(itemErrors, sections, i);
            ValidateId(itemErrors, sections[i].Id, ownIds, seen);
        }

        return errors.Errors;
    }

    /// <summary>The trimmed label.</summary>
    /// <param name="label">Raw label.</param>
    /// <returns>Trimmed label (empty for null).</returns>
    public static string CleanLabel(string? label) => label?.Trim() ?? string.Empty;

    private static void ValidateOne(ValidationErrors errors, SectionInput section, SectionContext context)
    {
        var label = CleanLabel(section.Label);
        errors.Check(label.Length is >= 1 and <= MaxLabelLength, "label", $"The label needs 1 to {MaxLabelLength} characters.");
        errors.Check(section.StartSeconds >= 0, "startSeconds", "A section must not start before the track.");
        errors.Check(section.EndSeconds <= context.DurationSeconds + Tolerance, "endSeconds", "A section must not end after the track.");
        errors.Check(section.EndSeconds - section.StartSeconds >= MinSectionSeconds - Tolerance, "endSeconds", "A section must be at least 1 second long.");
        ValidateInstrument(errors, section, context);
    }

    private static void ValidateInstrument(ValidationErrors errors, SectionInput section, SectionContext context)
    {
        if (section.Instrument is not { } instrument)
        {
            return;
        }

        errors.Check(section.Kind == SectionKind.Solo, "instrument", "Only a solo can name an instrument.");
        errors.Check(section.Kind != SectionKind.Solo || context.Channels.Contains(instrument), "instrument", $"The track has no channel '{instrument}'.");
    }

    private static void ValidateOrder(ValidationErrors errors, IReadOnlyList<SectionInput> sections, int index) =>
        errors.Check(index == 0 || sections[index].StartSeconds >= sections[index - 1].EndSeconds - Tolerance, "startSeconds", "Sections must be in order and must not overlap.");

    private static void ValidateId(ValidationErrors errors, Guid? id, IReadOnlySet<Guid> ownIds, HashSet<Guid> seen)
    {
        if (id is not { } value)
        {
            return;
        }

        errors.Check(ownIds.Contains(value), "id", "This section does not belong to the track.");
        errors.Check(!ownIds.Contains(value) || seen.Add(value), "id", "Each section id may be sent only once.");
    }
}
