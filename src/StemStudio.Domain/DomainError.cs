namespace StemStudio.Domain;

/// <summary>One violated domain rule.</summary>
/// <param name="Field">JSON-style path of the offending value relative to the validated object, e.g. <c>metronome.bpmOverride</c>.</param>
/// <param name="Message">A message the user can act on.</param>
public sealed record DomainError(string Field, string Message);
