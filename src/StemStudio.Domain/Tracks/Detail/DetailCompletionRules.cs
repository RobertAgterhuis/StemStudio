using System.Globalization;

namespace StemStudio.Domain.Tracks.Detail;

/// <summary>Rules for the per-parent outcomes of a completed detail job (data-model.md 2.2 and 3.1).</summary>
internal static class DetailCompletionRules
{
    /// <summary>Validates the outcomes against the track's stems.</summary>
    /// <param name="parents">Reported outcomes.</param>
    /// <param name="trackStems">Stem names of the track.</param>
    /// <exception cref="DomainValidationException">A rule is violated.</exception>
    public static void Validate(IReadOnlyList<ParentOutcome> parents, IReadOnlyList<string> trackStems)
    {
        var errors = new ValidationErrors();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < parents.Count; i++)
        {
            var parent = parents[i];
            var parentErrors = errors.Nested(string.Create(CultureInfo.InvariantCulture, $"parents[{i}]"));
            parentErrors.Check(trackStems.Contains(parent.Parent) && seen.Add(parent.Parent), "parent", $"'{parent.Parent}' is not a stem of the track or is listed twice.");
            ValidateStems(parentErrors, parent);
        }

        errors.ThrowIfAny();
    }

    private static void ValidateStems(ValidationErrors errors, ParentOutcome parent)
    {
        if (parent.Outcome != ParentOutcomeKind.Split)
        {
            errors.Check(parent.Stems.Count == 0, "stems", "Only split parents have detail stems.");
            return;
        }

        var names = parent.Stems.Select(stem => stem.Name).ToList();
        errors.Check(HasValidCount(names) && AreAllowedOnce(parent.Parent, names), "stems", "A split parent needs at least two of its own detail stems plus 'rest', each at most once.");
        errors.Check(parent.Stems.All(stem => stem.SizeBytes >= 0), "stems", "Detail stem sizes must not be negative.");
    }

    private static bool HasValidCount(List<string> names) =>
        names.Count is >= 3 and <= DetailCatalog.MaxStemsPerParent && names.Contains(DetailCatalog.Rest);

    private static bool AreAllowedOnce(string parent, List<string> names) =>
        names.Distinct(StringComparer.Ordinal).Count() == names.Count && names.All(name => DetailCatalog.IsAllowed(parent, name));
}
