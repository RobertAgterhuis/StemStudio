using System.Globalization;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Result of a trainer edit: the new settings, or why they were refused.</summary>
/// <param name="Settings">The settings; null when refused.</param>
/// <param name="Error">The reason; null when accepted.</param>
public sealed record TrainerEdit(SpeedTrainerSettingsDto? Settings, string? Error);

/// <summary>
/// The four number fields of the speed trainer as typed (012 FR-010): every field is validated together, so a target
/// typed below the start stays visible with its error until the user fixes either field (data-model.md 2.2).
/// </summary>
public sealed class TrainerForm
{
    /// <summary>Range message of the start field.</summary>
    public const string StartMessage = "Start must be a whole number from 50 to 119 %.";

    /// <summary>Range message of the step field.</summary>
    public const string StepMessage = "Step must be a whole number from 1 to 20 %.";

    /// <summary>Range message of the target field.</summary>
    public const string TargetMessage = "Target must be a whole number from 51 to 120 %.";

    /// <summary>Range message of the passes field.</summary>
    public const string PassesMessage = "Passes per step must be a whole number from 1 to 8.";

    /// <summary>Start speed as typed.</summary>
    public string? Start { get; set; }

    /// <summary>Step as typed.</summary>
    public string? Step { get; set; }

    /// <summary>Target speed as typed.</summary>
    public string? Target { get; set; }

    /// <summary>Passes per step as typed.</summary>
    public string? Passes { get; set; }

    /// <summary>Fills the fields from saved settings.</summary>
    /// <param name="settings">The settings.</param>
    public void Load(SpeedTrainerSettingsDto settings)
    {
        Start = Text(settings.StartPercent);
        Step = Text(settings.StepPercent);
        Target = Text(settings.TargetPercent);
        Passes = Text(settings.PassesPerStep);
    }

    /// <summary>Validates all fields (ranges, then target above start).</summary>
    /// <param name="enabled">Whether the trainer is on.</param>
    /// <returns>The edit.</returns>
    public TrainerEdit Validate(bool enabled)
    {
        var fields = Fields();
        var values = fields.Select(f => InputValue.Whole(f.Text, f.Min, f.Max)).ToArray();
        var invalid = Array.FindIndex(values, v => v is null);
        if (invalid >= 0)
        {
            return new(null, fields[invalid].Message);
        }

        return values[0] < values[2]
            ? new(new SpeedTrainerSettingsDto(enabled, values[0]!.Value, values[1]!.Value, values[2]!.Value, values[3]!.Value), null)
            : new(null, PracticeLimits.TargetAboveStartMessage);
    }

    private (string? Text, int Min, int Max, string Message)[] Fields() =>
    [
        (Start, PracticeLimits.MinSpeedPercent, PracticeLimits.MaxTrainerStartPercent, StartMessage),
        (Step, PracticeLimits.MinTrainerStepPercent, PracticeLimits.MaxTrainerStepPercent, StepMessage),
        (Target, PracticeLimits.MinTrainerTargetPercent, PracticeLimits.MaxSpeedPercent, TargetMessage),
        (Passes, PracticeLimits.MinPassesPerStep, PracticeLimits.MaxPassesPerStep, PassesMessage),
    ];

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
