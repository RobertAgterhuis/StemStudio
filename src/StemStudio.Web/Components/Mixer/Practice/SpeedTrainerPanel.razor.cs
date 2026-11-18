using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer.Practice;

/// <summary>Speed trainer settings and status; validates with <see cref="TrainerForm"/>, emits accepted settings.</summary>
public sealed partial class SpeedTrainerPanel
{
    /// <summary>Id of the needs-loop text (also referenced by the count-in's every-pass checkbox).</summary>
    public const string NeedsLoopId = "practice-needs-loop";

    private readonly TrainerForm _form = new();
    private SpeedTrainerSettingsDto? _loaded;

    /// <summary>The saved trainer settings.</summary>
    [Parameter, EditorRequired]
    public SpeedTrainerSettingsDto Settings { get; set; } = SpeedTrainerSettingsDto.Default;

    /// <summary>The engine's trainer progress.</summary>
    [Parameter]
    public TrainerStatus Status { get; set; } = PracticeStatus.Initial.Trainer;

    /// <summary>Why the loop does not allow the trainer (<see cref="PracticeRules.LoopProblem"/>), or null.</summary>
    [Parameter]
    public string? LoopProblem { get; set; }

    /// <summary>The trainer region named by the loop ("Region: Guitar solo (0:40-0:56)"; 014 A-012-3), or null without a loop.</summary>
    [Parameter]
    public string? RegionLabel { get; set; }

    /// <summary>True while the time-stretch stage is unavailable.</summary>
    [Parameter]
    public bool Unavailable { get; set; }

    /// <summary>Raised with accepted settings.</summary>
    [Parameter]
    public EventCallback<SpeedTrainerSettingsDto> OnChange { get; set; }

    /// <summary>The switch was pressed.</summary>
    [Parameter]
    public EventCallback OnToggle { get; set; }

    private string? Error { get; set; }

    // Turning on needs a usable loop and the stage; an enabled trainer can always be turned off.
    private bool ToggleDisabled => !Settings.Enabled && (LoopProblem is not null || Unavailable);

    private TrainerStatusText StatusText => PracticeText.TrainerStatus(Status, Settings);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Settings != _loaded)
        {
            _loaded = Settings;
            _form.Load(Settings);
        }
    }

    private Task EditAsync(ChangeEventArgs args, Action<TrainerForm, string?> set)
    {
        set(_form, args.Value?.ToString());
        var edit = _form.Validate(Settings.Enabled);
        Error = edit.Error;
        if (edit.Settings is not { } settings)
        {
            return Task.CompletedTask;
        }

        _loaded = settings;
        return OnChange.InvokeAsync(settings);
    }
}
